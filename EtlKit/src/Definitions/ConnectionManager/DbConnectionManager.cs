using System.Diagnostics;
using EtlKit.Common;
using EtlKit.ControlFlow;
using EtlKit.Primitives;

namespace EtlKit.ConnectionManager
{
    /// <summary>
    /// Base <see cref="IConnectionManager"/> implementation shared by every ADO.NET-based connection
    /// manager: connection lifecycle, command execution, transactions, and the retry/clone/dispose
    /// machinery. Derived classes provide the ADO.NET <typeparamref name="TConnection"/> type and the
    /// engine-specific quoting, culture, and bulk-insert behavior.
    /// </summary>
    /// <typeparam name="TConnection">The ADO.NET connection type for this database engine.</typeparam>
    [PublicAPI]
    [DebuggerDisplay("{ConnectionManagerType}:{ConnectionString}")]
    [MustDisposeResource]
    public abstract class DbConnectionManager<TConnection> : IConnectionManager
        where TConnection : class, IDbConnection, new()
    {
        /// <inheritdoc />
        public abstract ConnectionManagerType ConnectionManagerType { get; }

        /// <inheritdoc />
        public int MaxLoginAttempts { get; set; } = 3;

        /// <inheritdoc />
        public virtual bool LeaveOpen
        {
            get => _leaveOpen || Transaction != null;
            set => _leaveOpen = value;
        }

        /// <inheritdoc />
        /// <remarks>
        /// This base implementation always reports <see langword="false"/> and throws on set; derived
        /// classes may override to support it.
        /// </remarks>
        public bool IsInBulkInsert
        {
            get => false;
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public IDbConnectionString ConnectionString { get; set; }

        /// <summary>
        /// The underlying ADO.NET connection, or <see langword="null"/> until <see cref="Open"/> has
        /// been called.
        /// </summary>
        [CanBeNull]
        protected TConnection DbConnection { get; set; }

        /// <inheritdoc />
        public ConnectionState? State => DbConnection?.State;

        /// <inheritdoc />
        [CanBeNull]
        public IDbTransaction Transaction { get; set; }

        private bool _leaveOpen;

        /// <inheritdoc />
        public abstract string QB { get; }

        /// <inheritdoc />
        public abstract string QE { get; }

        /// <inheritdoc />
        public abstract CultureInfo ConnectionCulture { get; }

        /// <inheritdoc />
        public virtual bool SupportDatabases { get; } = true;

        /// <inheritdoc />
        public virtual bool SupportProcedures { get; } = true;

        /// <inheritdoc />
        public virtual bool SupportSchemas { get; } = true;

        /// <inheritdoc />
        public virtual bool SupportComputedColumns { get; } = true;

        /// <summary>
        /// Creates a connection manager with no connection string set yet.
        /// </summary>
        protected DbConnectionManager() { }

        /// <summary>
        /// Creates a connection manager for the given connection string.
        /// </summary>
        /// <param name="connectionString">Connection string pointing at the database server.</param>
        protected DbConnectionManager(IDbConnectionString connectionString)
            : this()
        {
            ConnectionString = connectionString;
        }

        /// <inheritdoc />
        public void Open()
        {
            if (LeaveOpen)
            {
                DbConnection ??= new TConnection { ConnectionString = ConnectionString.Value };
            }
            else
            {
                DbConnection?.Close();
                DbConnection = new TConnection { ConnectionString = ConnectionString.Value };
            }
            if (DbConnection!.State != ConnectionState.Open)
            {
                TryOpenConnectionXTimes();
            }
        }

        private void TryOpenConnectionXTimes()
        {
            Exception firstException = null;
            for (var i = 1; i <= MaxLoginAttempts; i++)
            {
                try
                {
                    if (DbConnection!.State == ConnectionState.Open)
                    {
                        return;
                    }

                    DbConnection.Open();
                    if (DbConnection.State == ConnectionState.Open)
                    {
                        return;
                    }
                }
                catch (Exception e)
                {
                    // Keep the first (real) cause. A later attempt can surface a
                    // misleading follow-up error — e.g. ClickHouse.Ado throwing
                    // "Connection already open." on a connection whose previous
                    // Open() failed — which must not mask the original failure.
                    firstException ??= e;
                }

                if (i >= MaxLoginAttempts)
                {
                    break;
                }

                // Drop the (possibly half-opened) connection and retry with a
                // fresh one. Some providers refuse to reopen a connection whose
                // previous Open() attempt failed and do not report State as Open,
                // so reopening the same instance would loop on that error instead
                // of recovering.
                DbConnection?.Dispose();
                DbConnection = new TConnection { ConnectionString = ConnectionString.Value };

                Task.Delay(1000).Wait();
            }

            DbConnection?.Dispose();
            DbConnection = null;

            throw firstException ?? new EtlKitException("Could not connect to database!");
        }

        /// <inheritdoc />
        /// <exception cref="EtlKitException"><see cref="DbConnection"/> is <see langword="null"/> (the connection has not been opened).</exception>
        public IDbCommand CreateCommand(
            string commandText,
            IEnumerable<IQueryParameter> parameterList
        )
        {
            if (DbConnection is null)
            {
                throw new EtlKitException("Database connection is not established!");
            }

            var cmd = DbConnection.CreateCommand();
            cmd.CommandTimeout = 0;
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = commandText;
            if (parameterList != null)
            {
                foreach (var par in parameterList)
                {
                    var newPar = cmd.CreateParameter();
                    MapQueryParameterToCommandParameter(par, newPar);
                    cmd.Parameters.Add(newPar);
                }
            }
            if (Transaction?.Connection is { State: ConnectionState.Open })
                cmd.Transaction = Transaction;
            return cmd;
        }

        /// <summary>
        /// Copies <paramref name="source"/>'s name, <see cref="System.Data.DbType"/>, and value onto
        /// <paramref name="destination"/>. Derived classes override to customize per-engine parameter
        /// binding.
        /// </summary>
        /// <param name="source">The query parameter to copy from.</param>
        /// <param name="destination">The ADO.NET command parameter to copy onto.</param>
        protected virtual void MapQueryParameterToCommandParameter(
            IQueryParameter source,
            IDbDataParameter destination
        )
        {
            destination.ParameterName = source.Name;
            destination.DbType = source.DBType;
            destination.Value = source.Value;
        }

        /// <inheritdoc />
        public int ExecuteNonQuery(
            string command,
            IEnumerable<IQueryParameter> parameterList = null
        )
        {
            using var cmd = CreateCommand(command, parameterList);
            return cmd.ExecuteNonQuery();
        }

        /// <inheritdoc />
        public object ExecuteScalar(
            string command,
            IEnumerable<IQueryParameter> parameterList = null
        )
        {
            using var cmd = CreateCommand(command, parameterList);
            return cmd.ExecuteScalar();
        }

        /// <inheritdoc />
        /// <remarks>
        /// Closes the connection when the reader is disposed, unless <see cref="LeaveOpen"/> is <see
        /// langword="true"/>.
        /// </remarks>
        public IDataReader ExecuteReader(
            string command,
            IEnumerable<IQueryParameter> parameterList = null
        )
        {
            return new DisposableDataReader(
                () => CreateCommand(command, parameterList),
                LeaveOpen ? null : CommandBehavior.CloseConnection
            );
        }

        /// <inheritdoc />
        public IConnectionManager CloneIfAllowed()
        {
            return LeaveOpen ? this : Clone();
        }

        /// <inheritdoc />
        public void BeginTransaction(IsolationLevel isolationLevel)
        {
            Open();
            Transaction = DbConnection?.BeginTransaction(isolationLevel);
        }

        /// <inheritdoc />
        public void BeginTransaction() => BeginTransaction(IsolationLevel.Unspecified);

        /// <inheritdoc />
        public void CommitTransaction()
        {
            Transaction?.Commit();
            CloseTransaction();
        }

        /// <inheritdoc />
        public void RollbackTransaction()
        {
            Transaction?.Rollback();
            CloseTransaction();
        }

        /// <inheritdoc />
        public void CloseTransaction()
        {
            Transaction?.Dispose();
            Transaction = null;
            CloseIfAllowed();
        }

        /// <inheritdoc />
        public abstract void PrepareBulkInsert(string tableName);

        /// <inheritdoc />
        public abstract void CleanUpBulkInsert(string tableName);

        /// <inheritdoc />
        public abstract void BulkInsert(ITableData data, string tableName);

        /// <inheritdoc />
        public abstract void BeforeBulkInsert(string tableName);

        /// <inheritdoc />
        public abstract void AfterBulkInsert(string tableName);

        #region IDisposable Support
        private bool _disposedValue; // To detect redundant calls

        /// <summary>
        /// Disposes <see cref="Transaction"/> and <see cref="DbConnection"/> when <paramref
        /// name="disposing"/> is <see langword="true"/>; safe to call more than once.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/> rather than a finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue)
            {
                return;
            }

            if (disposing)
            {
                Transaction?.Dispose();
                Transaction = null;
                DbConnection?.Dispose();
                DbConnection = null;
            }
            _disposedValue = true;
        }

        /// <summary>
        /// Disposes the connection and transaction via <see cref="Dispose(bool)"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public void CloseIfAllowed()
        {
            if (!LeaveOpen)
            {
                Dispose();
            }
        }

        /// <inheritdoc />
        public void Close()
        {
            Dispose();
        }

        /// <inheritdoc />
        public abstract IConnectionManager Clone();

        /// <inheritdoc />
        public virtual bool IndexExists(ITask callingTask, string sql)
        {
            return new SqlTask(callingTask, sql).ExecuteScalarAsBool();
        }
        #endregion
    }
}
