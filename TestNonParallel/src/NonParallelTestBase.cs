using EtlKit.ConnectionManager;
using EtlKit.Primitives;
using EtlKit.TestNonParallel.Fixtures;
using EtlKit.TestShared.Helper;

namespace EtlKit.TestNonParallel
{
    [CollectionDefinition("Logging")]
    public class LoggingCollectionClass : ICollectionFixture<LoggingDatabaseFixture> { }

    [Collection("Logging")]
    public class NonParallelTestBase
    {
        protected LoggingDatabaseFixture Fixture;

        public NonParallelTestBase(LoggingDatabaseFixture fixture)
        {
            Fixture = fixture;
        }

        protected static SqlConnectionManager SqlConnection =>
            Config.SqlConnection.ConnectionManager("Logging");

        public static TheoryData<IConnectionManager> AllSqlConnections =>
            new(Config.AllSqlConnections("Logging"));

        public static TheoryData<IConnectionManager> AllSqlConnectionsWithoutClickHouse =>
            new(Config.AllConnectionsWithoutClickHouse("Logging"));
    }
}
