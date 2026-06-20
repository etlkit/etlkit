using EtlKit.Primitives;

using EtlKit.ConnectionManager;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Checks if a table exists.
    /// </summary>
    [PublicAPI]
    public sealed class IfTableOrViewExistsTask : IfExistsTask
    {
        internal override string GetSql()
        {
            switch (ConnectionType)
            {
                case ConnectionManagerType.SQLite:
                    return $@"SELECT 1 FROM sqlite_master WHERE name='{ON.UnquotedObjectName}';";
                case ConnectionManagerType.SqlServer:
                    return $@"IF ( OBJECT_ID('{ON.QuotedFullName}', 'U') IS NOT NULL OR OBJECT_ID('{ON.QuotedFullName}', 'V') IS NOT NULL)
                            SELECT 1";
                case ConnectionManagerType.MySql:
                    return $@"SELECT EXISTS(
                                    SELECT table_name
                                    FROM information_schema.tables
                                    WHERE table_schema = DATABASE()
                                      AND ( table_name = '{ON.UnquotedFullName}' OR CONCAT(table_catalog, '.', table_name) = '{ON.UnquotedFullName}')
                                ) AS 'DoesExist'";
                case ConnectionManagerType.Postgres:
                    return $@"SELECT EXISTS(
                                    SELECT table_name
                                    FROM information_schema.tables
                                    WHERE table_catalog = CURRENT_DATABASE()
                                      AND ( table_name = '{ON.UnquotedFullName}' OR CONCAT(table_schema, '.', table_name) = '{ON.UnquotedFullName}')
                                    )";
                case ConnectionManagerType.ClickHouse:
                    return $@"SELECT EXISTS(
                                    SELECT *
                                    FROM system.tables
                                    WHERE database = currentDatabase()
                                      AND name = '{ON.UnquotedFullName}')";
            }

            if (ConnectionType != ConnectionManagerType.Access)
            {
                return string.Empty;
            }

            var connMan = DbConnectionManager.CloneIfAllowed() as AccessOdbcConnectionManager;
            DoesExist = connMan?.CheckIfTableOrViewExists(ON.UnquotedFullName) ?? false;
            connMan?.CloseIfAllowed();
            return string.Empty;
        }

        public IfTableOrViewExistsTask() { }

        public IfTableOrViewExistsTask(string tableName)
            : this()
        {
            ObjectName = tableName;
        }

        public IfTableOrViewExistsTask(IConnectionManager connectionManager, string tableName)
            : this(tableName)
        {
            ConnectionManager = connectionManager;
        }

        public static bool IsExisting(string objectName) =>
            new IfTableOrViewExistsTask(objectName).Exists();

        public static bool IsExisting(IConnectionManager connectionManager, string objectName) =>
            new IfTableOrViewExistsTask(objectName)
            {
                ConnectionManager = connectionManager
            }.Exists();

        public static void ThrowExceptionIfNotExists(
            IConnectionManager connectionManager,
            string tableName
        )
        {
            var tableExists = new IfTableOrViewExistsTask(tableName)
            {
                ConnectionManager = connectionManager,
                DisableLogging = true
            }.Exists();
            if (!tableExists)
                throw new InvalidOperationException(
                    $"A table {tableName} does not exists in the database!"
                );
        }
    }
}
