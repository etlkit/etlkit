using EtlKit.Primitives;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Checks if a database exists.
    /// </summary>
    [PublicAPI]
    public class IfDatabaseExistsTask : IfExistsTask
    {
        /* ITask Interface */
        internal override string GetSql()
        {
            if (!DbConnectionManager.SupportDatabases)
                throw new ETLBoxNotSupportedException("This task is not supported!");

            return ConnectionType switch
            {
                ConnectionManagerType.SqlServer
                    => $@"SELECT COUNT(*) FROM sys.databases WHERE [NAME] = '{ON.UnquotedObjectName}'",
                ConnectionManagerType.MySql
                    => $@"SELECT COUNT(*)  FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{ON.UnquotedObjectName}'",
                ConnectionManagerType.Postgres
                    => $@"SELECT COUNT(*) FROM pg_database WHERE datname = '{ON.UnquotedObjectName}'",
                ConnectionManagerType.ClickHouse
                    => $@"SELECT COUNT(*) FROM system.databases WHERE name = '{ON.UnquotedObjectName}'",
                _ => string.Empty
            };
        }

        /* Some constructors */
        public IfDatabaseExistsTask() { }

        public IfDatabaseExistsTask(string databaseName)
            : this()
        {
            ObjectName = databaseName;
        }

        /* Static methods for convenience */
        public static bool IsExisting(string databaseName) =>
            new IfDatabaseExistsTask(databaseName).Exists();

        public static bool IsExisting(IConnectionManager connectionManager, string databaseName) =>
            new IfDatabaseExistsTask(databaseName)
            {
                ConnectionManager = connectionManager
            }.Exists();
    }
}
