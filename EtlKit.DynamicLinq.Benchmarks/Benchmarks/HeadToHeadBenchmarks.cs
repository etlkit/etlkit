using System.Dynamic;
using System.Linq.Dynamic.Core;
using EtlKit.DataFlow;
using EtlKit.DynamicLinq;
using BenchmarkDotNet.Attributes;

namespace EtlKit.DynamicLinq.Benchmarks.Benchmarks;

/// <summary>
/// Direct head-to-head: <see cref="ExpressionRowFiltration"/> (the component shipped
/// in the MR) vs <see cref="ExpressionRowMultiplicationPrototype"/> (the variant the
/// reviewer suggested as the alternative path - inherit from RowMultiplication and
/// return a single-element or empty array).
/// </summary>
/// <remarks>
/// Both components use the same underlying logic for evaluating the expression
/// (System.Linq.Dynamic.Core + ExpandoTypeMapper). The difference is the inheritance
/// chain and the API exposed at the call site:
/// - ExpressionRowFiltration : RowFiltration : DataFlowTransformation - signature
///   Func&lt;TInput, bool&gt;.
/// - ExpressionRowMultiplicationPrototype : RowMultiplication - signature
///   Func&lt;TInput, IEnumerable&lt;TOutput&gt;&gt;, with the user manually returning
///   <c>new[] { row }</c> or <c>Array.Empty&lt;ExpandoObject&gt;()</c>.
///
/// This benchmark answers Q1 of the review: is there any runtime cost from having
/// a separate component, or is it purely a readability decision?
///
/// Both paths build a small in-memory pipeline (MemorySource -> filter -> MemoryDestination)
/// and process the same input set with a 50/50 pass/drop ratio.
/// </remarks>
[MemoryDiagnoser]
public class HeadToHeadBenchmarks
{
    [Params(1_000, 10_000)]
    public int RowCount { get; set; }

    private List<ExpandoObject> _rows = new();

    [GlobalSetup]
    public void Setup()
    {
        _rows = new List<ExpandoObject>(RowCount);
        for (var i = 0; i < RowCount; i++)
        {
            var row = new ExpandoObject();
            var dict = (IDictionary<string, object?>)row;
            // Half the rows pass (Reserve > 0), half drop (Reserve <= 0)
            dict["Reserve"] = (decimal)(i - RowCount / 2);
            dict["Type"] = "Day";
            _rows.Add(row);
        }
    }

    [Benchmark(Baseline = true, Description = "ExpressionRowFiltration (shipped)")]
    public int Filtration_Pipeline()
    {
        var source = new MemorySource<ExpandoObject>(_rows);
        var filter = new ExpressionRowFiltration("Reserve > 0");
        var dest = new MemoryDestination<ExpandoObject>();

        source.LinkTo(filter);
        filter.LinkTo(dest);

        source.Execute();
        dest.Wait();

        return dest.Data.Count;
    }

    [Benchmark(Description = "ExpressionRowMultiplication prototype (reviewer variant)")]
    public int Multiplication_Pipeline()
    {
        var source = new MemorySource<ExpandoObject>(_rows);
        var filter = new ExpressionRowMultiplicationPrototype("Reserve > 0");
        var dest = new MemoryDestination<ExpandoObject>();

        source.LinkTo(filter);
        filter.LinkTo(dest);

        source.Execute();
        dest.Wait();

        return dest.Data.Count;
    }
}

/// <summary>
/// Reviewer-suggested alternative form: instead of a dedicated <c>RowFiltration</c>
/// component with <c>Func&lt;T,bool&gt;</c>, inherit from <c>RowMultiplication</c>
/// and use the empty/single-element collection idiom inside the multiplication
/// function. Same Dynamic LINQ evaluation as <see cref="ExpressionRowFiltration"/>.
/// </summary>
internal sealed class ExpressionRowMultiplicationPrototype
    : RowMultiplication<ExpandoObject, ExpandoObject>
{
    private static readonly ParsingConfig s_parsingConfig =
        new() { ConvertObjectToSupportComparison = true };

    private static readonly ExpandoObject[] s_empty = Array.Empty<ExpandoObject>();

    public string FilterExpression { get; }

    public ExpressionRowMultiplicationPrototype(string filterExpression)
    {
        FilterExpression = filterExpression;
        MultiplicationFunc = Filter;
    }

    private IEnumerable<ExpandoObject> Filter(ExpandoObject row)
    {
        if (row is null)
            return s_empty;

        var (type, instance) = ExpandoTypeMapper.Map(row);
        var array = Array.CreateInstance(type, 1);
        array.SetValue(instance, 0);

        return array.AsQueryable().Any(s_parsingConfig, FilterExpression) ? new[] { row } : s_empty;
    }
}
