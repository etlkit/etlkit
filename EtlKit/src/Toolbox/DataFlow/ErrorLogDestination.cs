using System.Collections.Concurrent;
using EtlKit.Common.DataFlow;
using EtlKit.Primitives;
using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// A destination that collects error records (from <c>LinkErrorTo</c>) into an in-memory <see
    /// cref="BlockingCollection{T}"/> and periodically logs them, instead of writing them to a database.
    /// </summary>
    public class ErrorLogDestination : DataFlowDestination<EtlKitError>
    {
        /* ITask Interface */
        /// <inheritdoc />
        public override string TaskName => "Write error";

        /// <summary>
        /// The error records received so far.
        /// </summary>
        public BlockingCollection<EtlKitError> Errors { get; set; } = new();

        /// <summary>
        /// Creates a new instance with no logger.
        /// </summary>
        public ErrorLogDestination()
            : this(null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public ErrorLogDestination([CanBeNull] ILogger<ErrorLogDestination> logger)
            : base(logger)
        {
            TargetAction = new ActionBlock<EtlKitError>(WriteRecord);
            SetCompletionTask();
        }

        private void WriteRecord(EtlKitError error)
        {
            Errors ??= new BlockingCollection<EtlKitError>();
            if (error is null)
                return;
            Errors.Add(error);

            if (
                DisableLogging
                || !HasLoggingThresholdRows
                || ProgressCount % LoggingThresholdRows != 0
            )
            {
                return;
            }
            var logException = LoggerMessage.Define<string, string>(
                LogLevel.Error,
                0,
                "{ErrorText}: {RecordAsJson}"
            );
            logException.Invoke(Logger, error.ErrorText, error.RecordAsJson, error.Exception);
        }

        /// <summary>
        /// Marks <see cref="Errors"/> as complete (no more records will be added), then runs the base
        /// cleanup (invoking <see cref="EtlKit.Common.DataFlow.DataFlowDestination{TInput}.OnCompletion"/>
        /// and logging).
        /// </summary>
        protected override void CleanUp()
        {
            Errors?.CompleteAdding();
            OnCompletion?.Invoke();
            LogFinish();
        }
    }
}
