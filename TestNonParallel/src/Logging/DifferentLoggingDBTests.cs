using EtlKit.Common.DataFlow;
using EtlKit.ConnectionManager;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Logging;
using EtlKit.Logging.Database;
using EtlKit.TestNonParallel.Fixtures;

namespace EtlKit.TestNonParallel.Logging
{
    [Collection("Logging")]
    public sealed class DifferentLoggingDBTests
        : NonParallelTestBase,
            IDisposable,
            IClassFixture<NoLoggingDatabaseFixture>
    {
        public NoLoggingDatabaseFixture NoLoggingDatabaseFixture { get; }
        private static SqlConnectionManager LoggingConnection => SqlConnection;
        private static SqlConnectionManager NoLogConnection =>
            NoLoggingDatabaseFixture.SqlConnection;

        public DifferentLoggingDBTests(
            LoggingDatabaseFixture fixture,
            NoLoggingDatabaseFixture noLoggingDatabaseFixture
        )
            : base(fixture)
        {
            NoLoggingDatabaseFixture = noLoggingDatabaseFixture;
            CreateLogTableTask.Create(LoggingConnection);
            DatabaseLoggingConfiguration.AddDatabaseLoggingConfiguration(SqlConnection);
        }

        public void Dispose()
        {
            DropTableTask.Drop(LoggingConnection, EtlKit.Common.ControlFlow.ControlFlow.LogTable);
            EtlKit.Common.ControlFlow.ControlFlow.ClearSettings();
            Common.DataFlow.DataFlow.ClearSettings();
        }

        [Fact]
        public void ControlFlowLoggingInDifferentDB()
        {
            //Arrange

            //Act
            SqlTask.ExecuteNonQuery(
                NoLogConnection,
                "Create source table",
                @"CREATE TABLE CFLogSource
                            (Col1 INT NOT NULL, Col2 NVARCHAR(50) NULL)"
            );

            EtlKit.Common.ControlFlow.ControlFlow.DefaultDbConnection = NoLogConnection;

            SqlTask.ExecuteNonQuery(
                "Insert demo data",
                "INSERT INTO CFLogSource VALUES(1,'Test1')"
            );

            //Assert
            Assert.Equal(
                4,
                new RowCountTask("etlkit_log", "task_type = 'SqlTask' ")
                {
                    DisableLogging = true,
                    ConnectionManager = LoggingConnection,
                }
                    .Count()
                    .Rows
            );
        }

        [Fact]
        public void DataFlowLoggingInDifferentDB()
        {
            //Arrange
            Common.DataFlow.DataFlow.LoggingThresholdRows = 3;
            SqlTask.ExecuteNonQuery(
                NoLogConnection,
                "Create source table",
                @"CREATE TABLE DFLogSource
                            (Col1 INT NOT NULL, Col2 NVARCHAR(50) NULL)"
            );
            SqlTask.ExecuteNonQuery(
                NoLogConnection,
                "Insert demo data",
                "INSERT INTO DFLogSource VALUES(1,'Test1')"
            );
            SqlTask.ExecuteNonQuery(
                NoLogConnection,
                "Insert demo data",
                "INSERT INTO DFLogSource VALUES(2,'Test2')"
            );
            SqlTask.ExecuteNonQuery(
                NoLogConnection,
                "Insert demo data",
                "INSERT INTO DFLogSource VALUES(3,'Test3')"
            );

            SqlTask.ExecuteNonQuery(
                LoggingConnection,
                "Create source table",
                @"CREATE TABLE DFLogDestination
                            (Col1 INT NOT NULL, Col2 NVARCHAR(50) NULL)"
            );

            DbSource source = new DbSource(NoLogConnection, "DFLogSource");
            DbDestination dest = new DbDestination(LoggingConnection, "DFLogDestination");

            //Act
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(
                4,
                new RowCountTask("etlkit_log", "task_type = 'DbSource'")
                {
                    DisableLogging = true,
                    ConnectionManager = LoggingConnection,
                }
                    .Count()
                    .Rows
            );
            Assert.Equal(
                4,
                new RowCountTask("etlkit_log", "task_type = 'DbDestination'")
                {
                    DisableLogging = true,
                    ConnectionManager = LoggingConnection,
                }
                    .Count()
                    .Rows
            );
        }
    }
}
