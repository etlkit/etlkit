using System.Diagnostics;
using Newtonsoft.Json;

namespace EtlKit.Logging
{
    /// <summary>
    /// Represents one row of the log table (see <c>CreateLogTableTask</c>,
    /// <c>DatabaseLoggingConfiguration</c>): a single structured log entry produced by a task or
    /// component during a load process.
    /// </summary>
    [DebuggerDisplay("#{Id} {TaskType} - {TaskAction} {LogDate}")]
    public class LogEntry
    {
        /// <summary>
        /// Database-assigned identifier of this log row.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// When this log entry was written.
        /// </summary>
        public DateTime LogDate { get; set; }

        /// <summary>
        /// When the logged operation ended, or <see langword="null"/> if not applicable/not yet finished.
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Log level (e.g. <c>Information</c>, <c>Warning</c>, <c>Error</c>).
        /// </summary>
        public string Level { get; set; }

        /// <summary>
        /// The plain-text log message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// The class name of the task or component that produced the entry.
        /// </summary>
        public string TaskType { get; set; }

        /// <summary>
        /// The log action (e.g. <c>START</c>, <c>END</c>, <c>RUN</c>, <c>LOG</c>).
        /// </summary>
        public string TaskAction { get; set; }

        /// <summary>
        /// Hash of the task or component that produced the entry (see <see cref="EtlKit.Primitives.ITask.TaskHash"/>).
        /// </summary>
        public string TaskHash { get; set; }

        /// <summary>
        /// The value of <c>ControlFlow.Stage</c> at the time this entry was written.
        /// </summary>
        public string Stage { get; set; }

        /// <summary>
        /// Free-text label identifying what produced this entry.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// The id of the load process this entry belongs to, or <see langword="null"/> if none was started.
        /// </summary>
        public long? LoadProcessId { get; set; }
    }

    /// <summary>
    /// A <see cref="LogEntry"/> augmented with parent/child links, letting log entries from nested
    /// tasks be assembled into a tree.
    /// </summary>
    [PublicAPI]
    [DebuggerDisplay("#{Id} {TaskType} - {TaskAction} {LogDate}")]
    public class LogHierarchyEntry : LogEntry
    {
        /// <summary>
        /// Log entries produced by tasks nested within this one.
        /// </summary>
        public List<LogHierarchyEntry> Children { get; set; }

        /// <summary>
        /// The entry this one is nested under, or <see langword="null"/> for a top-level entry.
        /// Excluded from JSON serialization to avoid a reference cycle with <see cref="Children"/>.
        /// </summary>
        [JsonIgnore]
        public LogHierarchyEntry Parent { get; set; }

        /// <summary>
        /// Creates an entry with no fields set yet and an empty <see cref="Children"/> list.
        /// </summary>
        public LogHierarchyEntry()
        {
            Children = new List<LogHierarchyEntry>();
        }

        /// <summary>
        /// Creates an entry copying every field from <paramref name="entry"/>, with an empty <see
        /// cref="Children"/> list.
        /// </summary>
        /// <param name="entry">The log entry to copy fields from.</param>
        public LogHierarchyEntry(LogEntry entry)
            : this()
        {
            Id = entry.Id;
            LogDate = entry.LogDate;
            EndDate = entry.EndDate;
            Level = entry.Level;
            Message = entry.Message;
            TaskType = entry.TaskType;
            TaskAction = entry.TaskAction;
            TaskHash = entry.TaskHash;
            Stage = entry.Stage;
            Source = entry.Source;
            LoadProcessId = entry.LoadProcessId;
        }
    }
}
