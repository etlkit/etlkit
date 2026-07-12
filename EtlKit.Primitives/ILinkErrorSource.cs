namespace EtlKit.Primitives
{
    /// <summary>
    /// Implemented by data flow sources that can route their processing errors to a dedicated target,
    /// separate from the normal output.
    /// </summary>
    public interface ILinkErrorSource
    {
        /// <summary>
        /// Links this component's error output to <paramref name="target"/>, so <see cref="EtlKitError"/>
        /// records produced while processing rows are sent there instead of failing the flow.
        /// </summary>
        /// <param name="target">The component that will receive error records.</param>
        void LinkErrorTo(IDataFlowLinkTarget<EtlKitError> target);
    }
}
