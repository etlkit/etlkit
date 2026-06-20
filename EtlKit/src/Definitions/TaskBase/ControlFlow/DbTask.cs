using System.Data.Odbc;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using EtlKit.Primitives;

using EtlKit.Common.ControlFlow;
using EtlKit.ConnectionManager;

namespace EtlKit.ControlFlow
{
    [PublicAPI]
    [SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem")]
    public abstract class DbTask : GenericTask
    {
        /* Public Properties */
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
        public long Limit { get; set; } = long.MaxValue;
        public int? RowsAffected { get; private set; }
        public bool IsOdbcConnection =>
            DbConnectionManager.GetType().IsSubclassOf(typeof(DbConnectionManager<OdbcConnection>));
        public virtual bool DoXMLCommentStyle { get; set; }
        public IDbTransaction Transaction { get; set; }
        internal virtual string NameAsComment =>
            CommentStart + TaskName + CommentEnd + Environment.NewLine;
        private string CommentStart => DoXMLCommentStyle ? "<!--" : "/*";
        private string CommentEnd => DoXMLCommentStyle ? "-->" : "*/";
        public string Command
        {
            get
            {
                if (HasSql)
                    return HasName && !IsOdbcConnection ? NameAsComment + Sql : Sql;
                throw new InvalidOperationException("Empty command");
            }
        }
        public IEnumerable<QueryParameter> Parameter { get; set; }

        /* Internal/Private properties */
        [SuppressMessage(
            "Code Quality",
            "S1144:Unused private types or members should be removed",
            Justification = "Private set is reserved for future use."
        )]
        internal bool DoSkipSql { get; private set; }
        private bool HasSql => !string.IsNullOrWhiteSpace(Sql);

        /* Some constructors */
        protected DbTask() { }

        protected DbTask(string name)
            : this()
        {
            TaskName = name;
        }

        protected DbTask(string name, string sql)
            : this(name)
        {
            Sql = sql;
        }

        protected DbTask(ITask callingTask, string sql)
        {
            Sql = sql;
            CopyTaskProperties(callingTask);
        }

        protected DbTask(string name, string sql, params Action<object>[] actions)
            : this(name, sql)
        {
            Actions = actions.ToList();
        }

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

        public T? ExecuteScalar<T>()
            where T : struct
        {
            var result = ExecuteScalar();
            if (result == null || result == DBNull.Value)
                return null;
            return (T)Convert.ChangeType(result, typeof(T));
        }

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
