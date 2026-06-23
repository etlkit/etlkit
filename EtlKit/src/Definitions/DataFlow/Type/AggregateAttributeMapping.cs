namespace EtlKit.DataFlow
{
    public sealed record AggregateAttributeMapping : AttributeMappingInfo
    {
        internal AggregationMethod AggregationMethod { get; set; }
    }
}
