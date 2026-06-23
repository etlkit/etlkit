using System.Dynamic;
using System.Linq.Dynamic.Core;
using EtlKit.DynamicLinq;
using EtlKit.Scripting;
using BenchmarkDotNet.Attributes;

using EtlKit.DynamicLinq.Benchmarks.TestData;

namespace EtlKit.DynamicLinq.Benchmarks.Benchmarks;

/// <summary>
/// Steady-state per-row evaluation cost: one shape compiled once, then evaluated
/// many times. Answers the Round 5 oral remark on per-row hot-path performance
/// in DataFlow runtime - distinct from ColdCompile (per-shape startup), HeadToHead
/// (full pipeline overhead) and ManyShapes (long-running with shape drift).
/// </summary>
/// <remarks>
/// The benchmark intentionally pre-warms each engine in <c>GlobalSetup</c> so the
/// per-call cost reflects steady state, not first-call overhead.
///
/// What each benchmark measures:
/// 1. <c>Roslyn_HotEval</c> - <c>runner.RunAsync(...).Result.ReturnValue</c> path
///    that <see cref="ScriptedRowTransformation{TInput,TOutput}"/> uses for every
///    row. Pays Task allocation, state machine, and synchronous wait per call.
/// 2. <c>DynamicLinq_AsQueryableAny_HotEval</c> - the path that
///    <see cref="ExpressionRowFiltration"/> uses today: build single-element array,
///    AsQueryable, Any with the expression. The expression parse is cached by
///    System.Linq.Dynamic.Core internally (see ParsingConfig CacheKeyFactory).
///
/// The Roslyn vs Dynamic LINQ comparison here is what tells us the per-row cost
/// difference in a long-running service after warmup, which the headline
/// ColdCompile metric cannot answer alone.
/// </remarks>
[MemoryDiagnoser]
public class HotEvaluationBenchmarks
{
    private const int ShapeId = 1;
    private const string Expression = "Reserve_S1 > 0";

    private ExpandoObject _row = new();
    private IDictionary<string, object?> _dict = null!;

    private ScriptRunner<bool> _roslynRunner = null!;
    private ParsingConfig _parsingConfig = null!;

    // Pre-warmed ExpressionRowFiltration instances (cached compiled delegates).
    private ExpressionRowFiltration _expandoFiltration = null!;
    private ExpressionRowFiltration<ChangeRatioRow> _typedFiltration = null!;
    private ChangeRatioRow _typedRow = null!;

    [GlobalSetup]
    public void Setup()
    {
        _row = ExpandoFactory.NewShape(ShapeId);
        _dict = (IDictionary<string, object?>)_row;

        // Roslyn: pre-compile the runner so the per-call benchmark only measures evaluation.
        _roslynRunner = ScriptBuilder
            .Default.ForType(_dict, hashCode: ShapeId)
            .CreateRunner<bool>(Expression);
        _roslynRunner.Script.Compile();

        // Dynamic LINQ: warm the parser cache by running once so subsequent calls hit
        // the System.Linq.Dynamic.Core internal expression cache.
        _parsingConfig = new ParsingConfig { ConvertObjectToSupportComparison = true };
        _ = DynamicLinq_AsQueryableAny_HotEval();

        // Pre-warmed ExpressionRowFiltration with cached compiled delegate.
        // The ExpandoTypeMapper now routes flat shapes through a compiled per-shape
        // mapper (Expression Trees, no per-row reflection), so this single cell
        // measures the post-Optimization-2 hot path on ExpandoObject.
        _expandoFiltration = new ExpressionRowFiltration(Expression);
        _ = _expandoFiltration_HotEval(); // first call compiles + caches the delegate

        // Typed POCO ExpressionRowFiltration<TInput> with cached compiled delegate.
        _typedRow = new ChangeRatioRow
        {
            AdminReserveRatio = 25,
            AdminReserveRatioPrevious = 20,
            AuthLimit = 500_000m,
            Reserve = 100m,
            Type = "Day",
        };
        _typedFiltration = new ExpressionRowFiltration<ChangeRatioRow>("Reserve > 0");
        _ = _typedFiltration_HotEval(); // first call compiles + caches
    }

    /// <summary>
    /// Roslyn: per-call runner.RunAsync(...).Result.ReturnValue.
    /// This is the path ScriptedRowTransformation uses internally for each row.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Roslyn - per-row eval (warm)")]
    public bool Roslyn_HotEval()
    {
        return _roslynRunner.RunAsync(_dict).Result.ReturnValue;
    }

    /// <summary>
    /// Dynamic LINQ via AsQueryable().Any() with NO delegate cache - per-call full
    /// pipeline. This was the implementation before the Round 5 optimization;
    /// kept as a baseline to demonstrate the gain from caching.
    /// </summary>
    [Benchmark(Description = "Dynamic LINQ (AsQueryable.Any, no cache) - per-row eval (warm)")]
    public bool DynamicLinq_AsQueryableAny_HotEval()
    {
        var (type, instance) = ExpandoTypeMapper.Map(_row);
        var array = System.Array.CreateInstance(type, 1);
        array.SetValue(instance, 0);
        return array.AsQueryable().Any(_parsingConfig, Expression);
    }

    /// <summary>
    /// ExpressionRowFiltration on ExpandoObject with cached compiled delegate.
    /// Per call: ExpandoTypeMapper.Map (compiled per-shape mapper for flat shapes)
    /// + cached Func&lt;object, bool&gt; invocation. No re-parse, no Queryable wrap,
    /// no per-row reflection.
    /// </summary>
    [Benchmark(Description = "Dynamic LINQ ExpandoObject (cached delegate) - per-row eval (warm)")]
    public bool _expandoFiltration_HotEval()
    {
        return _expandoFiltration.PredicateFunc!(_row);
    }

    /// <summary>
    /// ExpressionRowFiltration&lt;TInput&gt; on typed POCO with cached compiled
    /// delegate. Per call: pure Func&lt;TInput, bool&gt; invocation - no mapping,
    /// no parse, no allocations.
    /// </summary>
    [Benchmark(Description = "Dynamic LINQ typed POCO (cached delegate) - per-row eval (warm)")]
    public bool _typedFiltration_HotEval()
    {
        return _typedFiltration.PredicateFunc!(_typedRow);
    }
}
