using System.Globalization;

namespace EtlKit.Primitives
{
    /// <summary>
    /// Base contract implemented by every control flow task and data flow component. Carries the
    /// identity, connection, and logging settings shared by all of them.
    /// </summary>
    public interface ITask
    {
        /// <summary>
        /// Human-readable name of this task instance, used in log entries and error messages.
        /// </summary>
        string TaskName { get; }

        /// <summary>
        /// The class name of the task or component, used in log entries to identify its type.
        /// </summary>
        string TaskType { get; }

        /// <summary>
        /// A hash of the task or component, derived from <see cref="TaskType"/> and <see cref="TaskName"/>,
        /// used to correlate log entries produced by the same task instance.
        /// </summary>
        string TaskHash { get; }

        /// <summary>
        /// The connection manager this task uses to access the database.
        /// </summary>
        IConnectionManager ConnectionManager { get; }

        /// <summary>
        /// When <see langword="true"/>, suppresses database logging for this task.
        /// </summary>
        bool DisableLogging { get; }

        /// <summary>
        /// Culture used to format and parse values processed by this task.
        /// </summary>
        CultureInfo CurrentCulture { get; }
    }
}
