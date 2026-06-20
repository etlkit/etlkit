namespace EtlKit.Primitives
{
    public interface ILinkErrorSource
    {
        void LinkErrorTo(IDataFlowLinkTarget<EtlKitError> target);
    }
}
