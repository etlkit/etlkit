using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using EtlKit.Primitives;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace EtlKit.Common.DataFlow
{
    /// <summary>
    /// Base class for all data flow transformations. Implements the linking and completion-tracking
    /// machinery for both the <see cref="IDataFlowLinkTarget{TInput}"/> and <see
    /// cref="IDataFlowLinkSource{TOutput}"/> sides; derived classes create <see cref="TransformBlock"/>.
    /// </summary>
    /// <typeparam name="TInput">Type of the rows accepted from upstream components.</typeparam>
    /// <typeparam name="TOutput">Type of the rows forwarded to downstream components.</typeparam>
    [PublicAPI]
    public abstract class DataFlowTransformation<TInput, TOutput>
        : DataFlowTask,
            IDataFlowTransformation<TInput, TOutput>,
            ILinkErrorSource
    {
        /// <summary>
        /// Creates a new instance with no logger.
        /// </summary>
        protected DataFlowTransformation() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        protected DataFlowTransformation(ILogger logger)
            : base(logger) { }

        /// <summary>
        /// Target for the previous component in the data flow.
        /// </summary>
        public virtual ITargetBlock<TInput> TargetBlock { get; }

        /// <summary>
        /// Source for the next component in the data flow.
        /// </summary>
        public virtual ISourceBlock<TOutput> SourceBlock { get; }

        /// <summary>
        /// List of completion Tasks from all preceding components.
        /// </summary>
        protected List<Task> PredecessorCompletions { get; set; } = new();

        /// <summary>
        /// Transformation block component
        /// </summary>
        protected IPropagatorBlock<TInput, TOutput> TransformBlock { get; set; }

        /// <summary>
        /// Error handler
        /// </summary>
        protected ErrorHandler ErrorHandler { get; set; } = new();

        /// <summary>
        /// Link to error target block
        /// </summary>
        /// <param name="target">The component that will receive error records.</param>
        public virtual void LinkErrorTo(IDataFlowLinkTarget<EtlKitError> target) =>
            ErrorHandler.LinkErrorTo(target, TransformBlock.Completion);

        /// <inheritdoc />
        public void AddPredecessorCompletion(Task completion)
        {
            PredecessorCompletions.Add(completion);
            completion.ContinueWith(_ => CheckCompleteAction());
        }

        /// <summary>
        /// Completes <see cref="TargetBlock"/> once every task in <see cref="PredecessorCompletions"/>
        /// has finished, faulting it instead if any predecessor faulted.
        /// </summary>
        protected void CheckCompleteAction()
        {
            Task.WhenAll(PredecessorCompletions)
                .ContinueWith(t =>
                {
                    if (!TargetBlock.Completion.IsCompleted)
                        if (t.IsFaulted)
                            TargetBlock.Fault(t.Exception!.InnerException!);
                        else
                            TargetBlock.Complete();
                });
        }

        /// <inheritdoc />
        public virtual IDataFlowLinkSource<TOutput> LinkTo(IDataFlowLinkTarget<TOutput> target) =>
            new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo(target);

        /// <inheritdoc />
        public virtual IDataFlowLinkSource<TOutput> LinkTo(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> predicate
        ) => new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo(target, predicate);

        /// <inheritdoc />
        public virtual IDataFlowLinkSource<TOutput> LinkTo(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> rowsToKeep,
            Predicate<TOutput> rowsIntoVoid
        ) =>
            new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo(target, rowsToKeep, rowsIntoVoid);

        /// <inheritdoc />
        public virtual IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target
        ) => new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo<TConvert>(target);

        /// <inheritdoc />
        public virtual IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> predicate
        ) => new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo<TConvert>(target, predicate);

        /// <inheritdoc />
        public virtual IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> rowsToKeep,
            Predicate<TOutput> rowsIntoVoid
        ) =>
            new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo<TConvert>(
                target,
                rowsToKeep,
                rowsIntoVoid
            );
    }
}
