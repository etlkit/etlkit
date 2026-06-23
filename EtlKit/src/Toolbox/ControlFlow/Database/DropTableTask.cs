using EtlKit.Primitives;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Drops a table. Use DropIfExists to drop a table only if it exists.
    /// </summary>
    [PublicAPI]
    public class DropTableTask : DropTask<IfTableOrViewExistsTask>
    {
        internal override string GetSql()
        {
            return $@"DROP TABLE {ON.QuotedFullName}";
        }

        public DropTableTask() { }

        public DropTableTask(string tableName)
            : this()
        {
            ObjectName = tableName;
        }

        public static void Drop(string tableName) => new DropTableTask(tableName).Drop();

        public static void Drop(IConnectionManager connectionManager, string tableName) =>
            new DropTableTask(tableName) { ConnectionManager = connectionManager }.Drop();

        public static void DropIfExists(string tableName) =>
            new DropTableTask(tableName).DropIfExists();

        public static void DropIfExists(IConnectionManager connectionManager, string tableName) =>
            new DropTableTask(tableName) { ConnectionManager = connectionManager }.DropIfExists();
    }
}
