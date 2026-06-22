using EtlKit.Primitives;
using MySql.Data.MySqlClient;

namespace EtlKit.ConnectionManager
{
    /// <summary>
    /// Connection manager of a classic ADO.NET connection to a (Microsoft) Sql Server.
    /// </summary>
    /// <example>
    /// <code>
    /// ControlFlow.DefaultDbConnection = new SqlConnectionManager(new ConnectionString("Data Source=.;"));
    /// </code>
    /// </example>
    [PublicAPI]
    public class MySqlConnectionManager : DbConnectionManager<MySqlConnection>
    {
        public override ConnectionManagerType ConnectionManagerType { get; } =
            ConnectionManagerType.MySql;
        public override string QB { get; } = @"`";
        public override string QE { get; } = @"`";
        public override CultureInfo ConnectionCulture => CultureInfo.InvariantCulture;
        public override bool SupportSchemas { get; }

        public MySqlConnectionManager() { }

        public MySqlConnectionManager(MySqlConnectionString connectionString)
            : base(connectionString) { }

        public MySqlConnectionManager(string connectionString)
            : base(new MySqlConnectionString(connectionString)) { }

        public override void BulkInsert(ITableData data, string tableName)
        {
            var bulkInsert = new BulkInsertSql<MySqlParameter>
            {
                ConnectionType = ConnectionManagerType.MySql,
                QB = QB,
                QE = QE,
                UseParameterQuery = true
            };
            var sql = bulkInsert.CreateBulkInsertStatement(data, tableName);
            var cmd = DbConnection.CreateCommand();
            cmd.Transaction = Transaction as MySqlTransaction;
            cmd.Parameters.AddRange(bulkInsert.Parameters.ToArray());
            cmd.CommandText = sql;
            cmd.Prepare();
            cmd.ExecuteNonQuery();
        }

        public override void PrepareBulkInsert(string tableName) { }

        public override void CleanUpBulkInsert(string tableName) { }

        public override void BeforeBulkInsert(string tableName) { }

        public override void AfterBulkInsert(string tableName) { }

        public override IConnectionManager Clone()
        {
            var clone = new MySqlConnectionManager(
                (MySqlConnectionString)ConnectionString
            )
            {
                MaxLoginAttempts = MaxLoginAttempts
            };
            return clone;
        }
    }
}
