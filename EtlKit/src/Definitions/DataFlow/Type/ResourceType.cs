namespace EtlKit.DataFlow
{
    /// <summary>
    /// Selects whether a stream source/destination (see <see cref="DataFlowStreamSource{TOutput}"/>,
    /// <see cref="DataFlowStreamDestination{TInput}"/>) reads/writes a local file or an HTTP endpoint.
    /// </summary>
    public enum ResourceType
    {
        /// <summary>
        /// No resource type set. Streams are opened as for <see cref="Http"/>, but
        /// <see cref="DataFlowStreamDestination{TInput}"/> finalizes the HTTP upload (completing the
        /// request and checking the response status) only when <see cref="Http"/> is set explicitly —
        /// always set <see cref="Http"/> when writing to an HTTP endpoint.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// The resource is accessed over HTTP.
        /// </summary>
        Http = 1,

        /// <summary>
        /// The resource is a local file.
        /// </summary>
        File = 2,
    }
}
