using EtlKit.ControlFlow;
using EtlKit.Primitives;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks
{
    [Collection(nameof(ControlFlowCollection))]
    public class DropViewTaskTests : ControlFlowTestBase
    {
        public DropViewTaskTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        [Theory, MemberData(nameof(Connections))]
        public void Drop(IConnectionManager connection)
        {
            //Arrange
            CreateViewTask.CreateOrAlter(connection, "DropViewTest", "SELECT 1 AS Test");
            Assert.True(IfTableOrViewExistsTask.IsExisting(connection, "DropViewTest"));

            //Act
            DropViewTask.Drop(connection, "DropViewTest");

            //Assert
            Assert.False(IfTableOrViewExistsTask.IsExisting(connection, "DropTableTest"));
        }

        [Theory, MemberData(nameof(Connections))]
        public void DropIfExists(IConnectionManager connection)
        {
            // Act
            DropViewTask.DropIfExists(connection, "DropIfExistsViewTest");

            //Arrange
            CreateViewTask.CreateOrAlter(connection, "DropIfExistsViewTest", "SELECT 1 AS Test");
            Assert.True(IfTableOrViewExistsTask.IsExisting(connection, "DropIfExistsViewTest"));

            //Act
            DropViewTask.DropIfExists(connection, "DropIfExistsViewTest");

            //Assert
            Assert.False(IfTableOrViewExistsTask.IsExisting(connection, "DropIfExistsViewTest"));
        }
    }
}
