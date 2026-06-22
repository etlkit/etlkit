namespace EtlKit.Primitives
{
    public interface IDataFlowTransformation<in TInput, out TOutput>
        : IDataFlowLinkSource<TOutput>,
            IDataFlowLinkTarget<TInput>
    { }
}
