using EtlKit.ClickHouse.ConnectionManager;
using EtlKit.ConnectionManager;
using EtlKit.Primitives;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks
{
    [Collection(nameof(ControlFlowCollection))]
    public class ControlFlowTestBase
    {
        private readonly ControlFlowDatabaseFixture _fixture;
        private static string ConfigSection => ControlFlowDatabaseFixture.ConfigSection;

        protected ControlFlowTestBase(ControlFlowDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        protected static MySqlConnectionManager MySqlConnection =>
            Config.MySqlConnection.ConnectionManager(ConfigSection);

        protected static PostgresConnectionManager PostgresConnection =>
            Config.PostgresConnection.ConnectionManager(ConfigSection);

        public static SqlConnectionManager SqlConnection =>
            Config.SqlConnection.ConnectionManager(ConfigSection);

        public static ClickHouseConnectionManager ClickHouseConnection =>
            Config.ClickHouseConnection.ConnectionManager(ConfigSection);

        public static AdomdConnectionManager AdomdConnection =>
            new(Config.SSASConnection.ConnectionString(ConfigSection).CloneWithoutDbName());

        protected SQLiteConnectionManager SqliteConnection =>
            Config.SQLiteConnection.ConnectionManager(ConfigSection, _fixture.SQLiteDbSuffix);

        public static TheoryData<IConnectionManager> AccessConnection =>
            new(Config.AccessConnection(ConfigSection));

        public static TheoryData<IConnectionManager> AllSqlConnections =>
            new(Config.AllSqlConnections(ConfigSection));

        public static TheoryData<IConnectionManager> AllConnectionsWithoutSQLite =>
            new(Config.AllConnectionsWithoutSQLite(ConfigSection));

        public static TheoryData<IConnectionManager> AllConnectionsWithoutClickHouse =>
            new(Config.AllConnectionsWithoutClickHouse(ConfigSection));

        public static TheoryData<IConnectionManager> AllConnectionsWithoutSQLiteAndClickHouse =>
            new(Config.AllConnectionsWithoutSQLiteAndClickHouse(ConfigSection));

        public static TheoryData<string> DbConnectionTypesWithMaster() =>
            ["ClickHouse", "SqlServer", "Postgres", "MySql"];

        public static ClickHouseConnectionManager CreateClickHouseConnectionManager() =>
            new(
                Config.ClickHouseConnection.ConnectionString(ConfigSection).CloneWithMasterDbName()
            );

        public static SqlConnectionManager CreateSqlConnectionManager() =>
            new(Config.SqlConnection.ConnectionString(ConfigSection).CloneWithMasterDbName());

        public static PostgresConnectionManager CreatePostgresConnectionManager() =>
            new(Config.PostgresConnection.ConnectionString(ConfigSection).CloneWithMasterDbName());

        public static MySqlConnectionManager CreateMySqlConnectionManager() =>
            new(Config.MySqlConnection.ConnectionString(ConfigSection).CloneWithMasterDbName());

        public static IConnectionManager CreateConnectionManager(string dbType) =>
            dbType switch
            {
                "ClickHouse" => CreateClickHouseConnectionManager(),
                "SqlServer" => CreateSqlConnectionManager(),
                "Postgres" => CreatePostgresConnectionManager(),
                "MySql" => CreateMySqlConnectionManager(),
                _ => throw new ArgumentException($"Unknown database type: {dbType}"),
            };
    }
}
