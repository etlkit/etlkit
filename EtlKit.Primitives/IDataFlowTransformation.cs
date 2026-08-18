namespace EtlKit.Primitives
{
    /// <summary>
    /// A component that sits between a source and a destination: it accepts rows as a link target and
    /// forwards (possibly transformed) rows as a link source. Declares no members of its own — it only
    /// combines <see cref="IDataFlowLinkTarget{TInput}"/> and <see cref="IDataFlowLinkSource{TOutput}"/>.
    /// </summary>
    /// <typeparam name="TInput">Type of the rows accepted from upstream components.</typeparam>
    /// <typeparam name="TOutput">Type of the rows forwarded to downstream components.</typeparam>
    public interface IDataFlowTransformation<in TInput, out TOutput>
        : IDataFlowLinkSource<TOutput>,
            IDataFlowLinkTarget<TInput> { }
}
