#nullable enable

using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace EtlKit.Common.TaskUtilities
{
    /// <summary>
    /// <see cref="TransformBlock&lt;TInput, TOutput&gt;"/> with <see cref="OnComplete"/> handler.
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    internal class TransformBlockWithCompletion<TInput, TOutput> : IPropagatorBlock<TInput, TOutput>
    {
        /// <summary>
        /// Inner block to wrap and proxy
        /// </summary>
        private readonly IPropagatorBlock<TInput, TOutput> _innerTransformBlock;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="transform"></param>
        public TransformBlockWithCompletion(Func<TInput, TOutput> transform)
        {
            _innerTransformBlock = new TransformBlock<TInput, TOutput>(transform);
            Completion = _innerTransformBlock.Completion.ContinueWith(t =>
            {
                OnComplete?.Invoke(t);
                if (t.Status == TaskStatus.Faulted)
                {
                    if (t.Exception != null)
                        throw t.Exception;
                    throw new InvalidOperationException("Transform block faulted");
                }
            });
        }

        /// <summary>
        /// Event handler that is invoked after internal <see cref="Completion"/> task is marked as Complete.
        /// </summary>
        public Action<Task>? OnComplete { get; set; }

        #region IPropagatorBlock<TInput, TOutput> members

        public DataflowMessageStatus OfferMessage(
            DataflowMessageHeader messageHeader,
            TInput messageValue,
            ISourceBlock<TInput>? source,
            bool consumeToAccept
        ) =>
            _innerTransformBlock.OfferMessage(messageHeader, messageValue, source, consumeToAccept);

        public void Complete()
        {
            _innerTransformBlock.Complete();
        }

        public void Fault(Exception exception) => _innerTransformBlock.Fault(exception);

        /// <summary>
        /// Aggregate completion task
        /// </summary>
        public Task Completion { get; }

        public IDisposable LinkTo(ITargetBlock<TOutput> target, DataflowLinkOptions linkOptions) =>
            _innerTransformBlock.LinkTo(target, linkOptions);

        public TOutput? ConsumeMessage(
            DataflowMessageHeader messageHeader,
            ITargetBlock<TOutput> target,
            out bool messageConsumed
        ) => _innerTransformBlock.ConsumeMessage(messageHeader, target, out messageConsumed);

        public bool ReserveMessage(
            DataflowMessageHeader messageHeader,
            ITargetBlock<TOutput> target
        ) => _innerTransformBlock.ReserveMessage(messageHeader, target);

        public void ReleaseReservation(
            DataflowMessageHeader messageHeader,
            ITargetBlock<TOutput> target
        ) => _innerTransformBlock.ReleaseReservation(messageHeader, target);

        #endregion
    }
}
