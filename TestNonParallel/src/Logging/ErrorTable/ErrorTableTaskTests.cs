using EtlKit.ControlFlow;
using EtlKit.Logging;
using EtlKit.Primitives;
using EtlKit.TestNonParallel.Fixtures;

namespace EtlKit.TestNonParallel.Logging.ErrorTable
{
    [Collection("Logging")]
    public sealed class ErrorTableTaskTests : NonParallelTestBase, IDisposable
    {
        public ErrorTableTaskTests(LoggingDatabaseFixture fixture)
            : base(fixture) { }

        public void Dispose()
        {
            EtlKit.Common.ControlFlow.ControlFlow.ClearSettings();
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void CreateErrorTable(IConnectionManager connection)
        {
            //Arrange
            //Act
            CreateErrorTableTask.Create(connection, "etlbox_error");

            //Assert
            IfTableOrViewExistsTask.IsExisting(connection, "etlbox_error");
            var td = TableDefinition.GetDefinitionFromTableName(connection, "etlbox_error");
            Assert.True(td.Columns.Count == 3);
            //Cleanup
            DropTableTask.Drop(connection, "etlbox_error");
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void ReCreateErrorTable(IConnectionManager connection)
        {
            //Arrange
            //Act
            CreateTableTask.Create(
                connection,
                "etlbox_error",
                new List<TableColumn> { new("Col1", "INT") }
            );
            CreateErrorTableTask.DropAndCreate(connection, "etlbox_error");
            //Assert
            var td = TableDefinition.GetDefinitionFromTableName(connection, "etlbox_error");
            Assert.True(td.Columns.Count == 3);
            //Cleanup
            DropTableTask.Drop(connection, "etlbox_error");
        }
    }
}
