using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using EtlKit.Primitives;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace EtlKit.Common.DataFlow
{
    /// <summary>
    /// Base class for destinations that buffer incoming rows and write them in batches. Derived
    /// classes implement <see cref="PrepareWrite"/>, <see cref="TryBulkInsertData"/>, and <see
    /// cref="FinishWrite"/> to define how a batch is actually persisted.
    /// </summary>
    /// <typeparam name="TInput">Type of the rows accepted by this destination.</typeparam>
    [PublicAPI]
    public abstract class DataFlowBatchDestination<TInput>
        : DataFlowDestination<TInput[]>,
            IDataFlowBatchDestination<TInput>
    {
        /// <summary>
        /// Creates a new instance with no logger.
        /// </summary>
        protected DataFlowBatchDestination() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        protected DataFlowBatchDestination(ILogger logger)
            : base(logger) { }

        /// <summary>
        /// This function is called every time before a batch is inserted into the destination.
        /// It receives an array that represents the batch - you can modify the data itself if needed.
        /// </summary>
        public Func<TInput[], TInput[]> BeforeBatchWrite { get; set; }

        /// <summary>
        /// This action is called after a batch was successfully inserted into the destination.
        /// You will get a copy of the data that was used for the insertion.
        /// </summary>
        public Action<TInput[]> AfterBatchWrite { get; set; }

        /// <summary>
        /// The buffer component used as target for linking.
        /// </summary>
        public new ITargetBlock<TInput> TargetBlock => Buffer;

        /// <summary>
        /// The batch size defines how many records needs to be in the Input buffer before data is written into the destination.
        /// The default batch size is 1000.
        /// </summary>
        public int BatchSize
        {
            get => _batchSize ?? DefaultBatchSize;
            set
            {
                _batchSize = value > 0 ? value : (int?)null;
                InitObjects(_batchSize ?? DefaultBatchSize);
            }
        }
        private int? _batchSize;

        /// <summary>
        /// The batch size used when <see cref="BatchSize"/> has not been set to a positive value.
        /// </summary>
        public const int DefaultBatchSize = 1000;

        /// <summary>
        /// Gets or sets the maximum number of messages that may be buffered by the block.
        /// If not set, `3 * <see cref="BatchSize"/>` is assumed.
        /// </summary>
        public int BoundedCapacity
        {
            get => _boundedCapacity ?? BatchSize * 3;
            set
            {
                _boundedCapacity = value;
                InitObjects(BatchSize);
            }
        }

        private int? _boundedCapacity;

        /// <summary>
        /// Whether <see cref="PrepareWrite"/> has already run for the first batch.
        /// </summary>
        protected bool WasInitialized { get; set; }

        /// <summary>
        /// Use this method if you want to register a task that needs to be completed
        /// before the destination itself can complete. Normally you don't have to do anything -
        /// all linked components will automatically register using this method.
        /// Simple use the LinkTo() method of source components or transformations.
        /// </summary>
        /// <param name="completion">A task to wait for before this destination can complete.</param>
        public new void AddPredecessorCompletion(Task completion)
        {
            PredecessorCompletions.Add(completion);
            completion.ContinueWith(_ => CheckCompleteAction());
        }

        /// <summary>
        /// Completes the batch <see cref="TargetBlock"/> once every task in <see
        /// cref="DataFlowDestination{TInput}.PredecessorCompletions"/> has finished, faulting it
        /// instead if any predecessor faulted. Shadows the base implementation because <see
        /// cref="TargetBlock"/> here is the batch-typed block, not the base class's.
        /// </summary>
        protected new void CheckCompleteAction()
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
        /// Accumulates incoming rows until <see cref="BatchSize"/> is reached, then forwards the full
        /// batch to <see cref="DataFlowDestination{TInput}.TargetAction"/> for writing.
        /// </summary>
        protected BatchBlock<TInput> Buffer { get; set; }

        /// <summary>
        /// (Re-)creates <see cref="Buffer"/> and <see cref="DataFlowDestination{TInput}.TargetAction"/>
        /// with the given batch size and current <see cref="BoundedCapacity"/>, and links them
        /// together. Called whenever <see cref="BatchSize"/> or <see cref="BoundedCapacity"/> is set.
        /// </summary>
        /// <param name="initBatchSize">Batch size to configure <see cref="Buffer"/> with.</param>
        protected virtual void InitObjects(int initBatchSize)
        {
            var options = new GroupingDataflowBlockOptions { BoundedCapacity = BoundedCapacity };
            Buffer = new BatchBlock<TInput>(initBatchSize, options);
            TargetAction = new ActionBlock<TInput[]>(WriteBatch);
            SetCompletionTask();
            Buffer.LinkTo(TargetAction, new DataflowLinkOptions { PropagateCompletion = true });
        }

        /// <summary>
        /// Writes one batch: runs <see cref="BeforeBatchWrite"/>, lazily calls <see cref="PrepareWrite"/>
        /// on the first batch, persists the data via <see cref="TryBulkInsertData"/>, logs progress,
        /// then runs <see cref="AfterBatchWrite"/>.
        /// </summary>
        /// <param name="data">The accumulated batch of rows.</param>
        protected void WriteBatch(TInput[] data)
        {
            if (ProgressCount == 0)
                LogStart();
            if (BeforeBatchWrite != null)
                data = BeforeBatchWrite.Invoke(data);
            if (!WasInitialized)
            {
                PrepareWrite();
                WasInitialized = true;
            }
            TryBulkInsertData(data);
            LogProgressBatch(data.Length);
            AfterBatchWrite?.Invoke(data);
        }

        /// <summary>
        /// Runs <see cref="FinishWrite"/> before the base cleanup (invoking <see
        /// cref="DataFlowDestination{TInput}.OnCompletion"/> and logging), so the destination gets a
        /// chance to flush or close resources first.
        /// </summary>
        protected override void CleanUp()
        {
            FinishWrite();
            base.CleanUp();
        }

        /// <summary>
        /// Called once, lazily, before the first batch is written — e.g. to open a connection or
        /// prepare the destination for bulk insertion.
        /// </summary>
        protected abstract void PrepareWrite();

        /// <summary>
        /// Persists one batch of rows to the destination.
        /// </summary>
        /// <param name="data">The batch to write.</param>
        protected abstract void TryBulkInsertData(TInput[] data);

        /// <summary>
        /// Called once, after the last batch has been written, to perform any needed teardown.
        /// </summary>
        protected abstract void FinishWrite();
    }
}
