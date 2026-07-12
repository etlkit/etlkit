using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace EtlKit.Primitives
{
    /// <summary>
    /// Database connection abstraction implemented by every connection manager (e.g. a SQL Server or
    /// PostgreSQL connection manager). Provides connection lifecycle, command execution, transactions,
    /// and bulk insert operations for control flow and data flow tasks.
    /// </summary>
    public interface IConnectionManager : IDisposable
    {
        /// <summary>
        /// Identifies which database engine this connection manager targets.
        /// </summary>
        ConnectionManagerType ConnectionManagerType { get; }

        /// <summary>
        /// The connection string used to open the database connection.
        /// </summary>
        IDbConnectionString ConnectionString { get; set; }

        /// <summary>
        /// Opens the database connection, retrying up to <see cref="MaxLoginAttempts"/> times on
        /// failure. Reuses the existing connection when <see cref="LeaveOpen"/> is <see langword="true"/>
        /// and one is already open; otherwise closes any existing connection first.
        /// </summary>
        void Open();

        /// <summary>
        /// Closes and disposes the database connection unconditionally, regardless of <see cref="LeaveOpen"/>.
        /// </summary>
        void Close();

        /// <summary>
        /// Closes and disposes the database connection unless <see cref="LeaveOpen"/> is <see
        /// langword="true"/>, in which case this is a no-op.
        /// </summary>
        void CloseIfAllowed();

        /// <summary>
        /// Current state of the underlying database connection, or <see langword="null"/> if it has
        /// not been created yet.
        /// </summary>
        ConnectionState? State { get; }

        /// <summary>
        /// Maximum number of connection attempts <see cref="Open"/> makes before giving up.
        /// </summary>
        int MaxLoginAttempts { get; set; }

        /// <summary>
        /// Creates a database command for <paramref name="commandText"/>, binding <paramref
        /// name="parameterList"/> and attaching the active <see cref="Transaction"/> if one is open.
        /// </summary>
        /// <param name="commandText">The SQL command text.</param>
        /// <param name="parameterList">Parameters to bind, or <see langword="null"/> for none.</param>
        IDbCommand CreateCommand(string commandText, IEnumerable<IQueryParameter> parameterList);

        /// <summary>
        /// Executes <paramref name="command"/> and returns the number of rows affected.
        /// </summary>
        /// <param name="command">The SQL command text.</param>
        /// <param name="parameterList">Parameters to bind, or <see langword="null"/> for none.</param>
        int ExecuteNonQuery(string command, IEnumerable<IQueryParameter> parameterList = null);

        /// <summary>
        /// Executes <paramref name="command"/> and returns the first column of the first row of the result.
        /// </summary>
        /// <param name="command">The SQL command text.</param>
        /// <param name="parameterList">Parameters to bind, or <see langword="null"/> for none.</param>
        object ExecuteScalar(string command, IEnumerable<IQueryParameter> parameterList = null);

        /// <summary>
        /// Executes <paramref name="command"/> and returns a forward-only reader over the result set.
        /// </summary>
        /// <param name="command">The SQL command text.</param>
        /// <param name="parameterList">Parameters to bind, or <see langword="null"/> for none.</param>
        IDataReader ExecuteReader(
            string command,
            IEnumerable<IQueryParameter> parameterList = null
        );

        /// <summary>
        /// The active transaction, or <see langword="null"/> if none is in progress.
        /// </summary>
        IDbTransaction Transaction { get; set; }

        /// <summary>
        /// Bulk-loads <paramref name="data"/> into <paramref name="tableName"/> using the driver's
        /// native bulk-insert mechanism.
        /// </summary>
        /// <param name="data">Row-by-row source data to insert.</param>
        /// <param name="tableName">Destination table name.</param>
        void BulkInsert(ITableData data, string tableName);

        /// <summary>
        /// Called before the first batch of a bulk insert into <paramref name="tableName"/>, in case
        /// <see cref="PrepareBulkInsert"/> was not called explicitly.
        /// </summary>
        /// <param name="tableName">Destination table name.</param>
        void BeforeBulkInsert(string tableName);

        /// <summary>
        /// Called after the last batch of a bulk insert into <paramref name="tableName"/> completes,
        /// to let the connection manager perform any needed follow-up.
        /// </summary>
        /// <param name="tableName">Destination table name.</param>
        void AfterBulkInsert(string tableName);

        /// <summary>
        /// Creates a new connection manager with the same connection string and settings, for use by
        /// another component.
        /// </summary>
        IConnectionManager Clone();

        /// <summary>
        /// Returns this instance if <see cref="LeaveOpen"/> is <see langword="true"/> (so its open
        /// connection and transaction are shared), otherwise returns a new instance from <see cref="Clone"/>.
        /// </summary>
        IConnectionManager CloneIfAllowed();

        /// <summary>
        /// When <see langword="true"/>, keeps the underlying connection open across <see cref="Close"/>
        /// and <see cref="CloseIfAllowed"/> calls instead of closing it. Also <see langword="true"/>
        /// whenever <see cref="Transaction"/> is active, since a transaction requires its connection to
        /// stay open.
        /// </summary>
        bool LeaveOpen { get; set; }

        /// <summary>
        /// Indicates whether this connection manager is currently performing a bulk insert.
        /// </summary>
        bool IsInBulkInsert { get; set; }

        /// <summary>
        /// Performs any setup needed before <see cref="BulkInsert"/> can run against <paramref
        /// name="tableName"/>, e.g. reading destination column definitions.
        /// </summary>
        /// <param name="tableName">Destination table name.</param>
        void PrepareBulkInsert(string tableName);

        /// <summary>
        /// Performs any cleanup needed after a bulk insert into <paramref name="tableName"/> completes.
        /// </summary>
        /// <param name="tableName">Destination table name.</param>
        void CleanUpBulkInsert(string tableName);

        /// <summary>
        /// Opens the connection if needed and starts a new transaction with the given isolation level.
        /// </summary>
        /// <param name="isolationLevel">Isolation level for the new transaction.</param>
        void BeginTransaction(IsolationLevel isolationLevel);

        /// <summary>
        /// Opens the connection if needed and starts a new transaction with <see
        /// cref="IsolationLevel.Unspecified"/>.
        /// </summary>
        void BeginTransaction();

        /// <summary>
        /// Commits the active transaction and closes it via <see cref="CloseTransaction"/>.
        /// </summary>
        void CommitTransaction();

        /// <summary>
        /// Rolls back the active transaction and closes it via <see cref="CloseTransaction"/>.
        /// </summary>
        void RollbackTransaction();

        /// <summary>
        /// Disposes the active transaction, clears <see cref="Transaction"/>, and closes the connection
        /// unless <see cref="LeaveOpen"/> is <see langword="true"/>.
        /// </summary>
        void CloseTransaction();

        /// <summary>
        /// Executes <paramref name="sql"/> and reports whether it returned a non-empty scalar result.
        /// </summary>
        /// <param name="callingTask">The task requesting the check, used for logging context.</param>
        /// <param name="sql">A scalar query that returns a value only if the index exists.</param>
        bool IndexExists(ITask callingTask, string sql);

        /// <summary>
        /// Quotation begin character used to escape identifiers in generated SQL.
        /// </summary>
        string QB { get; }

        /// <summary>
        /// Quotation end character used to escape identifiers in generated SQL.
        /// </summary>
        string QE { get; }

        /// <summary>
        /// Whether this database engine supports creating and dropping databases (checked by tasks
        /// such as <c>CreateDatabaseTask</c> and <c>DropDatabaseTask</c>).
        /// </summary>
        bool SupportDatabases { get; }

        /// <summary>
        /// Whether this database engine supports stored procedures (checked by tasks such as
        /// <c>CreateProcedureTask</c> and <c>DropProcedureTask</c>).
        /// </summary>
        bool SupportProcedures { get; }

        /// <summary>
        /// Whether this database engine supports schemas (checked by tasks such as
        /// <c>CreateSchemaTask</c> and <c>DropSchemaTask</c>).
        /// </summary>
        bool SupportSchemas { get; }

        /// <summary>
        /// Whether this database engine supports computed columns (checked by <c>CreateTableTask</c>).
        /// </summary>
        bool SupportComputedColumns { get; }

        /// <summary>
        /// Culture used to format and parse values sent to and read from the database.
        /// </summary>
        CultureInfo ConnectionCulture { get; }
    }
}
