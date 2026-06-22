namespace EtlKit.DataFlow
{
    /// <summary>
    /// This attribute is used to identify the grouping property for aggregations. The passed column name
    /// identifies the property in the aggregation output object.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class GroupColumnAttribute : Attribute
    {
        public string AggregationGroupingProperty { get; set; }

        public GroupColumnAttribute(string aggregationGroupingProperty)
        {
            AggregationGroupingProperty = aggregationGroupingProperty;
        }
    }
}
