using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using EtlKit.Primitives;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace EtlKit.Common.DataFlow
{
    /// <summary>
    /// Base class for all data flow sources. Implements the <see cref="IDataFlowLinkSource{TOutput}"/>
    /// linking machinery and buffers produced rows through a <see cref="BufferBlock{TOutput}"/>;
    /// derived classes only need to implement <see cref="Execute(CancellationToken)"/> to post rows
    /// into <see cref="Buffer"/>.
    /// </summary>
    /// <typeparam name="TOutput">Type of the rows produced by this source.</typeparam>
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

        /// <inheritdoc />
        public ISourceBlock<TOutput> SourceBlock => Buffer;

        /// <summary>
        /// The buffer that produced rows are posted to; backs <see cref="SourceBlock"/>. Derived
        /// classes post rows here from their <see cref="Execute(CancellationToken)"/> implementation.
        /// </summary>
        protected BufferBlock<TOutput> Buffer { get; set; } = new();

        /// <summary>
        /// Routes error records to the target linked via <see cref="LinkErrorTo"/>.
        /// </summary>
        protected ErrorHandler ErrorHandler { get; set; } = new();

        /// <inheritdoc cref="IDataFlowSource{TOutput}.Execute" />
        public abstract void Execute(CancellationToken cancellationToken);

        /// <summary>
        /// Starts producing rows using <see cref="CancellationToken.None"/>.
        /// </summary>
        public void Execute() => Execute(CancellationToken.None);

        /// <inheritdoc cref="IDataFlowSource{TOutput}.ExecuteAsync" />
        /// <remarks>
        /// Runs <see cref="Execute(CancellationToken)"/> on a thread pool thread via <see
        /// cref="Task.Factory"/>; <see cref="Execute(CancellationToken)"/> itself does not need to be
        /// asynchronous.
        /// </remarks>
        public Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            return Task.Factory.StartNew(() => Execute(cancellationToken), cancellationToken);
        }

        /// <inheritdoc />
        public IDataFlowLinkSource<TOutput> LinkTo(IDataFlowLinkTarget<TOutput> target) =>
            new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo(target);

        /// <inheritdoc />
        public IDataFlowLinkSource<TOutput> LinkTo(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> predicate
        ) => new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo(target, predicate);

        /// <inheritdoc />
        public IDataFlowLinkSource<TOutput> LinkTo(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> rowsToKeep,
            Predicate<TOutput> rowsIntoVoid
        ) =>
            new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo(target, rowsToKeep, rowsIntoVoid);

        /// <inheritdoc />
        public IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target
        ) => new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo<TConvert>(target);

        /// <inheritdoc />
        public IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> predicate
        ) => new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo<TConvert>(target, predicate);

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void LinkErrorTo(IDataFlowLinkTarget<EtlKitError> target) =>
            ErrorHandler.LinkErrorTo(target, SourceBlock.Completion);
    }
}
