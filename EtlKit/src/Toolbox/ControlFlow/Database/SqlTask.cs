using EtlKit.Helper;
using EtlKit.Primitives;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Executes any sql on the database. Use ExecuteNonQuery for SQL statements returning no data, ExecuteScalar for statements with one row and one column or
    /// ExecuteReader for SQL that returns a result set.
    /// </summary>
    /// <example>
    /// <code>
    /// SqlTask.ExecuteNonQuery("Description","insert into demo.table1 select * from demo.table2");
    /// </code>
    /// </example>
    [PublicAPI]
    public class SqlTask : DbTask
    {
        public sealed override string TaskName { get; set; } = "Run some sql";

        public void Execute() => ExecuteNonQuery();

        public SqlTask() { }

        public SqlTask(string name)
        {
            TaskName = name;
        }

        public SqlTask(ITask callingTask, string sql)
            : base(callingTask, sql) { }

        public SqlTask(string name, string sql)
            : base(name, sql) { }

        public SqlTask(string name, string sql, IEnumerable<QueryParameter> parameter)
            : base(name, sql)
        {
            Parameter = parameter;
        }

        public SqlTask(string name, string sql, params Action<object>[] actions)
            : base(name, sql, actions) { }

        public SqlTask(
            string name,
            string sql,
            IEnumerable<QueryParameter> parameterList,
            params Action<object>[] actions
        )
            : base(name, sql, actions)
        {
            Parameter = parameterList;
        }

        public SqlTask(
            string name,
            string sql,
            Action beforeRowReadAction,
            Action afterRowReadAction,
            params Action<object>[] actions
        )
            : base(name, sql, beforeRowReadAction, afterRowReadAction, actions) { }

        public SqlTask(
            string name,
            string sql,
            IEnumerable<QueryParameter> parameter,
            Action beforeRowReadAction,
            Action afterRowReadAction,
            params Action<object>[] actions
        )
            : base(name, sql, beforeRowReadAction, afterRowReadAction, actions)
        {
            Parameter = parameter;
        }

        public static int ExecuteNonQuery(string name, string sql) =>
            new SqlTask(name, sql).ExecuteNonQuery();

        public static int ExecuteNonQuery(
            IConnectionManager connectionManager,
            string name,
            string sql
        ) => new SqlTask(name, sql) { ConnectionManager = connectionManager }.ExecuteNonQuery();

        public static int ExecuteNonQuery(
            string name,
            string sql,
            IEnumerable<QueryParameter> parameterList
        ) => new SqlTask(name, sql, parameterList).ExecuteNonQuery();

        public static int ExecuteNonQuery(
            IConnectionManager connectionManager,
            string name,
            string sql,
            IEnumerable<QueryParameter> parameterList
        ) =>
            new SqlTask(name, sql, parameterList)
            {
                ConnectionManager = connectionManager
            }.ExecuteNonQuery();

        /// <summary>
        /// Take interpolated string as argument and apply formatting
        /// </summary>
        /// <param name="connectionManager"></param>
        /// <param name="name"></param>
        /// <param name="sql"></param>
        /// <returns></returns>
        public static int ExecuteNonQueryFormatted(
            IConnectionManager connectionManager,
            string name,
            FormattableString sql
        ) =>
            new SqlTask(name, connectionManager.FormatQuery(sql))
            {
                ConnectionManager = connectionManager
            }.ExecuteNonQuery();

        public static object ExecuteScalar(string name, string sql) =>
            new SqlTask(name, sql).ExecuteScalar();

        public static object ExecuteScalar(
            IConnectionManager connectionManager,
            string name,
            string sql
        ) => new SqlTask(name, sql) { ConnectionManager = connectionManager }.ExecuteScalar();

        public static T? ExecuteScalar<T>(string name, string sql)
            where T : struct => new SqlTask(name, sql).ExecuteScalar<T>();

        public static T? ExecuteScalar<T>(
            IConnectionManager connectionManager,
            string name,
            string sql
        )
            where T : struct =>
            new SqlTask(name, sql) { ConnectionManager = connectionManager }.ExecuteScalar<T>();

        public static object ExecuteScalar(
            string name,
            string sql,
            IEnumerable<QueryParameter> parameterList
        ) => new SqlTask(name, sql, parameterList).ExecuteScalar();

        public static object ExecuteScalar(
            IConnectionManager connectionManager,
            string name,
            string sql,
            IEnumerable<QueryParameter> parameterList
        ) =>
            new SqlTask(name, sql, parameterList)
            {
                ConnectionManager = connectionManager
            }.ExecuteScalar();

        public static T? ExecuteScalar<T>(
            string name,
            string sql,
            IEnumerable<QueryParameter> parameterList
        )
            where T : struct => new SqlTask(name, sql, parameterList).ExecuteScalar<T>();

        public static bool ExecuteScalarAsBool(string name, string sql) =>
            new SqlTask(name, sql).ExecuteScalarAsBool();

        public static bool ExecuteScalarAsBool(
            IConnectionManager connectionManager,
            string name,
            string sql
        ) => new SqlTask(name, sql) { ConnectionManager = connectionManager }.ExecuteScalarAsBool();

        public static bool ExecuteScalarAsBool(
            string name,
            string sql,
            IEnumerable<QueryParameter> parameterList
        ) => new SqlTask(name, sql, parameterList).ExecuteScalarAsBool();

        public static void ExecuteReader(
            string name,
            string sql,
            params Action<object>[] actions
        ) => new SqlTask(name, sql, actions).ExecuteReader();

        public static void ExecuteReader(
            IConnectionManager connectionManager,
            string name,
            string sql,
            params Action<object>[] actions
        ) =>
            new SqlTask(name, sql, actions)
            {
                ConnectionManager = connectionManager
            }.ExecuteReader();

        public static void ExecuteReader(
            string name,
            string sql,
            Action beforeRowReadAction,
            Action afterRowReadAction,
            params Action<object>[] actions
        ) =>
            new SqlTask(
                name,
                sql,
                beforeRowReadAction,
                afterRowReadAction,
                actions
            ).ExecuteReader();

        public static void ExecuteReader(
            IConnectionManager connectionManager,
            string name,
            string sql,
            Action beforeRowReadAction,
            Action afterRowReadAction,
            params Action<object>[] actions
        ) =>
            new SqlTask(name, sql, beforeRowReadAction, afterRowReadAction, actions)
            {
                ConnectionManager = connectionManager
            }.ExecuteReader();

        public static void ExecuteReader(
            string name,
            string sql,
            IEnumerable<QueryParameter> parameterList,
            params Action<object>[] actions
        ) => new SqlTask(name, sql, parameterList, actions).ExecuteReader();

        public static void ExecuteReader(
            IConnectionManager connectionManager,
            string name,
            string sql,
            IEnumerable<QueryParameter> parameterList,
            params Action<object>[] actions
        ) =>
            new SqlTask(name, sql, parameterList, actions)
            {
                ConnectionManager = connectionManager
            }.ExecuteReader();

        public static void ExecuteReader(
            string name,
            string sql,
            IEnumerable<QueryParameter> parameterList,
            Action beforeRowReadAction,
            Action afterRowReadAction,
            params Action<object>[] actions
        ) =>
            new SqlTask(
                name,
                sql,
                parameterList,
                beforeRowReadAction,
                afterRowReadAction,
                actions
            ).ExecuteReader();

        public static void ExecuteReaderSingleLine(
            string name,
            string sql,
            IEnumerable<QueryParameter> parameterList,
            params Action<object>[] actions
        ) => new SqlTask(name, sql, parameterList, actions) { Limit = 1 }.ExecuteReader();

        public static void ExecuteReaderSingleLine(
            IConnectionManager connectionManager,
            string name,
            string sql,
            IEnumerable<QueryParameter> parameterList,
            params Action<object>[] actions
        ) =>
            new SqlTask(name, sql, parameterList, actions)
            {
                ConnectionManager = connectionManager,
                Limit = 1
            }.ExecuteReader();

        public static void ExecuteReaderSingleLine(
            string name,
            string sql,
            params Action<object>[] actions
        ) => new SqlTask(name, sql, actions) { Limit = 1 }.ExecuteReader();

        public static void ExecuteReaderSingleLine(
            IConnectionManager connectionManager,
            string name,
            string sql,
            params Action<object>[] actions
        ) =>
            new SqlTask(name, sql, actions)
            {
                ConnectionManager = connectionManager,
                Limit = 1
            }.ExecuteReader();

        public static void BulkInsert(string name, ITableData data, string tableName) =>
            new SqlTask(name).BulkInsert(data, tableName);

        public static void BulkInsert(
            IConnectionManager connectionManager,
            string name,
            ITableData data,
            string tableName
        ) =>
            new SqlTask(name) { ConnectionManager = connectionManager }.BulkInsert(data, tableName);
    }
}
