using System.Data.Odbc;
using EtlKit.Primitives;

namespace EtlKit.ConnectionManager
{
    /// <summary>
    /// Base class for connection managers that access a database through an ODBC driver (e.g. MS
    /// Access), sharing a common bulk-insert implementation since ODBC has no native bulk-copy API.
    /// </summary>
    public abstract class OdbcConnectionManager : DbConnectionManager<OdbcConnection>
    {
        /// <summary>
        /// Creates a connection manager with no connection string set yet.
        /// </summary>
        protected OdbcConnectionManager() { }

        /// <summary>
        /// Creates a connection manager for the given ODBC connection string.
        /// </summary>
        /// <param name="connectionString">Connection string for the ODBC driver.</param>
        protected OdbcConnectionManager(OdbcConnectionString connectionString)
            : base(connectionString) { }

        /// <summary>
        /// Bulk-loads <paramref name="data"/> into <paramref name="tableName"/> by building and
        /// executing a single parameterized multi-row <c>INSERT</c> statement via <paramref
        /// name="bulkInsert"/>, since ODBC has no dedicated bulk-copy API.
        /// </summary>
        /// <param name="data">Row-by-row source data to insert.</param>
        /// <param name="tableName">Destination table name.</param>
        /// <param name="bulkInsert">Builds the multi-row insert statement and its parameters.</param>
        internal void OdbcBulkInsert(ITableData data, string tableName, BulkInsertSql bulkInsert)
        {
            var sql = bulkInsert.CreateBulkInsertStatement(data, tableName);
            var cmd = DbConnection.CreateCommand();
            cmd.Transaction = Transaction as OdbcTransaction;
            cmd.Parameters.AddRange(bulkInsert.Parameters.ToArray());
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }
}
