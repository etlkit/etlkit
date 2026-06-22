using System;
using System.Threading.Tasks.Dataflow;

namespace EtlKit.Primitives
{
    public interface IDataFlowLinkSource<out TOutput>
    {
        ISourceBlock<TOutput> SourceBlock { get; }
        IDataFlowLinkSource<TOutput> LinkTo(IDataFlowLinkTarget<TOutput> target);
        IDataFlowLinkSource<TOutput> LinkTo(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> predicate
        );
        IDataFlowLinkSource<TOutput> LinkTo(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> rowsToKeep,
            Predicate<TOutput> rowsIntoVoid
        );

        IDataFlowLinkSource<TConvert> LinkTo<TConvert>(IDataFlowLinkTarget<TOutput> target);
        IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> predicate
        );
        IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> rowsToKeep,
            Predicate<TOutput> rowsIntoVoid
        );
    }
}
