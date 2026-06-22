using EtlKit.Primitives;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Checks if an index exists.
    /// </summary>
    [PublicAPI]
    public class IfIndexExistsTask : IfExistsTask
    {
        /* ITask Interface */
        internal override string GetSql()
        {
            return ConnectionType switch
            {
                ConnectionManagerType.SQLite
                    => $@"
SELECT 1 FROM sqlite_master WHERE name='{ON.UnquotedObjectName}' AND type='index';
",
                ConnectionManagerType.SqlServer
                    => $@"
IF EXISTS (SELECT *  FROM sys.indexes  WHERE name='{ON.UnquotedObjectName}' AND object_id = OBJECT_ID('{OON.QuotedFullName}'))
    SELECT 1
",
                ConnectionManagerType.MySql
                    => $@"
SELECT 1
FROM information_schema.statistics 
WHERE table_schema = DATABASE()
  AND ( table_name = '{OON.UnquotedFullName}' 
  OR CONCAT(table_name,'.',table_catalog) = '{OON.UnquotedFullName}')
  AND index_name = '{ON.UnquotedObjectName}'
GROUP BY index_name
",
                ConnectionManagerType.Postgres
                    => $@"
SELECT     1
FROM       pg_indexes
WHERE     ( CONCAT(schemaname,'.',tablename) = '{OON.UnquotedFullName}'
            OR tablename = '{OON.UnquotedFullName}' )
            AND indexname = '{ON.UnquotedObjectName}'
",
                ConnectionManagerType.ClickHouse 
                    => $"SHOW INDEX FROM {OnObjectName} where key_name = '{ObjectName}';",
                _ => string.Empty
            };
        }

        /* Some constructors */
        public IfIndexExistsTask() { }

        public IfIndexExistsTask(string indexName, string tableName)
            : this()
        {
            ObjectName = indexName;
            OnObjectName = tableName;
        }

        /* Static methods for convenience */
        public static bool IsExisting(string indexName, string tableName) =>
            new IfIndexExistsTask(indexName, tableName).Exists();

        public static bool IsExisting(
            IConnectionManager connectionManager,
            string indexName,
            string tableName
        ) =>
            new IfIndexExistsTask(indexName, tableName)
            {
                ConnectionManager = connectionManager
            }.Exists();

        public override void Execute()
        {
            if (!string.IsNullOrEmpty(Sql))
            {
                DoesExist = ConnectionManager.IndexExists(this, Sql);
            }
        }
    }
}
