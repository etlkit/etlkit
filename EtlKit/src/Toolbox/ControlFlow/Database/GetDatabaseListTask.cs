using EtlKit.Primitives;

using EtlKit.Common.ControlFlow;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Returns a list of all user databases on the server. Make sure to connect with the correct permissions!
    /// In MySql, this will return a list of all schemas.
    /// </summary>
    /// <example>
    /// <code>
    /// GetDatabaseListTask.List();
    /// </code>
    /// </example>
    [PublicAPI]
    public class GetDatabaseListTask : GenericTask
    {
        /* ITask Interface */
        public override string TaskName => "Get names of all databases";

        public void Execute()
        {
            if (!DbConnectionManager.SupportDatabases)
                throw new ETLBoxNotSupportedException("This task is not supported!");

            DatabaseNames = [];
            new SqlTask(this, GetSql())
            {
                Actions = [name => DatabaseNames.Add((string)name)],
            }.ExecuteReader();

            if (ConnectionType == ConnectionManagerType.MySql)
                DatabaseNames.RemoveAll(m =>
                    new List<string>
                    {
                        "information_schema",
                        "mysql",
                        "performance_schema",
                        "sys",
                    }.Contains(m)
                );
        }

        public List<string> DatabaseNames { get; set; }

        public string GetSql()
        {
            return ConnectionType switch
            {
                ConnectionManagerType.SqlServer =>
                    "SELECT [name] FROM master.dbo.sysdatabases WHERE dbid > 4",
                ConnectionManagerType.MySql => "SHOW DATABASES",
                ConnectionManagerType.Postgres =>
                    "SELECT datname FROM pg_database WHERE datistemplate=false",
                ConnectionManagerType.ClickHouse => "SHOW DATABASES",
                _ => throw new ETLBoxNotSupportedException("This database is not supported!"),
            };
        }

        public GetDatabaseListTask GetList()
        {
            Execute();
            return this;
        }

        public static List<string> List() => new GetDatabaseListTask().GetList().DatabaseNames;

        public static List<string> List(IConnectionManager connectionManager) =>
            new GetDatabaseListTask { ConnectionManager = connectionManager }
                .GetList()
                .DatabaseNames;
    }
}
