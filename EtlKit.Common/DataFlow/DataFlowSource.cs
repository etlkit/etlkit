using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using EtlKit.Primitives;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace EtlKit.Common.DataFlow
{
    [PublicAPI]
    public abstract class DataFlowSource<TOutput> : DataFlowTask, ILinkErrorSource
    {
        /// <summary>
        /// Creates a new instance with no logger.
        /// </summary>
        protected DataFlowSource() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        protected DataFlowSource([CanBeNull] ILogger logger)
            : base(logger) { }

        public ISourceBlock<TOutput> SourceBlock => Buffer;
        protected BufferBlock<TOutput> Buffer { get; set; } = new();

        protected ErrorHandler ErrorHandler { get; set; } = new();

        public abstract void Execute(CancellationToken cancellationToken);

        public void Execute() => Execute(CancellationToken.None);

        public Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            return Task.Factory.StartNew(() => Execute(cancellationToken), cancellationToken);
        }

        public IDataFlowLinkSource<TOutput> LinkTo(IDataFlowLinkTarget<TOutput> target) =>
            new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo(target);

        public IDataFlowLinkSource<TOutput> LinkTo(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> predicate
        ) => new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo(target, predicate);

        public IDataFlowLinkSource<TOutput> LinkTo(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> rowsToKeep,
            Predicate<TOutput> rowsIntoVoid
        ) =>
            new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo(target, rowsToKeep, rowsIntoVoid);

        public IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target
        ) => new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo<TConvert>(target);

        public IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> predicate
        ) => new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo<TConvert>(target, predicate);

        public IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> rowsToKeep,
            Predicate<TOutput> rowsIntoVoid
        ) =>
            new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo<TConvert>(
                target,
                rowsToKeep,
                rowsIntoVoid
            );

        public void LinkErrorTo(IDataFlowLinkTarget<EtlKitError> target) =>
            ErrorHandler.LinkErrorTo(target, SourceBlock.Completion);
    }
}
