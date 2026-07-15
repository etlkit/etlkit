using System;

namespace EtlKit.Common.Logging
{
    /// <summary>
    /// Represents one row of the load process table (see <c>StartLoadProcessTask</c>,
    /// <c>EndLoadProcessTask</c>, <c>AbortLoadProcessTask</c>), tracking the lifecycle of a logical
    /// ETL run from start to success or abort.
    /// </summary>
    public class LoadProcess
    {
        /// <summary>
        /// Database-assigned identifier of this load process row, or <see langword="null"/> before it
        /// has been inserted.
        /// </summary>
        public long? Id { get; set; }

        /// <summary>
        /// When the load process started.
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// When the load process finished (successfully or aborted), or <see langword="null"/> while
        /// still running.
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Free-text label identifying what started this load process. Defaults to <c>"ETL"</c>.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Name of the load process.
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// Message recorded when the load process started.
        /// </summary>
        public string StartMessage { get; set; }

        /// <summary>
        /// Whether the load process is still running.
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Message recorded when the load process finished successfully.
        /// </summary>
        public string EndMessage { get; set; }

        /// <summary>
        /// Whether the load process finished successfully.
        /// </summary>
        public bool WasSuccessful { get; set; }

        /// <summary>
        /// Message recorded when the load process was aborted.
        /// </summary>
        public string AbortMessage { get; set; }

        /// <summary>
        /// Whether the load process was aborted.
        /// </summary>
        public bool WasAborted { get; set; }

        /// <summary>
        /// Whether the load process has finished, successfully or aborted.
        /// </summary>
        public bool IsFinished => WasSuccessful || WasAborted;
    }
}
