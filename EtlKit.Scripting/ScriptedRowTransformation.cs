using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;

using EtlKit.Common.DataFlow;

using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.Extensions.Logging;

namespace EtlKit.Scripting;

/// <inheritdoc />
[PublicAPI]
public class ScriptedTransformation : ScriptedRowTransformation<ExpandoObject, ExpandoObject>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public ScriptedTransformation() { }

    /// <summary>
    /// Creates a new instance with an injected logger.
    /// </summary>
    public ScriptedTransformation(ILogger<ScriptedTransformation> logger)
        : base(logger) { }
}

/// <summary>
/// Transforms a row with a C# script expressions for each field.
/// </summary>
/// <typeparam name="TInput"></typeparam>
/// <typeparam name="TOutput"></typeparam>
[PublicAPI]
public class ScriptedRowTransformation<TInput, TOutput> : RowTransformation<TInput, TOutput>
{
    /// <summary>
    /// Mapping of input fields to output fields, where
    /// each key is the output field name, each value is the script to transform the input field to the output field
    /// </summary>
    public Dictionary<string, string> Mappings { get; set; } = new();

    /// <summary>
    /// Additional assembly names or file paths to load for the script.
    /// Accepts a runtime assembly name (e.g. <c>System.Text.Json</c>) or a file path
    /// (e.g. <c>Files/MyLib.dll</c>).
    /// </summary>
    public IEnumerable<string> AdditionalAssemblyNames
    {
        get => _additionalAssemblies.Select(x => x.GetName().FullName);
        set => _additionalAssemblies = value.Select(LoadAssembly);
    }

    /// <summary>
    /// Additional namespace imports for the C# script expressions in <see cref="Mappings"/>.
    /// Each entry is a namespace string such as <c>"System.Text.Json"</c>.
    /// </summary>
    public IEnumerable<string> AdditionalImports { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Controls the nullable annotation context for compiled script expressions in <see cref="Mappings"/>.
    /// Defaults to <see cref="NullableContextOptions.Disable"/> for backward compatibility.
    /// Set to <see cref="NullableContextOptions.Enable"/> to allow nullable annotations
    /// such as <c>string?</c> and null-conditional operators inside scripts.
    /// </summary>
    public NullableContextOptions NullableContextOptions { get; set; } =
        NullableContextOptions.Disable;

    /// <summary>
    /// Indicates if transformation should fail when missing mapping field on source.
    /// * True: Transformation will fail when a field is missing in the source or script fails to compile.
    /// * False: Transformation will return null when a field is missing in the source, or script is failed to compile.
    /// </summary>
    public bool FailOnMissingField { get; set; } = true;

    /// <summary>
    /// When true, all input fields are copied to the output before applying Mappings.
    /// Mappings may add new fields or override existing ones. When false (default),
    /// only the fields listed in Mappings appear in the output.
    /// </summary>
    /// <remarks>
    /// For typed transformations, copy is supported only when <typeparamref name="TInput"/> is the
    /// same type as, or a subtype of, <typeparamref name="TOutput"/>. Setting this to
    /// <see langword="true"/> with an incompatible type pair throws
    /// <see cref="InvalidOperationException"/> at runtime.
    /// </remarks>
    public bool PassThrough { get; set; } = false;

    private static readonly bool _outputAssignableFromInput = typeof(TOutput).IsAssignableFrom(
        typeof(TInput)
    );
    private static readonly PropertyInfo[] _passThroughProperties = typeof(TOutput)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanRead && p.CanWrite)
        .ToArray();
    private static readonly ConcurrentDictionary<string, PropertyInfo?> _outputPropertiesCache =
        new();

    private IEnumerable<Assembly> _additionalAssemblies = Array.Empty<Assembly>();
    private readonly ConcurrentDictionary<string, ScriptRunner<object>?> _runnersCache = new();

    public ScriptedRowTransformation()
        : this(logger: null) { }

    /// <summary>
    /// Creates a new instance with an injected logger.
    /// </summary>
    public ScriptedRowTransformation(ILogger<ScriptedRowTransformation<TInput, TOutput>>? logger)
        : base(logger)
    {
        if (typeof(TInput).IsArray || typeof(TOutput).IsArray)
            throw new ArgumentException(
                "Array types are not supported. Use a singular object type instead."
            );
        TransformationFunc = ScriptedTransformation;
    }

    private TOutput ScriptedTransformation(TInput arg)
    {
        return typeof(IDictionary<string, object?>).IsAssignableFrom(typeof(TInput))
            ? (TOutput)TransformWithScriptDynamic((IDictionary<string, object?>)arg!)
            : TransformWithScriptTyped(arg);
    }

    private dynamic TransformWithScriptDynamic(IDictionary<string, object?> arg)
    {
        dynamic output =
            Activator.CreateInstance(typeof(TOutput))
            ?? throw new InvalidOperationException(
                $"Could not create instance of output type '{typeof(TOutput).FullName}'. This may be caused by a missing parameterless constructor."
            );

        if (PassThrough)
        {
            var outputDict = (IDictionary<string, object?>)output;
            foreach (var pair in arg)
                outputDict[pair.Key] = pair.Value;
        }

        var type = ScriptBuilder
            .Default.ForType(arg)
            .WithReferences(_additionalAssemblies)
            .WithImports(AdditionalImports)
            .WithNullableContextOptions(NullableContextOptions);

        foreach (var key in Mappings.Keys)
        {
            var runner = GetScriptRunner(key, type);
            dynamic? value = runner?.RunAsync(arg).Result.ReturnValue;

            try
            {
                ((IDictionary<string, object?>)output)[key] = value;
            }
            catch (RuntimeBinderException e)
            {
                throw new ArgumentException(
                    $"Property {key} not found on type {typeof(TOutput).FullName}.",
                    e
                );
            }
            catch (Exception e)
            {
                throw new ArgumentException(
                    $"Could not set property {key} on type {typeof(TOutput).FullName}.",
                    e
                );
            }
        }

        return output;
    }

    private TOutput TransformWithScriptTyped(TInput arg)
    {
        TOutput output =
            (TOutput)Activator.CreateInstance(typeof(TOutput))!
            ?? throw new InvalidOperationException(
                $"Could not create instance of output type {typeof(TOutput).FullName}. This may be caused by a missing parameterless constructor."
            );

        if (PassThrough && _outputAssignableFromInput)
        {
            foreach (var prop in _passThroughProperties)
                prop.SetValue(output, prop.GetValue(arg));
        }
        else if (PassThrough)
        {
            throw new InvalidOperationException(
                $"PassThrough requires TInput ({typeof(TInput).FullName}) to be the same type as or a subtype of TOutput ({typeof(TOutput).FullName})."
            );
        }

        var builder = ScriptBuilder
            .Default.ForType<TInput>()
            .WithReferences(_additionalAssemblies)
            .WithImports(AdditionalImports)
            .WithNullableContextOptions(NullableContextOptions);
        foreach (var key in Mappings.Keys)
        {
            var runner = GetScriptRunner(key, builder);

            var value = runner?.RunAsync(arg).Result.ReturnValue;
            try
            {
                var property = _outputPropertiesCache.GetOrAdd(
                    key,
                    k => typeof(TOutput).GetProperty(k)
                );
                if (property == null)
                    throw new ArgumentException(
                        $"Property {key} not found on type {typeof(TOutput).FullName}."
                    );
                property.SetValue(output, value);
            }
            catch (Exception e)
            {
                throw new ArgumentException(
                    $"Could not set property {key} on type {typeof(TOutput).FullName}.",
                    e
                );
            }
        }

        return output;
    }

    private static Assembly LoadAssembly(string nameOrPath)
    {
        try
        {
            return Assembly.Load(nameOrPath);
        }
        catch (Exception e) when (e is FileNotFoundException or FileLoadException)
        {
            // nameOrPath is not a valid assembly identity — treat it as a file path.
#pragma warning disable S3885 // Replace this call to 'Assembly.LoadFrom' with 'Assembly.Load'. File load for plugins is legitimate
            return Assembly.LoadFrom(nameOrPath);
#pragma warning restore S3885
        }
    }

    private ScriptRunner<object>? GetScriptRunner(string key, TypedScriptBuilder builder) =>
        _runnersCache.GetOrAdd(
            // The cache key must include the globals type. Otherwise, a runner compiled
            // for one ExpandoObject shape may be reused for a different shape and fail
            // when constructing the globals object (argument initialization).
            $"{builder.GlobalsType.FullName}::{Mappings[key]}",
            _ =>
            {
                var runner = builder.CreateRunner<object>(Mappings[key]);
                var diagnostics = runner.Script.Compile();

                if (!diagnostics.Any())
                {
                    return runner;
                }

                if (FailOnMissingField)
                {
                    throw new ArgumentException(
                        $"Could not compile script for '{typeof(TOutput).FullName}.{key}' => {Mappings[key]}.",
                        diagnostics.First().GetMessage()
                    );
                }

                return null;
            }
        );
}
