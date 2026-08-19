using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace EtlKit.Primitives
{
    /// <summary>
    /// The target side of a data flow link: any component that can receive rows from an
    /// <see cref="IDataFlowLinkSource{TOutput}"/>.
    /// </summary>
    /// <typeparam name="TInput">Type of the rows accepted by this target.</typeparam>
    public interface IDataFlowLinkTarget<in TInput> : ITask
    {
        /// <summary>
        /// The TPL Dataflow block that receives rows linked into this component.
        /// </summary>
        ITargetBlock<TInput> TargetBlock { get; }

        /// <summary>
        /// Registers a predecessor's completion task. <see cref="IDataFlowLinkSource{TOutput}.LinkTo"/>
        /// calls this automatically for every source linked to this target, so that when a target has
        /// several predecessors (fan-in), it only completes once all of them have finished.
        /// </summary>
        /// <param name="completion">The completion task of a linked predecessor component.</param>
        void AddPredecessorCompletion(Task completion);
    }
}
