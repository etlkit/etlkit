using EtlKit.Common;
using EtlKit.Common.ControlFlow;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Base class for tasks that check whether a database object exists (e.g. table, schema,
    /// procedure, index). Some checks are scoped to a container object — e.g. an index exists on a
    /// table — represented by <see cref="ObjectName"/>/<see cref="OnObjectName"/>. Derived classes
    /// implement <see cref="GetSql"/> with the engine-specific existence query.
    /// </summary>
    [PublicAPI]
    public abstract class IfExistsTask : GenericTask
    {
        /// <inheritdoc />
        public override string TaskName => $"Check if {ObjectName} exists";

        /// <summary>
        /// Runs <see cref="GetSql"/> and stores whether it found the object in <see cref="DoesExist"/>.
        /// Does nothing if <see cref="Sql"/> is empty (e.g. the check is not supported for the current
        /// <see cref="EtlKit.Common.ControlFlow.GenericTask.ConnectionType"/>).
        /// </summary>
        public virtual void Execute()
        {
            if (Sql != string.Empty)
                DoesExist = new SqlTask(this, Sql).ExecuteScalarAsBool();
        }

        /// <summary>
        /// The name of the object being checked, e.g. a table, schema, procedure, or index name.
        /// </summary>
        public string ObjectName { get; set; }

        /// <summary>
        /// <see cref="ObjectName"/> parsed into quoted/unquoted schema and table parts.
        /// </summary>
        public ObjectNameDescriptor ON => new(ObjectName, QB, QE);

        /// <summary>
        /// For checks scoped to a container object (e.g. an index existing on a table), the name of
        /// that container object.
        /// </summary>
        internal string OnObjectName { get; set; }

        /// <summary>
        /// <see cref="OnObjectName"/> parsed into quoted/unquoted schema and table parts.
        /// </summary>
        public ObjectNameDescriptor OON => new(OnObjectName, QB, QE);

        /// <summary>
        /// Whether the object was found by the last <see cref="Execute"/>/<see cref="Exists"/> call.
        /// </summary>
        public bool DoesExist { get; internal set; }

        /// <summary>
        /// The engine-specific existence query, from <see cref="GetSql"/>.
        /// </summary>
        public string Sql
        {
            get { return GetSql(); }
        }

        /// <summary>
        /// Builds the existence query for the current <see
        /// cref="EtlKit.Common.ControlFlow.GenericTask.ConnectionType"/>. Returns an empty string by
        /// default; derived classes override per supported engine.
        /// </summary>
        internal virtual string GetSql()
        {
            return string.Empty;
        }

        /// <summary>
        /// Runs <see cref="Execute"/> and returns <see cref="DoesExist"/>.
        /// </summary>
        public virtual bool Exists()
        {
            Execute();
            return DoesExist;
        }
    }
}
