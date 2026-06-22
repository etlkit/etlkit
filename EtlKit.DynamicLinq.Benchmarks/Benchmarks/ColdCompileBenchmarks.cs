using System.Linq.Dynamic.Core;
using EtlKit.DynamicLinq;
using EtlKit.Scripting;
using BenchmarkDotNet.Attributes;

using EtlKit.DynamicLinq.Benchmarks.TestData;

namespace EtlKit.DynamicLinq.Benchmarks.Benchmarks;

/// <summary>
/// Measures the cost of "first call on a new shape": parse the expression, build
/// the runtime type, compile to a delegate, evaluate once. Each iteration uses a
/// freshly-named shape so neither engine can reuse a previously emitted type.
/// </summary>
/// <remarks>
/// Three engines:
/// 1. Roslyn via ScriptBuilder (what ScriptedRowTransformation uses internally).
/// 2. Dynamic LINQ on a typed POCO (ExpressionRowFiltration&lt;TInput&gt; path).
/// 3. Dynamic LINQ on ExpandoObject (ExpressionRowFiltration path - mapping +
///    Array.CreateInstance trick).
/// Two complexity levels: Simple (one comparison) and Composite (boolean logic).
/// </remarks>
[MemoryDiagnoser]
public class ColdCompileBenchmarks
{
    private static readonly ParsingConfig s_parsingConfig =
        new() { ConvertObjectToSupportComparison = true };

    private int _shapeId;

    [Params("Simple", "Composite")]
    public string ExpressionKind { get; set; } = "Simple";

    [IterationSetup]
    public void IterationSetup()
    {
        _shapeId++;
    }

    [Benchmark(Baseline = true, Description = "Roslyn (ScriptBuilder) - fresh shape")]
    public bool Roslyn_FreshShape()
    {
        var row = ExpandoFactory.NewShape(_shapeId);
        var suffix = $"_S{_shapeId}";
        var expression =
            ExpressionKind == "Simple" ? Expressions.Simple(suffix) : Expressions.Composite(suffix);

        var dict = (IDictionary<string, object?>)row;
        var runner = ScriptBuilder
            .Default.ForType(dict, hashCode: _shapeId)
            .CreateRunner<bool>(expression);
        runner.Script.Compile();
        return runner.RunAsync(dict).Result.ReturnValue;
    }

    [Benchmark(Description = "Dynamic LINQ generic typed POCO")]
    public bool DynamicLinq_TypedPoco()
    {
        // Typed POCO does not have a per-iteration "shape" - the type is fixed.
        // What rotates per iteration is the expression text (closure on _shapeId not
        // meaningful here), so we still parse + compile fresh by feeding a slightly
        // different expression. We append "&& 1 == 1" with a per-iteration constant
        // pattern to defeat any internal expression cache in Dynamic LINQ.
        var row = ExpandoFactory.CanonicalTyped();
        var marker = _shapeId % 1000;
        var expression =
            ExpressionKind == "Simple"
                ? $"Reserve > 0 && {marker} == {marker}"
                : $"(AdminReserveRatio != AdminReserveRatioPrevious) && Reserve > 0 && Type == \"Day\" && {marker} == {marker}";

        return new[] { row }.AsQueryable().Any(s_parsingConfig, expression);
    }

    [Benchmark(Description = "Dynamic LINQ ExpandoObject - fresh shape")]
    public bool DynamicLinq_Expando_FreshShape()
    {
        var row = ExpandoFactory.NewShape(_shapeId);
        var suffix = $"_S{_shapeId}";
        var expression =
            ExpressionKind == "Simple" ? Expressions.Simple(suffix) : Expressions.Composite(suffix);

        var (type, instance) = ExpandoTypeMapper.Map(row);
        var array = Array.CreateInstance(type, 1);
        array.SetValue(instance, 0);
        return array.AsQueryable().Any(s_parsingConfig, expression);
    }
}
