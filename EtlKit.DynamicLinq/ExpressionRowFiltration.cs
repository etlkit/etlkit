using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.CustomTypeProviders;
using System.Linq.Expressions;
using System.Reflection;
using EtlKit.DataFlow;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace EtlKit.DynamicLinq;

/// <summary>
/// Filters rows by a string expression parsed and compiled by
/// <c>System.Linq.Dynamic.Core</c>. Suited for XML-defined flows where the predicate
/// is configured in the package, not in C# code. No Roslyn, no per-shape assembly
/// emission, no <c>Assembly.Load</c>.
/// </summary>
/// <typeparam name="TInput">
/// Row type. Properties referenced in <see cref="FilterExpression"/> must be public.
/// For typed <c>TInput</c> property names are resolved through <c>PropertyInfo</c>
/// directly; no runtime type generation is needed.
/// </typeparam>
/// <remarks>
/// <para>
/// Supported in the expression: comparisons (<c>==</c>, <c>!=</c>, <c>&gt;</c>, <c>&lt;</c>,
/// <c>&gt;=</c>, <c>&lt;=</c>), logical operators (<c>&amp;&amp;</c>, <c>||</c>, <c>!</c>),
/// arithmetic (<c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>, <c>%</c>), member access on nested
/// objects, null checks, and LINQ-style methods on homogeneous collections
/// (<c>Items.Any(predicate)</c>, <c>Items.Count()</c>, <c>Items.Sum(selector)</c>,
/// <c>Items.Contains(value)</c>).
/// </para>
/// <para>
/// For the broader picture - when to pick this over <c>ScriptedRowTransformation</c>,
/// limitations on heterogeneous collections, internals of the
/// <see cref="ExpressionRowFiltration"/> non-generic form, supported value types - see
/// <c>docs/dataflow/row-filtration.md</c>.
/// </para>
/// <para>
/// The <see cref="ParsingConfig.ConvertObjectToSupportComparison"/> flag is enabled so
/// comparison operators work on properties typed as <c>object</c> (null-valued fields
/// and mixed numeric literals such as <c>Reserve &gt; 0</c> when <c>Reserve</c> is
/// <c>decimal</c>).
/// </para>
/// <para>
/// Thread safety: not thread-safe. Configure (<see cref="FilterExpression"/>,
/// <see cref="AdditionalAssemblyNames"/>, <see cref="AdditionalImports"/>,
/// <see cref="RegisterCustomTypes"/>, <see cref="ParsingConfig"/>) once before the
/// dataflow starts. EtlKit runs each dataflow step on a single thread, so
/// concurrent calls to <c>EvaluateExpression</c> from the same instance are not
/// expected. Sharing one instance across multiple parallel pipelines is unsupported.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ExpressionRowFiltration&lt;ChangeRatioRow&gt; filtration = new ExpressionRowFiltration&lt;ChangeRatioRow&gt;(
///     "AdminReserveRatioPrevious != AdminReserveRatio");
/// source.LinkTo(filtration);
/// filtration.LinkTo(destination);
/// </code>
/// </example>
[PublicAPI]
public class ExpressionRowFiltration<TInput> : RowFiltration<TInput>
{
    /// <summary>
    /// Parser configuration applied to every <see cref="FilterExpression"/> evaluation.
    /// Default has <see cref="ParsingConfig.ConvertObjectToSupportComparison"/> = true
    /// (enables operators on properties typed as <c>object</c>, handles null-valued
    /// fields and mixed numeric literals like <c>"X &gt; 0"</c> when <c>X</c> is decimal).
    /// </summary>
    /// <remarks>
    /// Mutate this instance to register additional types or change parser flags. For
    /// the typical "let me call methods on my types" case use <see cref="RegisterCustomTypes"/>.
    /// Reassigning the property to a fresh <see cref="ParsingConfig"/> is also supported.
    /// </remarks>
    public ParsingConfig ParsingConfig { get; set; } =
        new() { ConvertObjectToSupportComparison = true };

    /// <summary>
    /// String expression to evaluate for each row.
    /// Field names are resolved from TInput properties.
    /// Supports: ==, !=, >, &lt;, >=, &lt;=, &amp;&amp;, ||, !, +, -, *, /, %
    /// </summary>
    public string FilterExpression { get; set; } = string.Empty;

    public ExpressionRowFiltration()
    {
        PredicateFunc = EvaluateExpression;
    }

    public ExpressionRowFiltration(string filterExpression)
        : this()
    {
        FilterExpression = filterExpression;
    }

    public ExpressionRowFiltration([CanBeNull] ILogger<ExpressionRowFiltration<TInput>> logger)
        : base(logger)
    {
        PredicateFunc = EvaluateExpression;
    }

    /// <summary>
    /// Names (or file paths) of assemblies to load and register so all their public
    /// types become resolvable from <see cref="FilterExpression"/>. Symmetric with
    /// <c>ScriptedRowTransformation.AdditionalAssemblyNames</c>; lets users reference
    /// types from named assemblies in expression text without per-type registration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each entry is resolved in order: (1) already loaded in <c>AppDomain</c> by
    /// short or full name, (2) <c>Assembly.Load(AssemblyName)</c>, (3)
    /// <c>Assembly.LoadFrom(path)</c> as a fallback for paths. Assemblies that fail
    /// all three throw <see cref="InvalidOperationException"/>. The AppDomain
    /// pre-check (step 1) extends the two-step strategy used on the Roslyn side -
    /// long-running processes avoid double-loading already-resident
    /// assemblies.
    /// </para>
    /// <para>
    /// All public exported types from each assembly are added to
    /// <see cref="ParsingConfig.CustomTypeProvider"/>. Combined with explicit
    /// <see cref="RegisterCustomTypes"/> calls and <see cref="AdditionalImports"/>,
    /// this is the bulk-registration path for XML-defined flows.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="xml">
    /// &lt;AdditionalAssemblyNames&gt;
    ///     &lt;string&gt;MyCompany.Domain&lt;/string&gt;
    /// &lt;/AdditionalAssemblyNames&gt;
    /// </code>
    /// </example>
    public IEnumerable<string> AdditionalAssemblyNames
    {
        get => _additionalAssemblyNames.AsEnumerable();
        set
        {
            _additionalAssemblyNames = (value ?? Array.Empty<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToArray();
            _additionalAssemblies = _additionalAssemblyNames
                .Select(AssemblyResolver.Load)
                .ToArray();
            InvalidateCompiledCache();
        }
    }

    /// <summary>
    /// Namespace prefixes used as imports when resolving short type names in
    /// <see cref="FilterExpression"/>. Symmetric with
    /// <c>ScriptedRowTransformation.AdditionalImports</c> (open-source/etlkit
    /// MR !115, MLRSSL-1510); lets users write <c>"MyType.Method()"</c> instead of
    /// <c>"MyCompany.Domain.MyType.Method()"</c> when <c>MyCompany.Domain</c> is in
    /// the imports list.
    /// </summary>
    /// <remarks>
    /// Imports are checked in declaration order. A short name resolves to the first
    /// type whose full name is <c>"{import}.{shortName}"</c> from the registered
    /// custom types (set via <see cref="AdditionalAssemblyNames"/> or
    /// <see cref="RegisterCustomTypes"/>). Falls back to the first type with the
    /// matching short name across all registered types.
    /// </remarks>
    public IEnumerable<string> AdditionalImports
    {
        get => _additionalImports.AsEnumerable();
        set
        {
            _additionalImports = (value ?? Array.Empty<string>())
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .ToArray();
            InvalidateCompiledCache();
        }
    }

    /// <summary>
    /// Register user types so their public instance methods become callable from
    /// <see cref="FilterExpression"/>. Without this, expressions like
    /// <c>"MyDto.SomeMethod() == 1"</c> fail to parse on user-defined types.
    /// Built-in types (<c>string</c>, <c>DateTime</c>, framework types) work without
    /// registration.
    /// </summary>
    /// <remarks>
    /// Combines with any types already registered on <see cref="ParsingConfig"/>;
    /// existing <c>CustomTypeProvider</c> is replaced with one that exposes the union.
    /// For bulk registration of all types from an assembly, use
    /// <see cref="AdditionalAssemblyNames"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var filtration = new ExpressionRowFiltration&lt;Order&gt;("Customer.IsPremium() &amp;&amp; Total &gt; 100");
    /// filtration.RegisterCustomTypes(typeof(Customer));
    /// </code>
    /// </example>
    public void RegisterCustomTypes(params Type[] types)
    {
        if (types is null || types.Length == 0)
            return;

        foreach (var t in types.Where(t => t is not null))
            _registeredTypes.Add(t);

        InvalidateCompiledCache();
    }

    // Explicit per-type registrations via RegisterCustomTypes - tracked separately
    // from assembly-bulk registrations so the two sources can be combined cleanly.
    private readonly HashSet<Type> _registeredTypes = new();

    // Original strings the caller passed to AdditionalAssemblyNames (whether short
    // names, full names or file paths). Kept separate from _additionalAssemblies so
    // the property getter is symmetric with the setter - an XML round-trip yields
    // exactly what the user wrote.
    private string[] _additionalAssemblyNames = Array.Empty<string>();
    private Assembly[] _additionalAssemblies = Array.Empty<Assembly>();
    private string[] _additionalImports = Array.Empty<string>();

    // Tracks whether the current ParsingConfig.CustomTypeProvider was installed
    // by RebuildTypeProvider. Lets us drop our provider when the user clears
    // every registration, without overwriting a manually-assigned provider that
    // was never ours to begin with.
    private bool _providerOwnedByUs;

    // Rebuilds ParsingConfig.CustomTypeProvider from the union of:
    //  - types from RegisterCustomTypes (_registeredTypes)
    //  - public exported types from AdditionalAssemblyNames-loaded assemblies
    // and applies AdditionalImports as namespace prefixes for short-name resolution.
    // Called on every compile cache miss (right before parsing) so XML
    // deserialization, where setter ordering is not guaranteed, never sees a
    // partially-rebuilt provider - the rebuild happens once, on the first row.
    // When all registrations are empty: leaves a manually-assigned
    // ParsingConfig.CustomTypeProvider untouched, but drops our previously-installed
    // provider so stale types/imports do not leak into subsequent evaluations.
    private protected void RebuildTypeProvider()
    {
        var hasRegistrations =
            _registeredTypes.Count > 0
            || _additionalAssemblies.Length > 0
            || _additionalImports.Length > 0;

        if (!hasRegistrations)
        {
            if (_providerOwnedByUs)
            {
                ParsingConfig.CustomTypeProvider = new DefaultDynamicLinqCustomTypeProvider(
                    ParsingConfig,
                    Array.Empty<Type>()
                );
                _providerOwnedByUs = false;
            }
            return;
        }

        var types = new HashSet<Type>(_registeredTypes);
        foreach (var assembly in _additionalAssemblies)
        {
            foreach (var t in AssemblyResolver.GetExportedTypesSafe(assembly))
            {
                types.Add(t);
            }
        }

        ParsingConfig.CustomTypeProvider = new DynamicLinqTypeProvider(types, _additionalImports);
        _providerOwnedByUs = true;
    }

    // Compiled-delegate cache. Holds the last successful Compile() output for
    // FilterExpression + ParsingConfig pair. The TInput-typed path produces a
    // direct Func<TInput, bool>; subsequent EvaluateExpression calls invoke the
    // delegate without re-parsing or building a Queryable per row.
    private Func<TInput, bool>? _compiledPredicate;
    private string? _compiledForExpression;
    private ParsingConfig? _compiledForConfig;

    /// <summary>
    /// Drops the cached compiled delegate, forcing a recompile on the next
    /// <see cref="EvaluateExpression"/> call. Useful when the user mutates
    /// <see cref="ParsingConfig"/> in place; reassigning the property is
    /// detected automatically.
    /// </summary>
    public virtual void InvalidateCompiledCache()
    {
        _compiledPredicate = null;
        _compiledForExpression = null;
        _compiledForConfig = null;
    }

    protected virtual bool EvaluateExpression(TInput row)
    {
        if (string.IsNullOrWhiteSpace(FilterExpression))
            throw new InvalidOperationException("FilterExpression is not set.");

        EnsureCompiled();
        return _compiledPredicate!(row);
    }

    private void EnsureCompiled()
    {
        if (
            _compiledPredicate is not null
            && _compiledForExpression == FilterExpression
            && ReferenceEquals(_compiledForConfig, ParsingConfig)
        )
        {
            return;
        }

        RebuildTypeProvider();
        var lambda = DynamicExpressionParser.ParseLambda<TInput, bool>(
            ParsingConfig,
            createParameterCtor: false,
            FilterExpression
        );
        _compiledPredicate = lambda.Compile();
        _compiledForExpression = FilterExpression;
        _compiledForConfig = ParsingConfig;
    }
}

/// <summary>
/// Non-generic <see cref="ExpressionRowFiltration{TInput}"/> bound to
/// <see cref="ExpandoObject"/>. Used by the XML reader and by flows on dynamic rows.
/// </summary>
/// <remarks>
/// <para>
/// Each row is mapped to a runtime DynamicClass before evaluation: nested
/// <c>IDictionary&lt;string, object&gt;</c> fields are mapped recursively, custom
/// classes are kept as-is and resolved through <c>PropertyInfo</c>, homogeneous
/// collections become typed <c>List&lt;T&gt;</c> (enabling
/// <c>Any(predicate)</c>, <c>Count()</c>, <c>Sum(selector)</c>, <c>Contains(...)</c>
/// in expressions). The mapping is performed by <see cref="ExpandoTypeMapper"/>.
/// </para>
/// <para>
/// The mapped instance is wrapped in an array via <c>Array.CreateInstance(type, 1)</c>
/// - required because <c>new[] { instance }</c> would give <c>object[]</c> and
/// <c>AsQueryable()</c> would lose the runtime element type, breaking property
/// resolution in the parsed expression.
/// </para>
/// <para>
/// Heterogeneous collections (items with different field sets or types) throw at
/// evaluation time. See <c>docs/dataflow/row-filtration.md</c> for the full list of
/// limitations.
/// </para>
/// </remarks>
/// <example>
/// XML form:
/// <code language="xml">
/// &lt;ExpressionRowFiltration&gt;
///     &lt;FilterExpression&gt;Reserve &amp;gt; 0 &amp;amp;&amp;amp; Type == &amp;quot;Day&amp;quot;&lt;/FilterExpression&gt;
/// &lt;/ExpressionRowFiltration&gt;
/// </code>
/// Programmatic form:
/// <code>
/// ExpressionRowFiltration filtration = new ExpressionRowFiltration(
///     "Reserve > 0 &amp;&amp; Type == \"Day\"");
/// </code>
/// </example>
/// <seealso cref="ExpressionRowFiltration{TInput}"/>
[PublicAPI]
public class ExpressionRowFiltration : ExpressionRowFiltration<ExpandoObject>
{
    // Compiled-delegate cache for the ExpandoObject path. The cache key is
    // (FilterExpression, ParsingConfig, mapped DynamicClass type). When a row
    // arrives with the same shape as the cached one, we skip the parse/wrap
    // step and invoke the previously compiled delegate on the freshly mapped
    // instance.
    private Func<object, bool>? _expandoCompiledPredicate;
    private string? _expandoCompiledForExpression;
    private ParsingConfig? _expandoCompiledForConfig;
    private Type? _expandoCompiledForType;

    public ExpressionRowFiltration() { }

    public ExpressionRowFiltration(string filterExpression)
        : base(filterExpression) { }

    public ExpressionRowFiltration([CanBeNull] ILogger<ExpressionRowFiltration> logger)
        : base(logger) { }

    public override void InvalidateCompiledCache()
    {
        base.InvalidateCompiledCache();
        _expandoCompiledPredicate = null;
        _expandoCompiledForExpression = null;
        _expandoCompiledForConfig = null;
        _expandoCompiledForType = null;
    }

    protected override bool EvaluateExpression(ExpandoObject row)
    {
        if (string.IsNullOrWhiteSpace(FilterExpression))
            throw new InvalidOperationException("FilterExpression is not set.");

        var (type, instance) = ExpandoTypeMapper.Map(row);

        if (
            _expandoCompiledPredicate is null
            || _expandoCompiledForExpression != FilterExpression
            || !ReferenceEquals(_expandoCompiledForConfig, ParsingConfig)
            || _expandoCompiledForType != type
        )
        {
            RebuildTypeProvider();
            // Build a Func<object, bool> wrapper around the type-specific lambda
            // so we can invoke it without DynamicInvoke (slow) and without
            // wrapping each row in an Array.CreateInstance + AsQueryable per call.
            var lambda = DynamicExpressionParser.ParseLambda(
                ParsingConfig,
                type,
                typeof(bool),
                FilterExpression
            );
            var paramObj = Expression.Parameter(typeof(object), "o");
            var wrapped = Expression.Lambda<Func<object, bool>>(
                Expression.Invoke(lambda, Expression.Convert(paramObj, type)),
                paramObj
            );

            _expandoCompiledPredicate = wrapped.Compile();
            _expandoCompiledForExpression = FilterExpression;
            _expandoCompiledForConfig = ParsingConfig;
            _expandoCompiledForType = type;
        }

        return _expandoCompiledPredicate(instance);
    }
}
