using EtlKit.Primitives;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Drops a database. Use DropIfExists to drop a database only if it exists. In MySql, this will drop a schema.
    /// </summary>
    /// <example>
    /// <code>
    /// DropDatabaseTask.Delete("DemoDB");
    /// </code>
    /// </example>
    [PublicAPI]
    public class DropDatabaseTask : DropTask<IfDatabaseExistsTask>
    {
        internal override string GetSql()
        {
            if (!DbConnectionManager.SupportDatabases)
                throw new EtlKitNotSupportedException("This task is not supported!");

            return ConnectionType switch
            {
                ConnectionManagerType.SqlServer => $@"
USE [master]
ALTER DATABASE [{ObjectName}]
SET SINGLE_USER WITH ROLLBACK IMMEDIATE
ALTER DATABASE [{ObjectName}]
SET MULTI_USER
DROP DATABASE [{ObjectName}]  
",
                ConnectionManagerType.Postgres => $@"
DROP DATABASE {ON.QuotedObjectName} WITH (force)
",
                _ => $@"DROP DATABASE {ON.QuotedObjectName}",
            };
        }

        public DropDatabaseTask() { }

        public DropDatabaseTask(string databaseName)
            : this()
        {
            ObjectName = databaseName;
        }

        public static void Drop(string databaseName) => new DropDatabaseTask(databaseName).Drop();

        public static void Drop(IConnectionManager connectionManager, string databaseName) =>
            new DropDatabaseTask(databaseName) { ConnectionManager = connectionManager }.Drop();

        public static void DropIfExists(string databaseName) =>
            new DropDatabaseTask(databaseName).DropIfExists();

        public static void DropIfExists(
            IConnectionManager connectionManager,
            string databaseName
        ) =>
            new DropDatabaseTask(databaseName)
            {
                ConnectionManager = connectionManager,
            }.DropIfExists();
    }
}
