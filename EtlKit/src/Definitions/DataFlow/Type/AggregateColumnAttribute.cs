namespace EtlKit.DataFlow
{
    /// <summary>
    /// This attribute is used to identify the aggregation property for aggregations. The passed column name
    /// identifies the property in the aggregation output object.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class AggregateColumnAttribute : Attribute
    {
        public string AggregationProperty { get; set; }
        public AggregationMethod AggregationMethod { get; set; }

        public AggregateColumnAttribute(
            string aggregationProperty,
            AggregationMethod aggregationMethod
        )
        {
            AggregationProperty = aggregationProperty;
            AggregationMethod = aggregationMethod;
        }
    }
}
