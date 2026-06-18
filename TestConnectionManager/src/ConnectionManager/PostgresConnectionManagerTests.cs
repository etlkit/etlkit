using System.Data;
using System.Dynamic;
using ALE.ETLBox;
using ALE.ETLBox.ConnectionManager;
using ALE.ETLBox.ControlFlow;
using ALE.ETLBox.DataFlow;
using Npgsql;
using TestConnectionManager.Fixtures;

namespace TestConnectionManager.ConnectionManager
{
    [Collection("Connection Manager")]
    public class PostgresConnectionManagerTests : ConnectionManagerTestBase
    {
        public PostgresConnectionManagerTests(ConnectionManagerFixture fixture)
            : base(ETLBox.Primitives.ConnectionManagerType.Postgres, fixture) { }

        private static int? GetOpenConnections(string connectionString)
        {
            var conString = new PostgresConnectionString(connectionString);
            using var master = new PostgresConnectionManager(conString.CloneWithMasterDbName());
            var dbName = conString.Builder.Database;
            var openConnections = new SqlTask(
                "Count open connections",
                $@"
                SELECT count(*)
                FROM pg_stat_activity
                where datname = '{dbName}'"
            )
            {
                ConnectionManager = master,
                DisableLogging = true,
            }.ExecuteScalar<int>();
            return openConnections;
        }

        // Count isolated by a unique Application Name (see PostgresOpenConnectionCounter),
        // so it is robust to connections from other tests/builds against a shared database.
        private static int OpenConnectionsByAppName(
            string connectionString,
            string applicationName
        ) => PostgresOpenConnectionCounter.CountOpenConnections(connectionString, applicationName);

        [Fact]
        public void TestLeaveConnectionOpen()
        {
            //Arrange
            var appName = PostgresOpenConnectionCounter.NewApplicationName(
                nameof(TestLeaveConnectionOpen)
            );
            var connectionString = PostgresOpenConnectionCounter.TagConnectionString(
                ConnectionStringParameter,
                appName
            );
            Assert.Equal(0, OpenConnectionsByAppName(ConnectionStringParameter, appName));
            var con = new PostgresConnectionManager(connectionString) { LeaveOpen = true };

            //Act
            Assert.Equal(0, OpenConnectionsByAppName(ConnectionStringParameter, appName));
            Assert.True(con.State == null);
            con.Open();
            Assert.True(con.State == ConnectionState.Open);
            Assert.Equal(1, OpenConnectionsByAppName(ConnectionStringParameter, appName));
            con.Open();
            Assert.Equal(1, OpenConnectionsByAppName(ConnectionStringParameter, appName));
            Assert.True(con.State == ConnectionState.Open);
            NpgsqlConnection.ClearAllPools();

            //Assert
            Assert.Equal(1, OpenConnectionsByAppName(ConnectionStringParameter, appName));
            con.Close();
            NpgsqlConnection.ClearAllPools();
            Assert.Equal(0, OpenConnectionsByAppName(ConnectionStringParameter, appName));

            con.Dispose();
        }

        [Fact]
        public void TestLeaveConnectionOpenWithSqlTask()
        {
            //Arrange
            var initialConnections = GetOpenConnections(ConnectionStringParameter);
            var con = new PostgresConnectionManager(ConnectionStringParameter) { LeaveOpen = true };

            //Act
            Assert.Equal(0, GetOpenConnections(ConnectionStringParameter) - initialConnections);
            Assert.True(con.State == null);
            SqlTask.ExecuteNonQuery(con, "Dummy", "SELECT 1");
            Assert.True(con.State == ConnectionState.Open);
            Assert.Equal(1, GetOpenConnections(ConnectionStringParameter) - initialConnections);
            SqlTask.ExecuteNonQuery(con, "Dummy", "SELECT 1");
            Assert.Equal(1, GetOpenConnections(ConnectionStringParameter) - initialConnections);
            Assert.True(con.State == ConnectionState.Open);
            Assert.Equal(1, GetOpenConnections(ConnectionStringParameter) - initialConnections);
            NpgsqlConnection.ClearAllPools();

            //Assert
            Assert.Equal(1, GetOpenConnections(ConnectionStringParameter) - initialConnections);
            con.Close();
            NpgsqlConnection.ClearAllPools();
            Assert.Equal(0, GetOpenConnections(ConnectionStringParameter) - initialConnections);
        }

        [Fact]
        public void TestCloningIfAllowedConnection()
        {
            //Arrange
            using var con = new PostgresConnectionManager(ConnectionStringParameter);
            con.LeaveOpen = true;

            //Act
            using var cloneIfAllowed = con.CloneIfAllowed();
            using var clone = con.Clone();

            //Assert
            Assert.Equal(cloneIfAllowed, con);
            Assert.NotEqual(clone, con);
        }

        [Fact]
        public void TestSimpleQueryForNotLeakingConnections()
        {
            //Arrange
            var initialConnectionCount = GetOpenConnections(ConnectionStringParameter);
            var con = new PostgresConnectionManager(ConnectionStringParameter);

            //Act
            new SqlTask("DUMMY", "select 1") { ConnectionManager = con }.ExecuteScalar();

            //Assert
            NpgsqlConnection.ClearAllPools();
            var connectionCount = GetOpenConnections(ConnectionStringParameter);
            Assert.Equal(0, connectionCount - initialConnectionCount);
        }

        [Fact]
        public void TestBulkInsertForNotLeakingConnections()
        {
            //Arrange
            using var con = new PostgresConnectionManager(ConnectionStringParameter);

            var tableName = "BulkInsertTest";
            TableDefinition tableDef = Prepare(con, tableName);

            using var data = new TableData<string[]>(tableDef);
            object[] values = { "1", "Test1" };
            data.Rows.Add(values);

            NpgsqlConnection.ClearAllPools();
            var initialConnectionCount = GetOpenConnections(ConnectionStringParameter);

            //Act
            SqlTask.BulkInsert(con, "BI", data, tableName);

            //Assert
            NpgsqlConnection.ClearAllPools();
            var connectionCount = GetOpenConnections(ConnectionStringParameter);
            Assert.Equal(0, connectionCount - initialConnectionCount);
        }

        [Fact]
        public void TestDbDestinationForNotLeakingConnections()
        {
            //Arrange
            using var con = new PostgresConnectionManager(ConnectionStringParameter);

            var tableName = "BulkInsertTest";
            Prepare(con, tableName);

            var data = new ExpandoObject();
            var dict = data as IDictionary<string, object>;
            dict["Col1"] = 1;
            dict["Col2"] = "Test1";

            NpgsqlConnection.ClearAllPools();
            var initialConnectionCount = GetOpenConnections(ConnectionStringParameter);

            //Act
            var source = new MemorySource([data]);
            var dest = new DbDestination(con, tableName);
            source.LinkTo(dest);

            source.Execute();
            dest.Wait();

            //Assert
            NpgsqlConnection.ClearAllPools();
            var connectionCount = GetOpenConnections(ConnectionStringParameter);
            Assert.Equal(0, connectionCount - initialConnectionCount);
        }

        [Fact]
        public void TestDbRowTransformationForNotLeakingConnections()
        {
            //Arrange
            using var con = new PostgresConnectionManager(ConnectionStringParameter);

            var tableName = "BulkInsertTest";
            Prepare(con, tableName);

            var data = new ExpandoObject();
            var dict = data as IDictionary<string, object>;
            dict["Col1"] = 1;
            dict["Col2"] = "Test1";

            NpgsqlConnection.ClearAllPools();
            var initialConnectionCount = GetOpenConnections(ConnectionStringParameter);

            //Act
            var source = new MemorySource(new[] { data });
            var transformation = new DbRowTransformation<ExpandoObject>(con, tableName);
            var dest = new MemoryDestination();
            source.LinkTo(transformation);
            transformation.LinkTo(dest);

            source.Execute();
            dest.Wait();

            //Assert
            NpgsqlConnection.ClearAllPools();
            var connectionCount = GetOpenConnections(ConnectionStringParameter);
            Assert.Equal(0, connectionCount - initialConnectionCount);
        }

        [Fact]
        public void TestOpeningConnectionTwice_WithLeaveOpenFalse()
        {
            var appName = PostgresOpenConnectionCounter.NewApplicationName(
                nameof(TestOpeningConnectionTwice_WithLeaveOpenFalse)
            );
            var connectionString = PostgresOpenConnectionCounter.TagConnectionString(
                ConnectionStringParameter,
                appName
            );
            using var con = new PostgresConnectionManager(connectionString);
            con.LeaveOpen = false;
            var connectionCount = OpenConnectionsByAppName(ConnectionStringParameter, appName);
            Assert.Equal(0, connectionCount);

            con.Open();
            connectionCount = OpenConnectionsByAppName(ConnectionStringParameter, appName);
            Assert.Equal(1, connectionCount);

            // With LeaveOpen = false, second Open() closes the first connection and creates a new one
            con.Open();
            connectionCount = OpenConnectionsByAppName(ConnectionStringParameter, appName);
            // Allow for up to 2 connections due to timing of connection closure
            Assert.True(
                connectionCount is 1 or 2,
                $"Expected 1 or 2 connections, but got {connectionCount}"
            );

            con.Close();
            NpgsqlConnection.ClearAllPools();
            connectionCount = OpenConnectionsByAppName(ConnectionStringParameter, appName);
            Assert.Equal(0, connectionCount);
        }

        private static TableDefinition Prepare(PostgresConnectionManager con, string tableName)
        {
            DropTableTask.DropIfExists(con, tableName);

            var tableDef = new TableDefinition(
                tableName,
                new List<TableColumn>
                {
                    new("Col1", "INT", allowNulls: false, isPrimaryKey: true),
                    new("Col2", "VARCHAR(100)", allowNulls: true),
                }
            );
            tableDef.CreateTable(con);
            return tableDef;
        }
    }
}
