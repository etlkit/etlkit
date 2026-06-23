using System.Threading.Tasks;
using EtlKit.ControlFlow;
using EtlKit.Logging;
using EtlKit.Logging.Database;
using EtlKit.Primitives;
using EtlKit.TestNonParallel.Fixtures;
using Microsoft.Extensions.Logging;

namespace EtlKit.TestNonParallel.Logging.LoadProcessTable
{
    [Collection("Logging")]
    public sealed class LoadProcessTasksTests : NonParallelTestBase, IDisposable
    {
        public LoadProcessTasksTests(LoggingDatabaseFixture fixture)
            : base(fixture) { }

        public void Dispose()
        {
            EtlKit.Common.ControlFlow.ControlFlow.ClearSettings();
        }

        [Theory, MemberData(nameof(AllSqlConnectionsWithoutClickHouse))]
        public void CreateLoadProcessTable(IConnectionManager connection)
        {
            //Arrange
            //Act
            CreateLoadProcessTableTask.Create(connection, "etlkit_testloadprocess");

            //Assert
            IfTableOrViewExistsTask.IsExisting(connection, "etlkit_testloadprocess");
            var td = TableDefinition.GetDefinitionFromTableName(
                connection,
                "etlkit_testloadprocess"
            );
            Assert.True(td.Columns.Count == 11);

            //Cleanup
            DropTableTask.Drop(connection, "etlkit_testloadprocess");
        }

        [Theory, MemberData(nameof(AllSqlConnectionsWithoutClickHouse))]
        public void StartLoadProcess(IConnectionManager connection)
        {
            //Arrange
            CreateLoadProcessTableTask.Create(connection, "test_load_process");

            //Act
            StartLoadProcessTask.Start(connection, "Test process 1");

            //Assert
            Assert.True(EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess != null);
            Assert.Equal(
                "Test process 1",
                EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess.ProcessName
            );
            Assert.True(
                EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess.StartDate
                    >= DateTime.Now.AddSeconds(-1)
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    EtlKit.Common.ControlFlow.ControlFlow.LoadProcessTable,
                    "start_message IS NULL and end_message IS NULL and abort_message IS NULL"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    EtlKit.Common.ControlFlow.ControlFlow.LoadProcessTable,
                    "is_running = 1 AND was_successful=0 AND was_aborted=0"
                )
            );

            //Cleanup
            DropTableTask.Drop(connection, "test_load_process");
        }

        [Theory, MemberData(nameof(AllSqlConnectionsWithoutClickHouse))]
        public void StartLoadProcessWithMessage(IConnectionManager connection)
        {
            //Arrange
            CreateLoadProcessTableTask.Create(connection, "test_lp_withmessage");

            //Act
            StartLoadProcessTask.Start(connection, "Test process 1", "Message 1", "SourceA");

            //Assert
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    "test_lp_withmessage",
                    "start_message = 'Message 1' AND source='SourceA' AND end_message IS NULL AND abort_message IS NULL"
                )
            );

            //Cleanup
            DropTableTask.Drop(connection, "test_lp_withmessage");
        }

        [Theory, MemberData(nameof(AllSqlConnectionsWithoutClickHouse))]
        public void EndLoadProcess(IConnectionManager connection)
        {
            //Arrange
            CreateLoadProcessTableTask.Create(connection, "test_lp_end");

            StartLoadProcessTask.Start(connection, "Test process 2");
            Assert.True(EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess.IsRunning);

            //Act
            EndLoadProcessTask.End(connection, "End process 2");

            //Assert
            Assert.False(EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess.IsRunning);
            Assert.True(EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess.WasSuccessful);
            Assert.True(EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess.IsFinished);
            Assert.True(
                EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess.EndDate
                    >= DateTime.Now.AddSeconds(-2)
            );

            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    "test_lp_end",
                    "is_running=0 and was_successful=1 and was_aborted=0"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    "test_lp_end",
                    "start_message IS NULL AND end_message = 'End process 2' AND abort_message IS NULL"
                )
            );

            //Cleanup
            DropTableTask.Drop(connection, "test_lp_end");
        }

        [Fact]
        public void AbortLoadProcess()
        {
            //Arrange
            CreateLoadProcessTableTask.Create(SqlConnection, "test_lp_abort");

            StartLoadProcessTask.Start(SqlConnection, "Test process 3");
            Assert.True(EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess.IsRunning);

            //Act
            AbortLoadProcessTask.Abort(
                SqlConnection,
                EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess.Id
            );

            //Assert
            Assert.False(EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess.IsRunning);
            Assert.True(EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess.WasAborted);
            Assert.True(
                EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess.AbortMessage == null
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "test_lp_abort",
                    "is_running=0 and was_successful=0 and was_aborted=1"
                )
            );

            //Cleanup
            DropTableTask.Drop(SqlConnection, "test_lp_abort");
        }

        [Theory, MemberData(nameof(AllSqlConnectionsWithoutClickHouse))]
        public void IsLoadProcessKeyInLog(IConnectionManager connection)
        {
            //Arrange
            CreateLoadProcessTableTask.Create(connection, "test_lpkey_inlog");
            CreateLogTableTask.Create(connection, "test_lpkey_log");
            DatabaseLoggingConfiguration.AddDatabaseLoggingConfiguration(
                connection,
                LogLevel.Information,
                "test_lpkey_log"
            );
            StartLoadProcessTask.Start(connection, "Test process 5");

            //Act
            SqlTask.ExecuteNonQuery(connection, "Test Task", "Select 1 as test");

            //Assert
            Assert.Equal(
                2,
                new RowCountTask(
                    "test_lpkey_log",
                    $"message='Test Task' and load_process_id = {EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess.Id}"
                )
                {
                    DisableLogging = true,
                    ConnectionManager = connection,
                }
                    .Count()
                    .Rows
            );

            //Cleanup
            DropTableTask.Drop(connection, EtlKit.Common.ControlFlow.ControlFlow.LogTable);
            DropTableTask.Drop(connection, EtlKit.Common.ControlFlow.ControlFlow.LoadProcessTable);
        }

        [Fact]
        public async Task ReadLastSuccessfulProcess()
        {
            //Arrange
            EtlKit.Common.ControlFlow.ControlFlow.DefaultDbConnection = SqlConnection;
            CreateLoadProcessTableTask.Create("test_lpkey_lastsuccess");
            StartLoadProcessTask.Start("Test process 8");
            await Task.Delay(10); //Sql Server datetime is not that exact
            EndLoadProcessTask.End();
            await Task.Delay(10);
            StartLoadProcessTask.Start("Test process 9");
            await Task.Delay(10); //Sql Server datetime is not that exact
            EndLoadProcessTask.End();

            //Act
            var lp = ReadLoadProcessTableTask.ReadWithOption(ReadOptions.ReadLastSuccessful);

            //Assert
            Assert.True(lp.IsFinished);
            Assert.True(lp.WasSuccessful);
            Assert.False(lp.WasAborted);
            Assert.Equal("Test process 9", lp.ProcessName);

            //Cleanup
            DropTableTask.Drop("test_lpkey_lastsuccess");
        }

        [Fact]
        public async Task ReadLastAbortedProcess()
        {
            //Arrange
            EtlKit.Common.ControlFlow.ControlFlow.DefaultDbConnection = SqlConnection;
            CreateLoadProcessTableTask.Create("test_lpkey_lastabort");
            StartLoadProcessTask.Start("Test process 10");
            await Task.Delay(10); //Sql Server datetime is not that exact
            EndLoadProcessTask.End();
            await Task.Delay(10);
            StartLoadProcessTask.Start("Test process 11");
            await Task.Delay(10); //Sql Server datetime is not that exact
            AbortLoadProcessTask.Abort();
            StartLoadProcessTask.Start("Test process 12");
            await Task.Delay(10); //Sql Server datetime is not that exact
            EndLoadProcessTask.End();

            //Act
            var lp = ReadLoadProcessTableTask.ReadWithOption(ReadOptions.ReadLastAborted);

            //Assert
            Assert.True(lp.IsFinished);
            Assert.True(lp.WasAborted);
            Assert.False(lp.WasSuccessful);
            Assert.Equal("Test process 11", lp.ProcessName);

            //Cleanup
            DropTableTask.Drop(SqlConnection, "test_lpkey_lastabort");
        }

        [Fact]
        public void IsLoadProcessKeySetIfRestarted()
        {
            //Arrange
            EtlKit.Common.ControlFlow.ControlFlow.DefaultDbConnection = SqlConnection;
            CreateLoadProcessTableTask.Create("test_lp_restart");
            CreateLogTableTask.Create("test_log_restart");
            DatabaseLoggingConfiguration.AddDatabaseLoggingConfiguration(
                SqlConnection,
                LogLevel.Information,
                "test_log_restart"
            );

            //Act
            StartLoadProcessTask.Start("Test process 13");
            long? processId1 = EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess.Id;
            SqlTask.ExecuteNonQuery("Test Task", "Select 1 as test");
            Assert.Equal(
                2,
                new RowCountTask(
                    "test_log_restart",
                    $"message='Test Task' AND load_process_id = {processId1}"
                )
                {
                    DisableLogging = true,
                }
                    .Count()
                    .Rows
            );

            StartLoadProcessTask.Start("Test process 14");
            long? processId2 = EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess.Id;

            //Assert
            Assert.NotEqual(processId1, processId2);
            SqlTask.ExecuteNonQuery("Test Task", "Select 1 as test");
            Assert.Equal(
                2,
                new RowCountTask(
                    "test_log_restart",
                    $"message='Test Task' AND load_process_id = {processId2}"
                )
                {
                    DisableLogging = true,
                }
                    .Count()
                    .Rows
            );

            //Cleanup
            DropTableTask.Drop(EtlKit.Common.ControlFlow.ControlFlow.LogTable);
            DropTableTask.Drop(EtlKit.Common.ControlFlow.ControlFlow.LoadProcessTable);
        }

        [Fact]
        public void IsLoadProcessKeySetForLogTask()
        {
            //Arrange
            EtlKit.Common.ControlFlow.ControlFlow.DefaultDbConnection = SqlConnection;
            CreateLoadProcessTableTask.Create("test_lp_logtask");
            CreateLogTableTask.Create("test_log_logtask");
            DatabaseLoggingConfiguration.AddDatabaseLoggingConfiguration(
                SqlConnection,
                LogLevel.Information,
                "test_log_logtask"
            );

            //Act
            StartLoadProcessTask.Start("Test process 15");
            var processId1 = EtlKit.Common.ControlFlow.ControlFlow.CurrentLoadProcess.Id;
            LogTask.Error("Test1");
            LogTask.Warn("Test2");
            LogTask.Info("Test3");
            //Assert
            Assert.Equal(
                3,
                new RowCountTask(
                    "test_log_logtask",
                    $"message like 'Test%' AND load_process_id = {processId1}"
                )
                {
                    DisableLogging = true,
                }
                    .Count()
                    .Rows
            );

            //Cleanup
            DropTableTask.Drop(EtlKit.Common.ControlFlow.ControlFlow.LogTable);
            DropTableTask.Drop(EtlKit.Common.ControlFlow.ControlFlow.LoadProcessTable);
        }
    }
}
