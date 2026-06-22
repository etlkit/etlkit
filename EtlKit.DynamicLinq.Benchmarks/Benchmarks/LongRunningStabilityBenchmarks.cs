using System.Dynamic;
using System.Linq.Dynamic.Core;
using EtlKit.DynamicLinq;
using EtlKit.Scripting;
using BenchmarkDotNet.Attributes;

using EtlKit.DynamicLinq.Benchmarks.TestData;

// GC.Collect / GC.GetTotalMemory in [GlobalSetup] and [GlobalCleanup] are intentional
// for the memory-delta probe - they bracket the workload to measure assembly and
// managed-memory accumulation across the run. Not part of the timed workload itself.
#pragma warning disable S1215 // GC.Collect should not be called - intentional probe boundary

namespace EtlKit.DynamicLinq.Benchmarks.Benchmarks;

/// <summary>
/// Long-running stability: with one stable shape, evaluate the same predicate
/// for many iterations and probe managed memory delta. Confirms that neither
/// engine accumulates assemblies or memory in steady state, beyond the one-time
/// cost of the first compile.
/// </summary>
/// <remarks>
/// This benchmark complements ColdCompile (one-shot startup) and HotEvaluation
/// (per-row cost). The hypothesis under test: a service running for hours/days
/// with a single stable schema reaches plateau. No linear growth in memory or
/// loaded assemblies as eval count grows.
///
/// The probe in <c>GlobalCleanup</c> reports the change in managed memory and
/// loaded assembly count between the start and end of the benchmark run. With
/// stable shape and pre-warmed compile, the deltas should be small and bounded
/// for both engines (any growth is the GC noise from per-call allocations,
/// which BenchmarkDotNet already tracks as Allocated metric).
///
/// Distinct from ManyShapes: ManyShapes deliberately creates N unique shapes
/// per iteration to stress assembly accumulation. LongRunning uses ONE shape
/// throughout to model a stable-schema service.
/// </remarks>
[MemoryDiagnoser]
public class LongRunningStabilityBenchmarks
{
    private const int ShapeId = 1;
    private const string Expression = "Reserve_S1 > 0";

    [Params(10_000, 100_000)]
    public int EvalCount { get; set; }

    private ExpandoObject _row = new();
    private IDictionary<string, object?> _dict = null!;
    private ScriptRunner<bool> _roslynRunner = null!;
    private ParsingConfig _parsingConfig = null!;

    private int _assembliesAtStart;
    private long _memoryAtStart;
    private string? _engineLabel;

    [GlobalSetup]
    public void Setup()
    {
        _row = ExpandoFactory.NewShape(ShapeId);
        _dict = (IDictionary<string, object?>)_row;

        _roslynRunner = ScriptBuilder
            .Default.ForType(_dict, hashCode: ShapeId)
            .CreateRunner<bool>(Expression);
        _roslynRunner.Script.Compile();

        _parsingConfig = new ParsingConfig { ConvertObjectToSupportComparison = true };

        // Warm both pipelines once so the steady-state probe doesn't include first-call cost.
        _ = _roslynRunner.RunAsync(_dict).Result.ReturnValue;
        var (type, instance) = ExpandoTypeMapper.Map(_row);
        var array = System.Array.CreateInstance(type, 1);
        array.SetValue(instance, 0);
        _ = array.AsQueryable().Any(_parsingConfig, Expression);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        _memoryAtStart = GC.GetTotalMemory(forceFullCollection: true);
        _assembliesAtStart = AppDomain.CurrentDomain.GetAssemblies().Length;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var memoryAtEnd = GC.GetTotalMemory(forceFullCollection: true);
        var assembliesAtEnd = AppDomain.CurrentDomain.GetAssemblies().Length;

        Console.WriteLine();
        Console.WriteLine($"=== LongRunning Probe ({_engineLabel}, EvalCount={EvalCount}) ===");
        Console.WriteLine($"  Memory delta:    {memoryAtEnd - _memoryAtStart:N0} bytes");
        Console.WriteLine($"  Assembly delta:  {assembliesAtEnd - _assembliesAtStart}");
        Console.WriteLine();
    }

    [Benchmark(Baseline = true, Description = "Roslyn - long-running")]
    public int Roslyn_LongRunning()
    {
        _engineLabel = "Roslyn";
        var passed = 0;
        for (var i = 0; i < EvalCount; i++)
        {
            if (_roslynRunner.RunAsync(_dict).Result.ReturnValue)
                passed++;
        }
        return passed;
    }

    [Benchmark(Description = "Dynamic LINQ - long-running")]
    public int DynamicLinq_LongRunning()
    {
        _engineLabel = "DynamicLinq";
        var passed = 0;
        for (var i = 0; i < EvalCount; i++)
        {
            var (type, instance) = ExpandoTypeMapper.Map(_row);
            var array = System.Array.CreateInstance(type, 1);
            array.SetValue(instance, 0);
            if (array.AsQueryable().Any(_parsingConfig, Expression))
                passed++;
        }
        return passed;
    }
}
