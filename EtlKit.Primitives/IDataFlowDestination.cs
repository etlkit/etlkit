using System.Threading.Tasks;

namespace EtlKit.Primitives
{
    public interface IDataFlowDestination<in TInput> : IDataFlowLinkTarget<TInput>
    {
        void Wait();
        Task Completion { get; }
    }
}
