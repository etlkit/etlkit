using EtlKit.ControlFlow;
using EtlKit.Logging;
using EtlKit.Logging.Database;
using EtlKit.TestNonParallel.Fixtures;

namespace EtlKit.TestNonParallel.Logging
{
    [Collection("Logging")]
    public sealed class DatabaseTasksLoggingTests : NonParallelTestBase, IDisposable
    {
        public DatabaseTasksLoggingTests(LoggingDatabaseFixture fixture)
            : base(fixture)
        {
            CreateSchemaTask.Create(SqlConnection, "etl");
            CreateLogTableTask.Create(SqlConnection);
            DatabaseLoggingConfiguration.AddDatabaseLoggingConfiguration(SqlConnection);
        }

        public void Dispose()
        {
            DropTableTask.Drop(SqlConnection, EtlKit.Common.ControlFlow.ControlFlow.LogTable);
            EtlKit.Common.ControlFlow.ControlFlow.ClearSettings();
        }

        private int? CountLogEntries(string taskName)
        {
            return new SqlTask(
                "Find log entry",
                $@"
SELECT COUNT(*) FROM etlkit_log
WHERE task_type='{taskName}'
GROUP BY task_hash"
            )
            {
                DisableLogging = true,
                ConnectionManager = SqlConnection,
            }.ExecuteScalar<int>();
        }

        private void CreateSimpleTable(string tableName)
        {
            new SqlTask(
                "Create test data table",
                $@"
CREATE TABLE {tableName}
(
    Col1 INT NULL,
    Col2 NVARCHAR(50) NULL
)
INSERT INTO {tableName}
SELECT * FROM
(VALUES (1,'Test1'), (2,'Test2'), (3,'Test3')) AS MyTable(v,w)"
            )
            {
                ConnectionManager = SqlConnection,
                DisableLogging = true,
            }.ExecuteNonQuery();
        }

        [Fact]
        public void RowCountLogging()
        {
            //Arrange
            CreateSimpleTable("etl.RowCountLog");
            //Act
            RowCountTask.Count(SqlConnection, "etl.RowCountLog");
            //Assert
            Assert.Equal(2, CountLogEntries("RowCountTask"));
        }

        [Fact]
        public void RowCountWithConditionLogging()
        {
            //Arrange
            CreateSimpleTable("etl.RowCountWithCondition");
            //Act
            RowCountTask.Count(SqlConnection, "etl.RowCountWithCondition", "Col1 = 2");
            //Assert
            Assert.Equal(
                2,
                new SqlTask(
                    "Find log entry",
                    @"
SELECT COUNT(*) FROM etlkit_log
WHERE task_type='RowCountTask' 
AND message LIKE '%with condition%' 
GROUP BY task_hash"
                )
                {
                    DisableLogging = true,
                    ConnectionManager = SqlConnection,
                }.ExecuteScalar<int>()
            );
        }

        [Fact]
        public void SqlTaskLogging()
        {
            //Arrange
            //Act
            SqlTask.ExecuteNonQuery(SqlConnection, "Test select", "select 1 as test");
            //Assert
            Assert.Equal(2, CountLogEntries("SqlTask"));
        }

        [Fact]
        public void TruncateTableLogging()
        {
            //Arrange
            CreateSimpleTable("etl.TruncateTableLog");
            //Act
            TruncateTableTask.Truncate(SqlConnection, "etl.TruncateTableLog");
            //Assert
            Assert.Equal(2, CountLogEntries("TruncateTableTask"));
        }

        [Fact]
        public void DropTableLogging()
        {
            //Arrange
            CreateSimpleTable("etl.DropTableLog");
            //Act
            DropTableTask.DropIfExists(SqlConnection, "etl.DropTableLog");
            //Assert
            Assert.Equal(2, CountLogEntries("DropTableTask"));
        }

        [Fact]
        public void CreateViewLogging()
        {
            //Arrange
            //Act
            CreateViewTask.CreateOrAlter(SqlConnection, "dbo.CreateView", "SELECT 1 AS Test");
            //Assert
            Assert.Equal(2, CountLogEntries("CreateViewTask"));
        }

        [Fact]
        public void DropViewLogging()
        {
            //Arrange
            CreateViewTask.CreateOrAlter(SqlConnection, "dbo.DropView", "SELECT 1 AS Test");
            //Act
            DropViewTask.Drop(SqlConnection, "dbo.DropView");
            //Assert
            Assert.Equal(2, CountLogEntries("DropViewTask"));
        }

        [Fact]
        public void CreateOrAlterProcedureLogging()
        {
            //Arrange
            //Act
            CreateProcedureTask.CreateOrAlter(SqlConnection, "dbo.Proc1", "SELECT 1 AS Test");
            //Assert
            Assert.Equal(2, CountLogEntries("CreateProcedureTask"));
        }

        [Fact]
        public void CreateTableLogging()
        {
            //Arrange
            //Act
            CreateTableTask.Create(
                SqlConnection,
                "dbo.CreateTable",
                new List<TableColumn> { new("value", "INT") }
            );
            //Assert
            Assert.Equal(2, CountLogEntries("CreateTableTask"));
        }

        [Fact]
        public void CreateSchemaLogging()
        {
            //Arrange
            //Act
            CreateSchemaTask.Create(SqlConnection, "createdSchema");
            //Assert
            Assert.Equal(2, CountLogEntries("CreateSchemaTask"));
        }

        [Fact]
        public void CreateIndexLogging()
        {
            //Arrange
            CreateSimpleTable("dbo.CreateIndex");
            //Act
            CreateIndexTask.CreateOrRecreate(
                SqlConnection,
                "ix_logIndexTest",
                "dbo.CreateIndex",
                new List<string> { "Col1", "Col2" }
            );
            //Assert
            Assert.Equal(2, CountLogEntries("CreateIndexTask"));
        }

        [Fact]
        public void IfExistsLogging()
        {
            //Arrange
            CreateSimpleTable("IfExistsTable");
            //Act
            IfTableOrViewExistsTask.IsExisting(SqlConnection, "IfExistsTable");
            //Assert
            Assert.Equal(2, CountLogEntries("IfTableOrViewExistsTask"));
        }

        [Fact]
        public void CheckHashValuesEquality()
        {
            //Arrange
            SqlTask.ExecuteNonQuery(SqlConnection, "Test Task - same name", "Select 1 as test");
            //Act
            SqlTask.ExecuteNonQuery(SqlConnection, "Test Task - same name", "Select 2 as test");
            //Assert
            Assert.Equal(
                4,
                new SqlTask(
                    "Check if hash are equal",
                    @"SELECT COUNT(*) from etlkit_log GROUP BY task_hash"
                )
                {
                    DisableLogging = true,
                    ConnectionManager = SqlConnection,
                }.ExecuteScalar<int>()
            );
        }

        [Fact]
        public void CustomTaskLogging()
        {
            //Arrange
            //Act
            CustomTask.Execute("Test custom task 4", () => { });
            //Assert
            Assert.Equal(2, CountLogEntries("CustomTask"));
        }

        [Fact]
        public void SequenceLogging()
        {
            //Arrange
            //Act
            Sequence.Execute("Test sequence 3", () => { });
            //Assert
            Assert.Equal(2, CountLogEntries("Sequence"));
        }
    }
}
