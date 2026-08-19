using System.Diagnostics.CodeAnalysis;
using EtlKit.Common.ControlFlow;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace EtlKit.Common.DataFlow
{
    /// <summary>
    /// Base class for data flow tasks, adding progress-logging on top of <see cref="GenericTask"/>:
    /// a start/end log entry plus periodic progress entries every <see
    /// cref="LoggingThresholdRows"/> rows.
    /// </summary>
    [PublicAPI]
    [SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem")]
    public abstract class DataFlowTask : GenericTask
    {
        /// <summary>
        /// Creates a new instance with no logger (uses static LoggerFactory fallback).
        /// </summary>
        protected DataFlowTask() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        protected DataFlowTask([CanBeNull] ILogger logger)
            : base(logger) { }

        private int? _loggingThresholdRows;

        /// <summary>
        /// Number of rows processed between progress log entries. When the global <see
        /// cref="Common.DataFlow.DataFlow.LoggingThresholdRows"/> is set, it takes precedence over
        /// this instance's own value; otherwise this value is used (or <see langword="null"/>/zero to
        /// disable progress logging).
        /// </summary>
        public virtual int? LoggingThresholdRows
        {
            get
            {
                return Common.DataFlow.DataFlow.HasLoggingThresholdRows
                    ? Common.DataFlow.DataFlow.LoggingThresholdRows
                    : _loggingThresholdRows;
            }
            set { _loggingThresholdRows = value; }
        }

        /// <summary>
        /// Total number of rows processed so far, updated by <see cref="LogProgress"/> and <see
        /// cref="LogProgressBatch"/>.
        /// </summary>
        public int ProgressCount { get; set; }

        /// <summary>
        /// Whether <see cref="LoggingThresholdRows"/> currently resolves to a positive value.
        /// </summary>
        protected bool HasLoggingThresholdRows => LoggingThresholdRows is > 0;

        /// <summary>
        /// Number of <see cref="LoggingThresholdRows"/> multiples reached so far; used by <see
        /// cref="LogProgressBatch"/> to log at most once per threshold crossing.
        /// </summary>
        protected int ThresholdCount { get; set; } = 1;

        /// <summary>
        /// Writes the <c>START</c> log entry for this task, unless <see
        /// cref="EtlKit.Primitives.ITask.DisableLogging"/> is set.
        /// </summary>
        protected void LogStart()
        {
            if (!DisableLogging)
                Logger.Info(
                    TaskName,
                    TaskType,
                    "START",
                    TaskHash,
                    ControlFlow.ControlFlow.Stage,
                    ControlFlow.ControlFlow.CurrentLoadProcess?.Id
                );
        }

        /// <summary>
        /// Writes a total-rows-processed entry (if <see cref="HasLoggingThresholdRows"/>) followed by
        /// the <c>END</c> log entry for this task, unless <see
        /// cref="EtlKit.Primitives.ITask.DisableLogging"/> is set.
        /// </summary>
        protected void LogFinish()
        {
            if (!DisableLogging && HasLoggingThresholdRows)
                Logger.Info(
                    TaskName + $" processed {ProgressCount} records in total.",
                    TaskType,
                    "LOG",
                    TaskHash,
                    ControlFlow.ControlFlow.Stage,
                    ControlFlow.ControlFlow.CurrentLoadProcess?.Id
                );
            if (!DisableLogging)
                Logger.Info(
                    TaskName,
                    TaskType,
                    "END",
                    TaskHash,
                    ControlFlow.ControlFlow.Stage,
                    ControlFlow.ControlFlow.CurrentLoadProcess?.Id
                );
        }

        /// <summary>
        /// Adds <paramref name="rowsProcessed"/> to <see cref="ProgressCount"/> and, once the total
        /// crosses the next multiple of <see cref="LoggingThresholdRows"/>, writes a progress log
        /// entry (at most once per threshold crossing, tracked via <see cref="ThresholdCount"/>).
        /// </summary>
        /// <param name="rowsProcessed">Number of rows processed in the batch just completed.</param>
        protected void LogProgressBatch(int rowsProcessed)
        {
            ProgressCount += rowsProcessed;
            if (
                DisableLogging
                || !HasLoggingThresholdRows
                || ProgressCount < LoggingThresholdRows * ThresholdCount
            )
            {
                return;
            }

            Logger.Info(
                TaskName + $" processed {ProgressCount} records.",
                TaskType,
                "LOG",
                TaskHash,
                ControlFlow.ControlFlow.Stage,
                ControlFlow.ControlFlow.CurrentLoadProcess?.Id
            );
            ThresholdCount++;
        }

        /// <summary>
        /// Increments <see cref="ProgressCount"/> by one and, when it lands exactly on a multiple of
        /// <see cref="LoggingThresholdRows"/>, writes a progress log entry. Intended for per-row
        /// (non-batched) processing loops.
        /// </summary>
        protected void LogProgress()
        {
            ProgressCount += 1;
            if (
                DisableLogging
                || !HasLoggingThresholdRows
                || ProgressCount % LoggingThresholdRows != 0
            )
            {
                return;
            }

            Logger.Info(
                TaskName + $" processed {ProgressCount} records.",
                TaskType,
                "LOG",
                TaskHash,
                ControlFlow.ControlFlow.Stage,
                ControlFlow.ControlFlow.CurrentLoadProcess?.Id
            );
        }
    }
}
