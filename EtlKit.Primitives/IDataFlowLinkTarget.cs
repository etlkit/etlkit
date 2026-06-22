using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace EtlKit.Primitives
{
    public interface IDataFlowLinkTarget<in TInput> : ITask
    {
        ITargetBlock<TInput> TargetBlock { get; }
        void AddPredecessorCompletion(Task completion);
    }
}
