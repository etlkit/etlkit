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
        /// Starts producing rows and returns a task that completes once all rows have been sent to
        /// their linked targets.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel the read/generation loop.</param>
        Task ExecuteAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Starts producing rows and blocks the calling thread until all rows have been sent to their
        /// linked targets.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel the read/generation loop.</param>
        void Execute(CancellationToken cancellationToken);
    }
}
