using System.Linq;
using EtlKit.ControlFlow;
using EtlKit.Logging;
using EtlKit.Logging.Database;
using EtlKit.Primitives;
using EtlKit.TestNonParallel.Fixtures;
using Microsoft.Extensions.Logging;

namespace EtlKit.TestNonParallel.Logging.LogTable
{
    [Collection("Logging")]
    public sealed class LogTaskTests : NonParallelTestBase, IDisposable
    {
        public LogTaskTests(LoggingDatabaseFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        public static IEnumerable<object[]> ConnectionsWithoutClickHouse =>
            AllSqlConnectionsWithoutClickHouse;

        public void Dispose()
        {
            EtlKit.Common.ControlFlow.ControlFlow.ClearSettings();
        }

        [Theory, MemberData(nameof(ConnectionsWithoutClickHouse))]
        public void CreateLogTable(IConnectionManager connection)
        {
            //Arrange
            //Act
            CreateLogTableTask.Create(connection, "etlkit_testlog");

            //Assert
            Assert.True(IfTableOrViewExistsTask.IsExisting(connection, "etlkit_testlog"));
            var td = TableDefinition.GetDefinitionFromTableName(connection, "etlkit_testlog");
            Assert.True(td.Columns.Count == 10);
            //Cleanup
            DropTableTask.Drop(connection, "etlkit_testlog");
        }

        [Theory, MemberData(nameof(ConnectionsWithoutClickHouse))]
        public void TestErrorLogging(IConnectionManager connection)
        {
            //Arrange
            DropTableTask.DropIfExists(connection, "test_log");
            CreateLogTableTask.Create(connection, "test_log");
            DatabaseLoggingConfiguration.AddDatabaseLoggingConfiguration(
                connection,
                LogLevel.Trace,
                "test_log"
            );
            //Act
            LogTask.Error(connection, "Error!");
            LogTask.Warn(connection, "Warn!");
            LogTask.Info(connection, "Info!");
            LogTask.Debug(connection, "Debug!");
            LogTask.Trace(connection, "Trace!");
            //Assert
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    EtlKit.Common.ControlFlow.ControlFlow.LogTable,
                    "message = 'Error!' AND level = 'Error' and task_action = 'LOG'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    EtlKit.Common.ControlFlow.ControlFlow.LogTable,
                    "message = 'Warn!' AND level = 'Warn' and task_action = 'LOG'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    EtlKit.Common.ControlFlow.ControlFlow.LogTable,
                    "message = 'Info!' AND level = 'Info' and task_action = 'LOG'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    EtlKit.Common.ControlFlow.ControlFlow.LogTable,
                    "message = 'Debug!' AND level = 'Debug' and task_action = 'LOG'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    EtlKit.Common.ControlFlow.ControlFlow.LogTable,
                    "message = 'Trace!' AND level = 'Trace' and task_action = 'LOG'"
                )
            );

            //Cleanup
            DropTableTask.Drop(connection, EtlKit.Common.ControlFlow.ControlFlow.LogTable);
        }

        [Fact]
        public void IsControlFlowStageLogged()
        {
            //Arrange
            CreateLogTableTask.Create(SqlConnection, "test_log_stage");
            DatabaseLoggingConfiguration.AddDatabaseLoggingConfiguration(
                SqlConnection,
                LogLevel.Debug,
                "test_log_stage"
            );

            //Act
            EtlKit.Common.ControlFlow.ControlFlow.Stage = "SETUP";
            SqlTask.ExecuteNonQuery(SqlConnection, "Test Task", "Select 1 as test");

            //Assert
            Assert.Equal(
                2,
                new RowCountTask("test_log_stage", "message='Test Task' and stage = 'SETUP'")
                {
                    DisableLogging = true,
                    ConnectionManager = SqlConnection,
                }
                    .Count()
                    .Rows
            );

            //Cleanup
            DropTableTask.Drop(SqlConnection, EtlKit.Common.ControlFlow.ControlFlow.LogTable);
        }

        [Theory, MemberData(nameof(ConnectionsWithoutClickHouse))]
        public void TestReadLogTask(IConnectionManager connection)
        {
            //Arrange
            DropTableTask.DropIfExists(connection, "test_readlog");
            CreateLogTableTask.Create(connection, "test_readlog");
            DatabaseLoggingConfiguration.AddDatabaseLoggingConfiguration(
                connection,
                LogLevel.Information,
                "test_readlog"
            );
            SqlTask.ExecuteNonQuery(connection, "Test Task", "Select 1 as test");

            //Act
            List<LogEntry> entries = ReadLogTableTask
                .Read(connection)
                .OrderBy(e => e.LogDate)
                .ToList();

            //Assert
            Assert.Collection(
                entries,
                l =>
                    Assert.True(
                        l.Message == "Test Task"
                            && l.TaskAction == "START"
                            && l.TaskType == "SqlTask"
                    ),
                l =>
                    Assert.True(
                        l.Message == "Test Task" && l.TaskAction == "END" && l.TaskType == "SqlTask"
                    )
            );

            //Cleanup
            DropTableTask.Drop(connection, EtlKit.Common.ControlFlow.ControlFlow.LogTable);
        }
    }
}
