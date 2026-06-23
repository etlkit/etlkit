namespace EtlKit.DynamicLinq.Benchmarks.TestData;

/// <summary>
/// Typed POCO for the generic <c>ExpressionRowFiltration&lt;TInput&gt;</c> path.
/// Mirrors the row shape that the real ChangeRatio package operates on.
/// </summary>
public sealed class ChangeRatioRow
{
    public int AdminReserveRatio { get; set; }
    public int AdminReserveRatioPrevious { get; set; }
    public decimal AuthLimit { get; set; }
    public decimal Reserve { get; set; }
    public string Type { get; set; } = "Day";
}
