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
/// Compiles and evaluates a predicate on N distinct shapes per iteration. Measures
/// total time and allocated memory through BenchmarkDotNet, plus loaded-assembly
/// count and managed memory through GlobalCleanup probes.
/// </summary>
/// <remarks>
/// This is the central benchmark for the Q2 review discussion: does Roslyn
/// accumulate assemblies and memory linearly with the number of shapes?
/// Dynamic LINQ on ExpandoObject emits new types into a shared persistent
/// AssemblyBuilder via DynamicClassFactory (no Assembly.Load per shape),
/// so the curve should be much flatter on the assembly-count axis.
///
/// Typed POCO path is intentionally omitted: a typed TInput is one fixed type,
/// "many shapes" does not apply.
/// </remarks>
[MemoryDiagnoser]
public class ManyShapesBenchmarks
{
    [Params(1, 5, 10)]
    public int N { get; set; }

    private static readonly ParsingConfig s_parsingConfig =
        new() { ConvertObjectToSupportComparison = true };

    private int _iterationOffset;
    private int _assembliesAtStart;
    private long _memoryAtStart;
    private string? _engineLabel;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Snapshot baseline at the start of a benchmark run, before any compiles.
        // BDN re-invokes GlobalSetup per parameter combination.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        _memoryAtStart = GC.GetTotalMemory(forceFullCollection: true);
        _assembliesAtStart = AppDomain.CurrentDomain.GetAssemblies().Length;
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var memoryAtEnd = GC.GetTotalMemory(forceFullCollection: true);
        var assembliesAtEnd = AppDomain.CurrentDomain.GetAssemblies().Length;

        Console.WriteLine();
        Console.WriteLine($"=== Probe ({_engineLabel}, N={N}) ===");
        Console.WriteLine($"  Memory delta:    {memoryAtEnd - _memoryAtStart:N0} bytes");
        Console.WriteLine($"  Assembly delta:  {assembliesAtEnd - _assembliesAtStart}");
        Console.WriteLine();
    }

    [Benchmark(Baseline = true, Description = "Roslyn - compile N shapes")]
    public int Roslyn_NShapes()
    {
        _engineLabel = "Roslyn";
        var passed = 0;
        for (var i = 0; i < N; i++)
        {
            // +1 to avoid shapeId=0, which makes ExpandoFactory.NewShape produce
            // unsuffixed field names while the expression below uses "_S0" suffix.
            var shapeId = _iterationOffset * 100_000 + i + 1;
            var row = ExpandoFactory.NewShape(shapeId);
            var dict = (IDictionary<string, object?>)row;
            var suffix = $"_S{shapeId}";
            var expression = $"Reserve{suffix} > 0";

            var runner = ScriptBuilder
                .Default.ForType(dict, hashCode: shapeId)
                .CreateRunner<bool>(expression);
            runner.Script.Compile();
            if (runner.RunAsync(dict).Result.ReturnValue)
                passed++;
        }
        _iterationOffset++;
        return passed;
    }

    [Benchmark(Description = "Dynamic LINQ ExpandoObject - compile N shapes")]
    public int DynamicLinq_Expando_NShapes()
    {
        _engineLabel = "DynamicLinq_Expando";
        var passed = 0;
        for (var i = 0; i < N; i++)
        {
            // +1 to avoid shapeId=0, which makes ExpandoFactory.NewShape produce
            // unsuffixed field names while the expression below uses "_S0" suffix.
            var shapeId = _iterationOffset * 100_000 + i + 1;
            var row = ExpandoFactory.NewShape(shapeId);
            var suffix = $"_S{shapeId}";
            var expression = $"Reserve{suffix} > 0";

            var (type, instance) = ExpandoTypeMapper.Map(row);
            var array = Array.CreateInstance(type, 1);
            array.SetValue(instance, 0);
            if (array.AsQueryable().Any(s_parsingConfig, expression))
                passed++;
        }
        _iterationOffset++;
        return passed;
    }
}
