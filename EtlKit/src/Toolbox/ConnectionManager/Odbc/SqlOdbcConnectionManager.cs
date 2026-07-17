using EtlKit.Primitives;

namespace EtlKit.ConnectionManager
{
    /// <summary>
    /// Sql Connection manager for an ODBC connection based on ADO.NET to Sql Server.
    /// ODBC by default does not support a Bulk Insert - inserting big amounts of data is translated into a
    /// <code>
    /// insert into (...) values (..),(..),(..) statementes.
    /// </code>
    /// This means that inserting big amounts of data in a database via Odbc can be much slower
    /// than using the native connector.
    /// Also be careful with the batch size - some databases have limitations regarding the length of sql statements.
    /// Reduce the batch size if you encounter issues here.
    /// </summary>
    /// <example>
    /// <code>
    /// ControlFlow.DefaultDbConnection =
    ///   new OdbcConnectionManager(new ObdcConnectionString(
    ///     "Driver={SQL Server};Server=.;Database=EtlKit;Trusted_Connection=Yes;"));
    /// </code>
    /// </example>
    [PublicAPI]
    public class SqlOdbcConnectionManager : OdbcConnectionManager
    {
        /// <inheritdoc />
        public override ConnectionManagerType ConnectionManagerType { get; } =
            ConnectionManagerType.SqlServer;

        /// <inheritdoc />
        public override string QB { get; } = @"[";

        /// <inheritdoc />
        public override string QE { get; } = @"]";

        /// <inheritdoc />
        public override CultureInfo ConnectionCulture => CultureInfo.CurrentCulture;

        /// <summary>
        /// Creates a connection manager with no connection string set yet.
        /// </summary>
        public SqlOdbcConnectionManager() { }

        /// <summary>
        /// Creates a connection manager for the given ODBC connection string.
        /// </summary>
        /// <param name="connectionString">Connection string for the SQL Server ODBC driver.</param>
        public SqlOdbcConnectionManager(OdbcConnectionString connectionString)
            : base(connectionString) { }

        /// <summary>
        /// Creates a connection manager from a raw SQL Server ODBC connection string.
        /// </summary>
        /// <param name="connectionString">Connection string for the SQL Server ODBC driver.</param>
        public SqlOdbcConnectionManager(string connectionString)
            : base(new OdbcConnectionString(connectionString)) { }

        /// <inheritdoc />
        /// <remarks>
        /// Builds the insert as a parameterized <c>INSERT INTO ... VALUES (..),(..),(..)</c> statement;
        /// reduce the batch size if the resulting statement exceeds the driver's length limits.
        /// </remarks>
        public override void BulkInsert(ITableData data, string tableName)
        {
            var bulkInsert = new BulkInsertSql
            {
                UseParameterQuery = true,
                QB = QB,
                QE = QE,
                //ConnectionType = ConnectionManagerType.SqlServer
            };
            OdbcBulkInsert(data, tableName, bulkInsert);
        }

        /// <inheritdoc />
        public override IConnectionManager Clone()
        {
            var clone = new SqlOdbcConnectionManager((OdbcConnectionString)ConnectionString)
            {
                MaxLoginAttempts = MaxLoginAttempts,
            };
            return clone;
        }

        /// <inheritdoc />
        public override void BeforeBulkInsert(string tableName) { }

        /// <inheritdoc />
        public override void AfterBulkInsert(string tableName) { }

        /// <inheritdoc />
        public override void PrepareBulkInsert(string tableName) { }

        /// <inheritdoc />
        public override void CleanUpBulkInsert(string tableName) { }
    }
}
