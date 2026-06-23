using EtlKit.ClickHouse.ConnectionManager;
using EtlKit.ConnectionManager;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;
using EtlKit.TestShared;
using EtlKit.TestShared.Helper;

namespace EtlKit.TestDatabaseConnectors
{
    [CollectionDefinition(
        nameof(DataFlowSourceDestinationCollection),
        DisableParallelization = false
    )]
    public class DataFlowSourceDestinationCollection
        : ICollectionFixture<DatabaseSourceDestinationFixture> { }

    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public abstract class DatabaseConnectorsTestBase : IDisposable
    {
        private const string SourceConfigSection =
            DatabaseSourceDestinationFixture.SourceConfigSection;

        private const string OtherConfigSection =
            DatabaseSourceDestinationFixture.OtherConfigSection;

        protected readonly DatabaseSourceDestinationFixture Fixture;
        private readonly string _sqLiteDbSuffix;

        protected DatabaseConnectorsTestBase(DatabaseSourceDestinationFixture fixture)
        {
            Fixture = fixture;
            _sqLiteDbSuffix = Fixture.GetSQLiteDbSuffix();
        }

        protected static SqlConnectionManager SqlConnection =>
            Config.SqlConnection.ConnectionManager(SourceConfigSection);

        protected static AccessOdbcConnectionManager AccessOdbcConnection =>
            Config.AccessOdbcConnection.ConnectionManager(OtherConfigSection);

        protected static SqlConnectionManager AzureSqlConnection =>
            Config.AzureSqlConnection.ConnectionManager(OtherConfigSection);

        public static TheoryData<IConnectionManager> AllSqlConnections =>
            new(Config.AllSqlConnections(SourceConfigSection));

        public static TheoryData<ConnectionManagerWithPK> AllSqlConnectionsWithPK =>
            new(
                Config
                    .AllSqlConnections(SourceConfigSection)
                    .Select(r => new ConnectionManagerWithPK(r))
            );

        public static TheoryData<IConnectionManager> AllConnectionsWithoutClickHouse =>
            new(Config.AllConnectionsWithoutClickHouse(SourceConfigSection));

        public static TheoryData<ConnectionManagerWithPK> AllConnectionsWithoutClickHouseWithPK =>
            new(
                Config
                    .AllConnectionsWithoutClickHouse(SourceConfigSection)
                    .Select(r => new ConnectionManagerWithPK(r))
            );

        protected static IEnumerable<CultureInfo> AllLocalCultures => Config.AllLocalCultures();

        public static TheoryData<IConnectionManager> AllConnectionsWithoutSQLite =>
            new(Config.AllConnectionsWithoutSQLiteAndClickHouse(SourceConfigSection));

        public static TheoryData<IConnectionManager> AllOdbcConnections =>
            new(Config.AllOdbcConnections(OtherConfigSection));

        public static TheoryData<Type, Type> MixedSourceDestinations()
        {
            var data = new TheoryData<Type, Type>
            {
                //Same DB
                { typeof(SqlConnectionManager), typeof(SqlConnectionManager) },
                { typeof(SQLiteConnectionManager), typeof(SQLiteConnectionManager) },
                { typeof(MySqlConnectionManager), typeof(MySqlConnectionManager) },
                { typeof(PostgresConnectionManager), typeof(PostgresConnectionManager) },
                { typeof(ClickHouseConnectionManager), typeof(ClickHouseConnectionManager) },
                //Mixed
                { typeof(SqlConnectionManager), typeof(SQLiteConnectionManager) },
                { typeof(SQLiteConnectionManager), typeof(SqlConnectionManager) },
                { typeof(MySqlConnectionManager), typeof(PostgresConnectionManager) },
                { typeof(SqlConnectionManager), typeof(PostgresConnectionManager) },
                { typeof(PostgresConnectionManager), typeof(ClickHouseConnectionManager) },
                { typeof(ClickHouseConnectionManager), typeof(PostgresConnectionManager) },
            };
            return data;
        }

        [MustDisposeResource]
        protected IConnectionManager GetConnectionManager(Type connectionType, string configSection)
        {
            if (
                !connectionType.IsClass
                || !connectionType.IsAssignableTo(typeof(IConnectionManager))
            )
                throw new ArgumentException(
                    $"Type {connectionType.Name} must be a subclass of IConnectionManager!"
                );
            return connectionType.Name switch
            {
                nameof(SQLiteConnectionManager) => Config.SQLiteConnection.ConnectionManager(
                    configSection,
                    _sqLiteDbSuffix
                ),
                nameof(SqlConnectionManager) => Config.SqlConnection.ConnectionManager(
                    configSection
                ),
                nameof(PostgresConnectionManager) => Config.PostgresConnection.ConnectionManager(
                    configSection
                ),
                nameof(MySqlConnectionManager) => Config.MySqlConnection.ConnectionManager(
                    configSection
                ),
                nameof(ClickHouseConnectionManager) =>
                    Config.ClickHouseConnection.ConnectionManager(configSection),
                _ => throw new ArgumentOutOfRangeException(nameof(connectionType)),
            };
        }

        protected static bool IsIdentitySupported(IConnectionManager connection)
        {
            return connection.ConnectionManagerType != ConnectionManagerType.ClickHouse;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }
            Fixture?.DisposeSqliteDb(_sqLiteDbSuffix);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DatabaseConnectorsTestBase()
        {
            Dispose(false);
        }
    }
}
