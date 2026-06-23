using System.Threading;
using System.Threading.Tasks;

namespace EtlKit.Primitives
{
    public interface IDataFlowSource<out TOutput> : IDataFlowLinkSource<TOutput>, ILinkErrorSource
    {
        Task ExecuteAsync(CancellationToken cancellationToken);

        void Execute(CancellationToken cancellationToken);
    }
}
