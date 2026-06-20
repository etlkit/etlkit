using EtlKit;
using EtlKit.ConnectionManager;
using EtlKit.ControlFlow;
using EtlKit.TestConnectionManager.Fixtures;
using EtlKit.TestShared.Attributes;
using Microsoft.Data.SqlClient;

namespace EtlKit.TestConnectionManager.ConnectionManager
{
    [Collection("Connection Manager")]
    public sealed class SqlConnectionManagerTests : ConnectionManagerTestBase
    {
        public SqlConnectionManagerTests(ConnectionManagerFixture fixture)
            : base(EtlKit.Primitives.ConnectionManagerType.SqlServer, fixture) { }

        // Подсчёт изолирован по уникальному Application Name (см. SqlOpenConnectionCounter),
        // поэтому устойчив к соединениям других тестов/сборок к общей БД.
        private void AssertOpenConnectionCount(
            int allowedOpenConnections,
            string applicationName
        ) =>
            Assert.Equal(
                allowedOpenConnections,
                SqlOpenConnectionCounter.CountOpenConnections(
                    ConnectionStringParameter,
                    applicationName
                )
            );

        [Fact]
        public void TestOpeningCloseConnection()
        {
            //Arrange
            var appName = SqlOpenConnectionCounter.NewApplicationName(
                nameof(TestOpeningCloseConnection)
            );
            var connectionString = SqlOpenConnectionCounter.TagConnectionString(
                ConnectionStringParameter,
                appName
            );
            using var con = new SqlConnectionManager(new SqlConnectionString(connectionString));

            //Act
            AssertOpenConnectionCount(0, appName);
            con.Open();
            AssertOpenConnectionCount(1, appName);
            con.Close(); //won't close any connection - ado.net will keep the connection open in it's pool in case it's needed again
            AssertOpenConnectionCount(1, appName);
            SqlConnection.ClearAllPools();

            //Assert
            AssertOpenConnectionCount(0, appName);
        }

        [Fact]
        public void TestOpeningConnectionTwice()
        {
            var appName = SqlOpenConnectionCounter.NewApplicationName(
                nameof(TestOpeningConnectionTwice)
            );
            var connectionString = SqlOpenConnectionCounter.TagConnectionString(
                ConnectionStringParameter,
                appName
            );
            using var con = new SqlConnectionManager(new SqlConnectionString(connectionString));
            AssertOpenConnectionCount(0, appName);
            con.Open();
            con.Open();
            AssertOpenConnectionCount(1, appName);
            con.Close();
            AssertOpenConnectionCount(1, appName);
            SqlConnection.ClearAllPools();
            AssertOpenConnectionCount(0, appName);
        }

        [MultiprocessorOnlyFact(Skip = "TODO: Hangs on Apple silicon and Docker")]
        public void TestOpeningConnectionsParallelOnSqlTask()
        {
            var appName = SqlOpenConnectionCounter.NewApplicationName(
                nameof(TestOpeningConnectionsParallelOnSqlTask)
            );
            var connectionString = SqlOpenConnectionCounter.TagConnectionString(
                ConnectionStringParameter,
                appName
            );
            AssertOpenConnectionCount(0, appName);
            var array = new List<int> { 1, 2, 3, 4 };
            var manager = new SqlConnectionManager(new SqlConnectionString(connectionString));
            Parallel.ForEach(
                array,
                new ParallelOptions { MaxDegreeOfParallelism = 2 },
                curNr =>
                    new SqlTask(
                        $"Test statement {curNr}",
                        $@"
                    DECLARE @counter INT = 0;
                    CREATE TABLE dbo.test{curNr} (
                        Col1 nvarchar(50)
                    )
                    WHILE @counter <= 10000
                    BEGIN
                        SET @counter = @counter + 1;
                         INSERT INTO dbo.test{curNr}
                            values('Lorem ipsum Lorem ipsum Lorem ipsum Lorem')
                    END
            "
                    )
                    {
                        ConnectionManager = manager,
                        DisableLogging = true,
                    }.ExecuteNonQuery()
            );
            AssertOpenConnectionCount(2, appName);
            SqlConnection.ClearAllPools();
            AssertOpenConnectionCount(0, appName);

            manager?.Dispose();
        }

        [Fact]
        public void TestCloningConnection()
        {
            //Arrange
            using var con = new SqlConnectionManager(ConnectionStringParameter);

            //Act
            using var clone = con.Clone() as SqlConnectionManager;

            //Assert
            Assert.NotEqual(clone, con);
        }
    }
}
