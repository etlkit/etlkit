using System.Threading.Tasks;

namespace EtlKit.Primitives
{
    /// <summary>
    /// The terminal component of a data flow: a target that does not forward rows further and can be
    /// waited on for completion.
    /// </summary>
    /// <typeparam name="TInput">Type of the rows accepted by this destination.</typeparam>
    public interface IDataFlowDestination<in TInput> : IDataFlowLinkTarget<TInput>
    {
        /// <summary>
        /// Blocks the calling thread until <see cref="Completion"/> finishes.
        /// </summary>
        void Wait();

        /// <summary>
        /// Completes once this destination and all of its linked predecessors have finished processing.
        /// </summary>
        Task Completion { get; }
    }
}
