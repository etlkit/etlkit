using EtlKit;
using EtlKit.ControlFlow;
using EtlKit.Primitives;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks
{
    [Collection(nameof(ControlFlowCollection))]
    public class CreateViewTaskTests : ControlFlowTestBase
    {
        public CreateViewTaskTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        [Theory, MemberData(nameof(AllSqlConnections)), MemberData(nameof(AccessConnection))]
        public void CreateView(IConnectionManager connection)
        {
            //Arrange
            //Act
            CreateViewTask.CreateOrAlter(connection, "View1", "SELECT 1 AS test");
            //Assert
            Assert.True(IfTableOrViewExistsTask.IsExisting(connection, "View1"));
            var td = TableDefinition.GetDefinitionFromTableName(connection, "View1");
            Assert.Contains(td.Columns, col => col.Name == "test");
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void AlterView(IConnectionManager connection)
        {
            //Arrange
            CreateViewTask.CreateOrAlter(connection, "View2", "SELECT 1 AS Test");
            Assert.True(IfTableOrViewExistsTask.IsExisting(connection, "View2"));

            //Act
            CreateViewTask.CreateOrAlter(connection, "View2", "SELECT 5 AS test");

            //Assert
            Assert.True(IfTableOrViewExistsTask.IsExisting(connection, "View2"));
        }
    }
}
