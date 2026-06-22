using System.Collections.Concurrent;

using EtlKit.Primitives;

using EtlKit.Common.DataFlow;

using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
    public class ErrorLogDestination : DataFlowDestination<EtlKitError>
    {
        /* ITask Interface */
        public override string TaskName => "Write error";

        public BlockingCollection<EtlKitError> Errors { get; set; } = new();

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

        protected override void CleanUp()
        {
            Errors?.CompleteAdding();
            OnCompletion?.Invoke();
            LogFinish();
        }
    }
}
