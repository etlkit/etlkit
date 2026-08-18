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
    /// Base class for all data flow destinations. Implements the <see
    /// cref="IDataFlowLinkTarget{TInput}"/> completion-tracking machinery; derived classes create
    /// <see cref="TargetAction"/> and call <see cref="SetCompletionTask"/> once it is ready.
    /// </summary>
    /// <typeparam name="TInput">Type of the rows accepted by this destination.</typeparam>
    [PublicAPI]
    public abstract class DataFlowDestination<TInput>
        : DataFlowTask,
            IDataFlowDestination<TInput>,
            ILinkErrorSource
    {
        /// <summary>
        /// Creates a new instance with no logger.
        /// </summary>
        protected DataFlowDestination() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        protected DataFlowDestination(ILogger logger)
            : base(logger) { }

        /// <summary>
        /// Optional callback invoked during <see cref="CleanUp"/>, once processing completes
        /// (successfully or not) and before the finish log entry is written.
        /// </summary>
        public Action OnCompletion { get; set; }

        /// <inheritdoc />
        public Task Completion { get; protected set; }

        /// <inheritdoc />
        public ITargetBlock<TInput> TargetBlock => TargetAction;

        /// <inheritdoc />
        public virtual void Wait() => Completion.Wait();

        /// <summary>
        /// The block that receives and processes rows; backs <see cref="TargetBlock"/>. Created by
        /// derived classes.
        /// </summary>
        protected ActionBlock<TInput> TargetAction { get; set; }

        /// <summary>
        /// Completion tasks registered via <see cref="AddPredecessorCompletion"/>; all of them must
        /// finish before <see cref="TargetBlock"/> is allowed to complete.
        /// </summary>
        protected List<Task> PredecessorCompletions { get; set; } = new();

        /// <summary>
        /// Routes error records to the target linked via <see cref="LinkErrorTo"/>.
        /// </summary>
        protected ErrorHandler ErrorHandler { get; set; } = new();

        /// <inheritdoc />
        public void AddPredecessorCompletion(Task completion)
        {
            PredecessorCompletions.Add(completion);
            completion.ContinueWith(_ => CheckCompleteAction());
        }

        /// <inheritdoc />
        public void LinkErrorTo(IDataFlowLinkTarget<EtlKitError> target) =>
            ErrorHandler.LinkErrorTo(target, TargetAction.Completion);

        /// <summary>
        /// Completes <see cref="TargetBlock"/> once every task in <see cref="PredecessorCompletions"/>
        /// has finished, faulting it instead if any predecessor faulted.
        /// </summary>
        protected void CheckCompleteAction()
        {
            Task.WhenAll(PredecessorCompletions)
                .ContinueWith(t =>
                {
                    if (TargetBlock.Completion.IsCompleted)
                    {
                        return;
                    }

                    if (t.IsFaulted)
                        TargetBlock.Fault(t.Exception!.InnerException!);
                    else
                        TargetBlock.Complete();
                });
        }

        /// <summary>
        /// Initializes <see cref="Completion"/> by starting <see cref="AwaitCompletion"/>. Derived
        /// classes must call this once <see cref="TargetAction"/> has been created.
        /// </summary>
        protected void SetCompletionTask() => Completion = AwaitCompletion();

        /// <summary>
        /// Awaits <see cref="TargetAction"/>'s completion, unwrapping any <see
        /// cref="AggregateException"/> to rethrow its inner exception, then always runs <see
        /// cref="CleanUp"/>.
        /// </summary>
        protected virtual async Task AwaitCompletion()
        {
            try
            {
                await TargetAction.Completion.ConfigureAwait(false);
            }
            catch (AggregateException aggregateException)
            {
                throw aggregateException.InnerException!;
            }
            finally
            {
                CleanUp();
            }
        }

        /// <summary>
        /// Invokes <see cref="OnCompletion"/> and writes the finish log entry. Called once, when
        /// processing completes successfully or not.
        /// </summary>
        protected virtual void CleanUp()
        {
            OnCompletion?.Invoke();
            LogFinish();
        }
    }
}
