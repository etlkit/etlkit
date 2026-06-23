using System;
using EtlKit.Common;
using EtlKit.Primitives;
using NLog.Layouts;
using NLog.Targets;

namespace EtlKit.Logging.Database;

internal sealed class CreateDatabaseTarget
{
    private ObjectNameDescriptor TN =>
        new(LogTableName, ConnectionManager.QB, ConnectionManager.QE);

    private string QB => ConnectionManager.QB;
    private string QE => ConnectionManager.QE;

    private string CommandText =>
        $@"
INSERT INTO {TN.QuotedFullName}
    ( {QB}log_date{QE}, {QB}level{QE}, {QB}stage{QE}, {QB}message{QE}, {QB}task_type{QE}, {QB}task_action{QE}, {QB}task_hash{QE}, {QB}source{QE}, {QB}load_process_id{QE})
SELECT {LogDate}
    , @Level
    , CAST(@Stage as {Varchar}(20))
    , CAST(@Message as {Varchar}(4000))
    , CAST(@Type as {Varchar}(40))
    , @Action
    , @Hash
    , CAST(@Logger as {Varchar}(20))
    , CASE WHEN @LoadProcessKey IS NULL OR @LoadProcessKey = '' OR @LoadProcessKey = '0'
           THEN NULL
           ELSE CAST(@LoadProcessKey AS {Int}) END 
";

    private string LogDate
    {
        get
        {
            return ConnectionManager.ConnectionManagerType switch
            {
                ConnectionManagerType.SqlServer
                or ConnectionManagerType.MySql
                    => "CAST(@LogDate AS DATETIME)",
                ConnectionManagerType.Postgres => "@LogDate::TIMESTAMP",
                _ => "@LogDate"
            };
        }
    }

    private string Varchar =>
        ConnectionManager.ConnectionManagerType switch
        {
            ConnectionManagerType.MySql => "CHAR",
            _ => "VARCHAR"
        };

    private string Int =>
        ConnectionManager.ConnectionManagerType switch
        {
            ConnectionManagerType.MySql => "UNSIGNED",
            ConnectionManagerType.ClickHouse => "Nullable(INT)",
            _ => "INT"
        };

    private IConnectionManager ConnectionManager { get; set; }
    private string LogTableName { get; set; }

    public CreateDatabaseTarget(IConnectionManager connectionManager, string logTableName)
    {
        ConnectionManager = connectionManager;
        LogTableName = logTableName;
    }

    public DatabaseTarget GetNLogDatabaseTarget()
    {
        var dbTarget = new DatabaseTarget();
        AddParameter(dbTarget, "LogDate", @"${date:format=yyyy-MM-dd HH\:mm\:ss.fff}");
        AddParameter(dbTarget, "Level", @"${level}");
        AddParameter(dbTarget, "Stage", @"${event-properties:item=Stage}");
        AddParameter(dbTarget, "Message", @"${message}");
        AddParameter(dbTarget, "Type", @"${event-properties:item=Type}");
        AddParameter(dbTarget, "Action", @"${event-properties:item=Action}");
        AddParameter(dbTarget, "Hash", @"${event-properties:item=Hash}");
        AddParameter(dbTarget, "LoadProcessKey", @"${event-properties:item=LoadProcessKey}");
        AddParameter(dbTarget, "Logger", @"ETL");

        dbTarget.CommandText = new SimpleLayout(CommandText);
        dbTarget.DBProvider = ConnectionManager.ConnectionManagerType switch
        {
            ConnectionManagerType.Postgres => "Npgsql.NpgsqlConnection, Npgsql",
            ConnectionManagerType.MySql => "MySql.Data.MySqlClient.MySqlConnection, MySql.Data",
            ConnectionManagerType.SQLite
                => "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite",
            ConnectionManagerType.SqlServer
                => "Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlClient",
            ConnectionManagerType.ClickHouse
                => "ClickHouse.Ado.ClickHouseConnection, ClickHouse.Ado",
            _ => throw new NotSupportedException("Only SQL databases are supported for logs")
        };
        dbTarget.ConnectionString = ConnectionManager.ConnectionString.Value;
        return dbTarget;
    }

    private static void AddParameter(DatabaseTarget dbTarget, string parameterName, string layout)
    {
        var par = new DatabaseParameterInfo(parameterName, new SimpleLayout(layout));
        dbTarget.Parameters.Add(par);
    }
}
