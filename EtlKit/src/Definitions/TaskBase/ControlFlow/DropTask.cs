using EtlKit.Common;
using EtlKit.Common.ControlFlow;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Base class for tasks that drop a database object only if it exists, using the matching <see
    /// cref="IfExistsTask"/> subclass <typeparamref name="T"/> to check first. Derived classes
    /// implement <see cref="GetSql"/> with the engine-specific <c>DROP</c> statement.
    /// </summary>
    /// <typeparam name="T">The <see cref="IfExistsTask"/> subclass used to check existence before dropping.</typeparam>
    [PublicAPI]
    public abstract class DropTask<T> : GenericTask
        where T : IfExistsTask, new()
    {
        /// <inheritdoc />
        public override string TaskName => $"Drop Object {ObjectName}";

        /// <summary>
        /// Checks whether the object exists (via a <typeparamref name="T"/> instance, logging
        /// disabled) and, if so, runs <see cref="Sql"/> to drop it.
        /// </summary>
        public void Execute()
        {
            var objectExists = new T
            {
                ObjectName = ObjectName,
                OnObjectName = OnObjectName,
                ConnectionManager = ConnectionManager,
                DisableLogging = true,
            }.Exists();
            if (objectExists)
                new SqlTask(this, Sql).ExecuteNonQuery();
        }

        /// <summary>
        /// The name of the object to drop, e.g. a table, schema, procedure, or index name.
        /// </summary>
        public string ObjectName { get; set; }

        /// <summary>
        /// <see cref="ObjectName"/> parsed into quoted/unquoted schema and table parts.
        /// </summary>
        public ObjectNameDescriptor ON => new(ObjectName, QB, QE);

        /// <summary>
        /// For drops scoped to a container object (e.g. an index on a table), the name of that
        /// container object. Passed through to the <typeparamref name="T"/> existence check.
        /// </summary>
        internal string OnObjectName { get; set; }

        /// <summary>
        /// The engine-specific <c>DROP</c> statement, from <see cref="GetSql"/>.
        /// </summary>
        public string Sql => GetSql();

        /// <summary>
        /// Builds the <c>DROP</c> statement. Returns an empty string by default; derived classes
        /// override per supported engine.
        /// </summary>
        internal virtual string GetSql() => string.Empty;

        /// <summary>
        /// Drops the object unconditionally, without checking whether it exists first.
        /// </summary>
        public void Drop() => new SqlTask(this, Sql).ExecuteNonQuery();

        /// <summary>
        /// Drops the object only if it exists. Equivalent to <see cref="Execute"/>.
        /// </summary>
        public void DropIfExists() => Execute();
    }
}
