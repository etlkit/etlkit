using EtlKit.ConnectionManager;
using EtlKit.TestPerformance.ETLBoxTests.Performance.Fixtures;
using EtlKit.TestShared.Helper;

namespace EtlKit.TestPerformance.ETLBoxTests.Performance
{
    [CollectionDefinition("Performance")]
    public class PerformanceCollectionClass : ICollectionFixture<PerformanceDatabaseFixture> { }

    [Collection("Performance")]
    public class PerformanceTestBase
    {
        public PerformanceDatabaseFixture Fixture { get; }

        protected PerformanceTestBase(PerformanceDatabaseFixture fixture)
        {
            Fixture = fixture;
        }

        protected static SqlConnectionManager SqlConnectionManager =>
            Config.SqlConnection.ConnectionManager("Performance");

        public static string SqlConnectionString =>
            Config.SqlConnection.RawConnectionString("Performance");

        public static IEnumerable<object[]> SqlConnection(
            int numberOfRows,
            int batchSize,
            double deviation
        ) => new[] { new object[] { SqlConnectionManager, numberOfRows, batchSize, deviation } };

        public static IEnumerable<object[]> MySqlConnection(
            int numberOfRows,
            int batchSize,
            double deviation
        ) =>
            new[]
            {
                new object[]
                {
                    Config.MySqlConnection.ConnectionManager("Performance"),
                    numberOfRows,
                    batchSize,
                    deviation,
                },
            };

        public static IEnumerable<object[]> PostgresConnection(
            int numberOfRows,
            int batchSize,
            double deviation
        ) =>
            new[]
            {
                new object[]
                {
                    Config.PostgresConnection.ConnectionManager("Performance"),
                    numberOfRows,
                    batchSize,
                    deviation,
                },
            };

        public static IEnumerable<object[]> SQLiteConnection(
            int numberOfRows,
            int batchSize,
            double deviation
        ) =>
            new[]
            {
                new object[]
                {
                    Config.SQLiteConnection.ConnectionManager("Performance"),
                    numberOfRows,
                    batchSize,
                    deviation,
                },
            };
    }
}
