using System.Data.Odbc;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using EtlKit.Common.ControlFlow;
using EtlKit.ConnectionManager;
using EtlKit.Primitives;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Base class for control flow tasks that execute a single SQL command: DDL/DML statements,
    /// scalar/reader queries, and bulk inserts, with row-level callback hooks and logging.
    /// </summary>
    [PublicAPI]
    [SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem")]
    public abstract class DbTask : GenericTask
    {
        /* Public Properties */
        /// <summary>
        /// The SQL command text. See <see cref="Command"/> for the version actually sent to the
        /// database (which may be prefixed with a name comment).
        /// </summary>
        public string Sql { get; set; }

        /// <summary>
        /// Optional action hooks to be performed on each column of returned dataset (precisely one action per column)
        /// </summary>
        [CanBeNull]
        public List<Action<object>> Actions { get; set; }

        /// <summary>
        /// Optional action hooks to be performed on each row before <see cref="Actions"/>
        /// </summary>
        [CanBeNull]
        public Action BeforeRowReadAction { get; set; }

        /// <summary>
        /// Optional action hooks to be performed on each row after <see cref="Actions"/>
        /// </summary>
        [CanBeNull]
        public Action AfterRowReadAction { get; set; }

        /// <summary>
        /// Maximum number of rows <see cref="ExecuteReader"/> will process. Defaults to unlimited.
        /// </summary>
        public long Limit { get; set; } = long.MaxValue;

        /// <summary>
        /// Number of rows affected by the last <see cref="ExecuteNonQuery"/> or <see
        /// cref="BulkInsert"/> call, or <see langword="null"/> before either has run.
        /// </summary>
        public int? RowsAffected { get; private set; }

        /// <summary>
        /// Whether <see cref="EtlKit.Common.ControlFlow.GenericTask.DbConnectionManager"/> is an ODBC
        /// connection manager.
        /// </summary>
        public bool IsOdbcConnection =>
            DbConnectionManager.GetType().IsSubclassOf(typeof(DbConnectionManager<OdbcConnection>));

        /// <summary>
        /// When <see langword="true"/>, <see cref="NameAsComment"/> wraps the task name in an XML
        /// comment (<c>&lt;!-- --&gt;</c>) instead of a SQL comment (<c>/* */</c>).
        /// </summary>
        public virtual bool DoXMLCommentStyle { get; set; }

        /// <summary>
        /// The transaction this task executes within, or <see langword="null"/> to run outside a transaction.
        /// </summary>
        public IDbTransaction Transaction { get; set; }

        /// <summary>
        /// <see cref="EtlKit.Primitives.ITask.TaskName"/> wrapped as a SQL or XML comment (per <see
        /// cref="DoXMLCommentStyle"/>), prefixed onto <see cref="Command"/> for named tasks.
        /// </summary>
        internal virtual string NameAsComment =>
            CommentStart + TaskName + CommentEnd + Environment.NewLine;
        private string CommentStart => DoXMLCommentStyle ? "<!--" : "/*";
        private string CommentEnd => DoXMLCommentStyle ? "-->" : "*/";

        /// <summary>
        /// The command text actually sent to the database: <see cref="Sql"/>, prefixed with <see
        /// cref="NameAsComment"/> when the task has a name and the connection is not ODBC (some ODBC
        /// drivers reject comments).
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="Sql"/> is empty or whitespace.</exception>
        public string Command
        {
            get
            {
                if (HasSql)
                    return HasName && !IsOdbcConnection ? NameAsComment + Sql : Sql;
                throw new InvalidOperationException("Empty command");
            }
        }

        /// <summary>
        /// Parameters bound to <see cref="Sql"/>.
        /// </summary>
        public IEnumerable<QueryParameter> Parameter { get; set; }

        /* Internal/Private properties */
        /// <summary>
        /// When <see langword="true"/>, execution methods skip actually running <see cref="Command"/>
        /// (used by callers that only need side effects like row callbacks without sending SQL).
        /// </summary>
        [SuppressMessage(
            "Code Quality",
            "S1144:Unused private types or members should be removed",
            Justification = "Private set is reserved for future use."
        )]
        internal bool DoSkipSql { get; private set; }
        private bool HasSql => !string.IsNullOrWhiteSpace(Sql);

        /* Some constructors */
        /// <summary>
        /// Creates a new instance with no SQL set yet.
        /// </summary>
        protected DbTask() { }

        /// <summary>
        /// Creates a new instance with the given task name.
        /// </summary>
        /// <param name="name">Task name.</param>
        protected DbTask(string name)
            : this()
        {
            TaskName = name;
        }

        /// <summary>
        /// Creates a new instance with the given task name and SQL command.
        /// </summary>
        /// <param name="name">Task name.</param>
        /// <param name="sql">SQL command text.</param>
        protected DbTask(string name, string sql)
            : this(name)
        {
            Sql = sql;
        }

        /// <summary>
        /// Creates a new instance with the given SQL command, copying identity and logging settings
        /// from <paramref name="callingTask"/>.
        /// </summary>
        /// <param name="callingTask">The task to copy properties from.</param>
        /// <param name="sql">SQL command text.</param>
        protected DbTask(ITask callingTask, string sql)
        {
            Sql = sql;
            CopyTaskProperties(callingTask);
        }

        /// <summary>
        /// Creates a new instance with the given task name, SQL command, and per-column action hooks.
        /// </summary>
        /// <param name="name">Task name.</param>
        /// <param name="sql">SQL command text.</param>
        /// <param name="actions">One action per column of the returned dataset, applied by <see cref="ExecuteReader"/>.</param>
        protected DbTask(string name, string sql, params Action<object>[] actions)
            : this(name, sql)
        {
            Actions = actions.ToList();
        }

        /// <summary>
        /// Creates a new instance with the given task name, SQL command, row-level hooks, and
        /// per-column action hooks.
        /// </summary>
        /// <param name="name">Task name.</param>
        /// <param name="sql">SQL command text.</param>
        /// <param name="beforeRowReadAction">Action run before each row's columns are processed.</param>
        /// <param name="afterRowReadAction">Action run after each row's columns are processed.</param>
        /// <param name="actions">One action per column of the returned dataset, applied by <see cref="ExecuteReader"/>.</param>
        protected DbTask(
            string name,
            string sql,
            Action beforeRowReadAction,
            Action afterRowReadAction,
            params Action<object>[] actions
        )
            : this(name, sql)
        {
            BeforeRowReadAction = beforeRowReadAction;
            AfterRowReadAction = afterRowReadAction;
            Actions = actions.ToList();
        }

        /* Public methods */
        /// <summary>
        /// Executes <see cref="Command"/> and returns the number of rows affected, also storing it in
        /// <see cref="RowsAffected"/>.
        /// </summary>
        public int ExecuteNonQuery()
        {
            var conn = DbConnectionManager.CloneIfAllowed();
            try
            {
                conn.Open();
                if (!DisableLogging)
                    LoggingStart();
                RowsAffected = DoSkipSql ? 0 : conn.ExecuteNonQuery(Command, Parameter);
                if (!DisableLogging)
                    LoggingEnd(LogType.Rows);
            }
            finally
            {
                conn.CloseIfAllowed();
            }
            return RowsAffected.GetValueOrDefault();
        }

        /// <summary>
        /// Executes <see cref="Command"/> and returns the first column of the first row of the result.
        /// </summary>
        public object ExecuteScalar()
        {
            object result;
            var conn = DbConnectionManager.CloneIfAllowed();
            try
            {
                conn.Open();
                if (!DisableLogging)
                    LoggingStart();
                result = conn.ExecuteScalar(Command, Parameter);
                if (!DisableLogging)
                    LoggingEnd();
            }
            finally
            {
                conn.CloseIfAllowed();
            }
            return result;
        }

        /// <summary>
        /// Executes <see cref="Command"/> and converts the scalar result to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The value type to convert the result to.</typeparam>
        /// <returns>The converted value, or <see langword="null"/> if the result was <see langword="null"/> or <see cref="DBNull"/>.</returns>
        public T? ExecuteScalar<T>()
            where T : struct
        {
            var result = ExecuteScalar();
            if (result == null || result == DBNull.Value)
                return null;
            return (T)Convert.ChangeType(result, typeof(T));
        }

        /// <summary>
        /// Executes <see cref="Command"/> and interprets the scalar result as a boolean: <see
        /// langword="false"/> for <see langword="null"/>, a non-positive integer, or anything other
        /// than the string <c>"true"</c> (case-insensitive); <see langword="true"/> otherwise.
        /// </summary>
        public bool ExecuteScalarAsBool()
        {
            var result = ExecuteScalar();
            return ObjectToBool(result);
        }

        private static bool ObjectToBool(object result)
        {
            if (result == null)
                return false;
            if (int.TryParse(result.ToString(), out var number) && number > 0)
                return true;
            if (result.ToString().Trim().Equals("true", StringComparison.CurrentCultureIgnoreCase))
                return true;
            return false;
        }

        /// <summary>
        /// Executes <see cref="Command"/> and, for up to <see cref="Limit"/> rows, invokes <see
        /// cref="BeforeRowReadAction"/>, then each of <see cref="Actions"/> (one per column), then
        /// <see cref="AfterRowReadAction"/>.
        /// </summary>
        public void ExecuteReader()
        {
            var conn = DbConnectionManager.CloneIfAllowed();
            try
            {
                conn.Open();
                if (!DisableLogging)
                    LoggingStart();
                using (IDataReader reader = conn.ExecuteReader(Command, Parameter))
                {
                    for (var rowNr = 0; rowNr < Limit; rowNr++)
                    {
                        if (reader.Read())
                        {
                            ProcessCurrentRow(reader);
                        }
                        else
                        {
                            // That bug on ClickHouseDataReader, by default does not proceed to correct Result
                            // https://github.com/killwort/clickhouse-net/issues/68
                            if (HandleClickHouseError(conn, reader))
                            {
                                continue;
                            }

                            break;
                        }
                    }
                }

                if (!DisableLogging)
                    LoggingEnd();
            }
            finally
            {
                conn.CloseIfAllowed();
            }
        }

        /// <summary>
        /// Bulk-loads <paramref name="data"/> into <paramref name="tableName"/> via the connection
        /// manager's native bulk insert mechanism, storing the affected row count in <see
        /// cref="RowsAffected"/>.
        /// </summary>
        /// <param name="data">Row-by-row source data to insert.</param>
        /// <param name="tableName">Destination table name.</param>
        public void BulkInsert(ITableData data, string tableName)
        {
            var conn = DbConnectionManager.CloneIfAllowed();
            try
            {
                conn.Open();
                if (!DisableLogging)
                    LoggingStart(LogType.Bulk);
                conn.BeforeBulkInsert(tableName);
                conn.BulkInsert(data, tableName);
                conn.AfterBulkInsert(tableName);
                RowsAffected = data.RecordsAffected;
                if (!DisableLogging)
                    LoggingEnd(LogType.Bulk);
            }
            finally
            {
                conn.CloseIfAllowed();
            }
        }

        /* Private implementation & stuff */
        private enum LogType
        {
            None,
            Rows,
            Bulk,
        }

        private void LoggingStart(LogType logType = LogType.None)
        {
            Logger.Info(
                TaskName,
                TaskType,
                "START",
                TaskHash,
                Common.ControlFlow.ControlFlow.Stage,
                Common.ControlFlow.ControlFlow.CurrentLoadProcess?.Id
            );
            Logger.Debug(
                logType == LogType.Bulk ? "SQL Bulk Insert" : $"{Command}",
                TaskType,
                "RUN",
                TaskHash,
                Common.ControlFlow.ControlFlow.Stage,
                Common.ControlFlow.ControlFlow.CurrentLoadProcess?.Id
            );
        }

        private void LoggingEnd(LogType logType = LogType.None)
        {
            Logger.Info(
                TaskName,
                TaskType,
                "END",
                TaskHash,
                Common.ControlFlow.ControlFlow.Stage,
                Common.ControlFlow.ControlFlow.CurrentLoadProcess?.Id
            );
            if (logType == LogType.Rows)
                Logger.Debug(
                    $"Rows affected: {RowsAffected ?? 0}",
                    TaskType,
                    "RUN",
                    TaskHash,
                    Common.ControlFlow.ControlFlow.Stage,
                    Common.ControlFlow.ControlFlow.CurrentLoadProcess?.Id
                );
        }

        private void ProcessCurrentRow(IDataReader reader)
        {
            BeforeRowReadAction?.Invoke();
            for (var i = 0; i < Actions?.Count; i++)
            {
                Actions[i].Invoke(!reader.IsDBNull(i) ? reader.GetValue(i) : null);
            }
            AfterRowReadAction?.Invoke();
        }

        private static bool HandleClickHouseError(IConnectionManager conn, IDataReader reader)
        {
            return conn.ConnectionManagerType == ConnectionManagerType.ClickHouse
                && reader.NextResult();
        }
    }
}
