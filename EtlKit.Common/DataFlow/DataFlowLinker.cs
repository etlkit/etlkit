using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks.Dataflow;
using EtlKit.Common.ControlFlow;
using EtlKit.Primitives;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace EtlKit.Common.DataFlow
{
    /// <summary>
    /// Implements the actual TPL Dataflow linking logic behind every <c>LinkTo</c> overload on <see
    /// cref="DataFlowSource{TOutput}"/> and <see cref="DataFlowTransformation{TInput,TOutput}"/>: those
    /// classes construct a <see cref="DataFlowLinker{TOutput}"/> and delegate to it rather than
    /// duplicating the linking code.
    /// </summary>
    /// <typeparam name="TOutput">Type of the rows produced by the linking source.</typeparam>
    [SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem")]
    [PublicAPI]
    public class DataFlowLinker<TOutput>
    {
        /// <summary>
        /// The block that produces rows to be linked to a target.
        /// </summary>
        public ISourceBlock<TOutput> SourceBlock { get; set; }

        /// <inheritdoc cref="EtlKit.Primitives.ITask.DisableLogging" />
        public bool DisableLogging => CallingTask.DisableLogging;

        /// <summary>
        /// Logger used to record linking activity, created from the shared <see
        /// cref="ControlFlow.ControlFlow.LoggerFactory"/>.
        /// </summary>
        public ILogger Logger =>
            ControlFlow.ControlFlow.LoggerFactory.CreateLogger<DataFlowLinker<TOutput>>();

        /// <summary>
        /// The component performing the link, used for logging context (name, type, hash).
        /// </summary>
        public DataFlowTask CallingTask { get; set; }

        /// <summary>
        /// Creates a linker for <paramref name="sourceBlock"/> on behalf of <paramref name="callingTask"/>.
        /// </summary>
        /// <param name="callingTask">The component performing the link, used for logging context.</param>
        /// <param name="sourceBlock">The block that produces rows to be linked.</param>
        public DataFlowLinker(DataFlowTask callingTask, ISourceBlock<TOutput> sourceBlock)
        {
            CallingTask = callingTask;
            SourceBlock = sourceBlock;
        }

        /// <inheritdoc cref="IDataFlowLinkSource{TOutput}.LinkTo(IDataFlowLinkTarget{TOutput})" />
        public IDataFlowLinkSource<TOutput> LinkTo(IDataFlowLinkTarget<TOutput> target) =>
            LinkTo<TOutput>(target);

        /// <inheritdoc cref="IDataFlowLinkSource{TOutput}.LinkTo{TConvert}(IDataFlowLinkTarget{TOutput})" />
        public IDataFlowLinkSource<TConvert> LinkTo<TConvert>(IDataFlowLinkTarget<TOutput> target)
        {
            SourceBlock.LinkTo(target.TargetBlock);
            target.AddPredecessorCompletion(SourceBlock.Completion);
            if (!DisableLogging)
                Logger.Debug(
                    CallingTask.TaskName + $" was linked to: {target.TaskName}",
                    CallingTask.TaskType,
                    "LOG",
                    CallingTask.TaskHash,
                    ControlFlow.ControlFlow.Stage,
                    ControlFlow.ControlFlow.CurrentLoadProcess?.Id
                );
            return target as IDataFlowLinkSource<TConvert>;
        }

        /// <inheritdoc cref="IDataFlowLinkSource{TOutput}.LinkTo(IDataFlowLinkTarget{TOutput}, Predicate{TOutput})" />
        public IDataFlowLinkSource<TOutput> LinkTo(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> predicate
        ) => LinkTo<TOutput>(target, predicate);

        /// <inheritdoc cref="IDataFlowLinkSource{TOutput}.LinkTo{TConvert}(IDataFlowLinkTarget{TOutput}, Predicate{TOutput})" />
        public IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> predicate
        )
        {
            SourceBlock.LinkTo(target.TargetBlock, predicate);
            target.AddPredecessorCompletion(SourceBlock.Completion);
            if (!DisableLogging)
                Logger.Debug(
                    CallingTask.TaskName + $" was linked to (with predicate): {target.TaskName}!",
                    CallingTask.TaskType,
                    "LOG",
                    CallingTask.TaskHash,
                    ControlFlow.ControlFlow.Stage,
                    ControlFlow.ControlFlow.CurrentLoadProcess?.Id
                );
            return target as IDataFlowLinkSource<TConvert>;
        }

        /// <inheritdoc cref="IDataFlowLinkSource{TOutput}.LinkTo(IDataFlowLinkTarget{TOutput}, Predicate{TOutput}, Predicate{TOutput})" />
        public IDataFlowLinkSource<TOutput> LinkTo(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> rowsToKeep,
            Predicate<TOutput> rowsIntoVoid
        ) => LinkTo<TOutput>(target, rowsToKeep, rowsIntoVoid);

        /// <inheritdoc cref="IDataFlowLinkSource{TOutput}.LinkTo{TConvert}(IDataFlowLinkTarget{TOutput}, Predicate{TOutput}, Predicate{TOutput})" />
        public IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> rowsToKeep,
            Predicate<TOutput> rowsIntoVoid
        )
        {
            SourceBlock.LinkTo(target.TargetBlock, rowsToKeep);
            target.AddPredecessorCompletion(SourceBlock.Completion);
            if (!DisableLogging)
                Logger.Debug(
                    CallingTask.TaskName + $" was linked to (with predicate): {target.TaskName}!",
                    CallingTask.TaskType,
                    "LOG",
                    CallingTask.TaskHash,
                    ControlFlow.ControlFlow.Stage,
                    ControlFlow.ControlFlow.CurrentLoadProcess?.Id
                );

            var voidTarget = new VoidDestination<TOutput>();
            SourceBlock.LinkTo(voidTarget.TargetBlock, rowsIntoVoid);
            voidTarget.AddPredecessorCompletion(SourceBlock.Completion);
            if (!DisableLogging)
                Logger.Debug(
                    CallingTask.TaskName
                        + " was also linked to: VoidDestination to ignore certain rows!",
                    CallingTask.TaskType,
                    "LOG",
                    CallingTask.TaskHash,
                    ControlFlow.ControlFlow.Stage,
                    ControlFlow.ControlFlow.CurrentLoadProcess?.Id
                );

            return target as IDataFlowLinkSource<TConvert>;
        }
    }
}
