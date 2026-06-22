#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using EtlKit.Common.DataFlow.Streaming;

using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace EtlKit.Common.DataFlow
{
    /// <summary>
    /// Terminal destination that commits a streaming checkpoint position once records have flowed
    /// all the way through the pipeline.
    /// </summary>
    /// <remarks>
    /// Place it at the very end, after the real destination (typically modelled as a transformation
    /// that re-emits what it wrote). A record reaching the writer therefore implies it was already
    /// durably written upstream, so committing its position here — rather than at emit time in the
    /// source — yields at-least-once delivery: a crash between the destination write and the commit
    /// replays the record (a duplicate), never drops it. Commits advance strictly forward
    /// (<see cref="IComparable{T}"/>), so reordering can never move the checkpoint backwards.
    /// </remarks>
    [PublicAPI]
    public class CheckpointWriter<TInput, TPosition> : DataFlowDestination<TInput>
        where TPosition : IComparable<TPosition>
    {
        /* ITask */
        /// <inheritdoc/>
        public sealed override string TaskName { get; set; } = "Commit streaming checkpoint";

        /// <summary>Store the committed position is written to.</summary>
        public ICheckpointStore<TPosition> CheckpointStore { get; set; } = null!;

        /// <summary>
        /// Identifies this consumer's checkpoint. Must match the <c>CheckpointId</c> of the source
        /// that produced the stream (the source loads by it, this writer commits by it).
        /// </summary>
        public string CheckpointId { get; set; } = null!;

        /// <summary>
        /// Extracts the typed checkpoint position from a record (e.g. its <c>StreamPosition</c>).
        /// Setting it wires up the underlying block.
        /// </summary>
        public Func<TInput, TPosition> Position
        {
            get => _position;
            set
            {
                _position = value;
                InitObjects();
            }
        }

        /// <summary>
        /// Minimum interval between commits. <see cref="TimeSpan.Zero"/> (default) commits on every
        /// record. A positive value debounces: the highest position seen is committed at most once
        /// per interval (plus a final commit when the pipeline completes). Larger intervals cut
        /// store writes but widen the at-least-once replay window after a crash.
        /// </summary>
        public TimeSpan CommitInterval { get; set; } = TimeSpan.Zero;

        private Func<TInput, TPosition> _position = null!;
        private TPosition _maxSeen = default!;
        private bool _hasSeen;
        private TPosition _committed = default!;
        private bool _hasCommitted;
        private DateTime _lastCommitUtc = DateTime.MinValue;

        /// <summary>Creates a new instance with no logger.</summary>
        public CheckpointWriter() { }

        /// <summary>Creates a new instance with an injected logger.</summary>
        public CheckpointWriter(ILogger<CheckpointWriter<TInput, TPosition>> logger)
            : base(logger) { }

        private void InitObjects()
        {
            TargetAction = new ActionBlock<TInput>(OnReceiveAsync);
            SetCompletionTask();
        }

        private async Task OnReceiveAsync(TInput input)
        {
            if (ProgressCount == 0)
                LogStart();

            if (input is not null)
            {
                var position = _position(input);
                if (!_hasSeen || position.CompareTo(_maxSeen) > 0)
                {
                    _maxSeen = position;
                    _hasSeen = true;
                }

                if (
                    CommitInterval == TimeSpan.Zero
                    || DateTime.UtcNow - _lastCommitUtc >= CommitInterval
                )
                {
                    await CommitMaxAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }

            LogProgress();
        }

        private async Task CommitMaxAsync(CancellationToken ct)
        {
            if (!_hasSeen)
                return;
            // Strictly-forward guard: never regress the checkpoint, even under reordering.
            if (_hasCommitted && _maxSeen.CompareTo(_committed) <= 0)
                return;
            await CheckpointStore.CommitAsync(CheckpointId, _maxSeen, ct).ConfigureAwait(false);
            _committed = _maxSeen;
            _hasCommitted = true;
            _lastCommitUtc = DateTime.UtcNow;
        }

        /// <inheritdoc/>
        protected override void CleanUp()
        {
            // Final flush of the highest seen position when the pipeline completes.
            CommitMaxAsync(CancellationToken.None).GetAwaiter().GetResult();
            base.CleanUp();
        }
    }
}
