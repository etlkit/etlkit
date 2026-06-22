namespace EtlKit.DataFlow
{
    /// <summary>
    /// The type info for an aggregation type. This type info is used to map the input data to the output data.
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    public interface IAggregationTypeInfo<in TInput, in TOutput> : IMappingTypeInfo<TInput, TOutput>
    {
        /// <summary>
        /// Read value from output object to use it for the next aggregation step
        /// </summary>
        /// <param name="outputRow"></param>
        /// <param name="attributeMapping"></param>
        /// <returns></returns>
        object GetOutputValueOrNull(TOutput outputRow, AggregateAttributeMapping attributeMapping);

        /// <summary>
        /// List of columns to aggregate
        /// </summary>
        IList<AggregateAttributeMapping> AggregateColumns { get; }

        /// <summary>
        /// List of columns to group by
        /// </summary>
        IList<AttributeMappingInfo> GroupColumns { get; }
    }
}
