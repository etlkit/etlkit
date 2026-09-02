using System.Threading;
using System.Threading.Tasks;

namespace EtlKit.Primitives
{
    /// <summary>
    /// The starting component of a data flow: reads or generates rows and pushes them into the linked
    /// targets. Must be linked to at least one target via <see cref="IDataFlowLinkSource{TOutput}.LinkTo"/>
    /// before execution.
    /// </summary>
    /// <typeparam name="TOutput">Type of the rows produced by this source.</typeparam>
    public interface IDataFlowSource<out TOutput> : IDataFlowLinkSource<TOutput>, ILinkErrorSource
    {
        /// <summary>
        /// Starts producing rows and returns a task that completes once all rows have been posted to
        /// this source's output buffer and the buffer is marked complete. Linked targets may still be
        /// processing at that point — await the destination's completion (e.g. its
        /// <c>Completion</c> task or <c>Wait()</c>) to know the whole pipeline has finished.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel the read/generation loop.</param>
        Task ExecuteAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Starts producing rows and blocks the calling thread until all rows have been posted to
        /// this source's output buffer and the buffer is marked complete. Linked targets may still be
        /// processing at that point — await the destination's completion (e.g. its
        /// <c>Completion</c> task or <c>Wait()</c>) to know the whole pipeline has finished.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel the read/generation loop.</param>
        void Execute(CancellationToken cancellationToken);
    }
}
