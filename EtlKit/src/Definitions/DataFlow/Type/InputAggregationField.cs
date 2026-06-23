namespace EtlKit.DataFlow;

/// <summary>
/// Mapping configuration for a single <see cref="ExpandoObject"/> field.
/// This configuration is to be serialized.
/// </summary>
public class InputAggregationField
{
    /// <summary>
    /// Input row property name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Aggregation method to use for this property.
    /// </summary>
    public InputAggregationMethod AggregationMethod { get; set; }

    public enum InputAggregationMethod
    {
        Sum,
        Min,
        Max,
        Count,
        GroupBy
    }
}
