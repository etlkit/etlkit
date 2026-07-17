namespace EtlKit.DataFlow
{
    /// <summary>
    /// Selects whether a stream source/destination (see <see cref="DataFlowStreamSource{TOutput}"/>,
    /// <see cref="DataFlowStreamDestination{TInput}"/>) reads/writes a local file or an HTTP endpoint.
    /// </summary>
    public enum ResourceType
    {
        /// <summary>
        /// No resource type set; behaves like <see cref="Http"/>.
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
