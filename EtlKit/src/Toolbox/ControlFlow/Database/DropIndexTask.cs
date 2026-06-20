using EtlKit.Primitives;

using EtlKit.Common;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Drops an index. Use DropIfExists to drop an index only if it exists.
    /// </summary>
    [PublicAPI]
    public class DropIndexTask : DropTask<IfIndexExistsTask>
    {
        public string TableName => OnObjectName;
        public ObjectNameDescriptor TN => new(TableName, QB, QE);

        internal override string GetSql()
        {
            var sql = $@"DROP INDEX {ON.QuotedFullName}";
            if (
                ConnectionType != ConnectionManagerType.SQLite
                && ConnectionType != ConnectionManagerType.Postgres
            )
                sql += $@" ON {TN.QuotedFullName}";
            return sql;
        }

        public DropIndexTask() { }

        public DropIndexTask(string indexName, string tableName)
            : this()
        {
            OnObjectName = tableName;
            ObjectName = indexName;
        }

        public static void Drop(string indexName, string tableName) =>
            new DropIndexTask(indexName, tableName).Drop();

        public static void Drop(
            IConnectionManager connectionManager,
            string indexName,
            string tableName
        ) =>
            new DropIndexTask(indexName, tableName)
            {
                ConnectionManager = connectionManager
            }.Drop();

        public static void DropIfExists(string indexName, string tableName) =>
            new DropIndexTask(indexName, tableName).DropIfExists();

        public static void DropIfExists(
            IConnectionManager connectionManager,
            string indexName,
            string tableName
        ) =>
            new DropIndexTask(indexName, tableName)
            {
                ConnectionManager = connectionManager
            }.DropIfExists();
    }
}
