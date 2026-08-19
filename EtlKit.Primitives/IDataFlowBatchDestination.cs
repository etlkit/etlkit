namespace EtlKit.Primitives
{
    /// <summary>
    /// A destination that buffers incoming rows and writes them in batches rather than one at a time.
    /// </summary>
    /// <typeparam name="TInput">Type of the rows accepted by this destination.</typeparam>
    public interface IDataFlowBatchDestination<in TInput> : IDataFlowDestination<TInput>
    {
        /// <summary>
        /// Number of rows collected before a batch is written to the destination.
        /// </summary>
        int BatchSize { get; set; }
    }
}
