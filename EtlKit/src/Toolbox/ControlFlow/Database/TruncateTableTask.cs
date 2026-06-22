using EtlKit.Primitives;

using EtlKit.Common;
using EtlKit.Common.ControlFlow;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Truncates a table.
    /// </summary>
    /// <example>
    /// <code>
    /// TruncateTableTask.Truncate("demo.table1");
    /// </code>
    /// </example>
    [PublicAPI]
    public class TruncateTableTask : GenericTask
    {
        /* ITask Interface */
        public override string TaskName => $"Truncate table {TableName}";

        public void Execute()
        {
            new SqlTask(this, Sql).ExecuteNonQuery();
        }

        /* Public properties */
        public string TableName { get; set; }
        public ObjectNameDescriptor TN => new(TableName, QB, QE);
        public string Sql
        {
            get
            {
                if (ConnectionType is ConnectionManagerType.SQLite or ConnectionManagerType.Access)
                    return $@"DELETE FROM {TN.QuotedFullName}";
                return $@"TRUNCATE TABLE {TN.QuotedFullName}";
            }
        }

        public TruncateTableTask() { }

        public TruncateTableTask(string tableName)
            : this()
        {
            TableName = tableName;
        }

        public static void Truncate(string tableName) => new TruncateTableTask(tableName).Execute();

        public static void Truncate(IConnectionManager connection, string tableName) =>
            new TruncateTableTask(tableName) { ConnectionManager = connection }.Execute();
    }
}
