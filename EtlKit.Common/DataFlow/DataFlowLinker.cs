using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using JetBrains.Annotations;
using System.Threading.Tasks.Dataflow;
using EtlKit.Primitives;
using EtlKit.Common.ControlFlow;

namespace EtlKit.Common.DataFlow
{
    [SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem")]
    [PublicAPI]
    public class DataFlowLinker<TOutput>
    {
        public ISourceBlock<TOutput> SourceBlock { get; set; }
        public bool DisableLogging => CallingTask.DisableLogging;
        public ILogger Logger => ControlFlow.ControlFlow.LoggerFactory.CreateLogger<DataFlowLinker<TOutput>>();
        public DataFlowTask CallingTask { get; set; }

        public DataFlowLinker(DataFlowTask callingTask, ISourceBlock<TOutput> sourceBlock)
        {
            CallingTask = callingTask;
            SourceBlock = sourceBlock;
        }

        public IDataFlowLinkSource<TOutput> LinkTo(IDataFlowLinkTarget<TOutput> target) =>
            LinkTo<TOutput>(target);

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

        public IDataFlowLinkSource<TOutput> LinkTo(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> predicate
        ) => LinkTo<TOutput>(target, predicate);

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

        public IDataFlowLinkSource<TOutput> LinkTo(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> rowsToKeep,
            Predicate<TOutput> rowsIntoVoid
        ) => LinkTo<TOutput>(target, rowsToKeep, rowsIntoVoid);

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
