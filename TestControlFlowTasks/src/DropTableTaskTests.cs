using EtlKit;
using EtlKit.ControlFlow;
using EtlKit.Primitives;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks
{
    [Collection(nameof(ControlFlowCollection))]
    public class DropTableTaskTests : ControlFlowTestBase
    {
        public DropTableTaskTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        public static IEnumerable<object[]> Access => AccessConnection;

        [Theory, MemberData(nameof(Connections)), MemberData(nameof(Access))]
        public void Drop(IConnectionManager connection)
        {
            //Arrange
            List<TableColumn> columns = new List<TableColumn>
            {
                new("id", "int", false, true),
                new("value", "int", true),
            };
            CreateTableTask.Create(connection, "DropTableTest", columns);
            Assert.True(IfTableOrViewExistsTask.IsExisting(connection, "DropTableTest"));

            //Act
            DropTableTask.Drop(connection, "DropTableTest");

            //Assert
            Assert.False(IfTableOrViewExistsTask.IsExisting(connection, "DropTableTest"));
        }

        [Theory, MemberData(nameof(Connections))]
        public void DropIfExists(IConnectionManager connection)
        {
            //Arrange
            DropTableTask.DropIfExists(connection, "DropIfExistsTableTest");
            List<TableColumn> columns = new List<TableColumn>
            {
                new("id", "int", false, true),
                new("value", "int", true),
            };
            CreateTableTask.Create(connection, "DropIfExistsTableTest", columns);
            Assert.True(IfTableOrViewExistsTask.IsExisting(connection, "DropIfExistsTableTest"));

            //Act
            DropTableTask.DropIfExists(connection, "DropIfExistsTableTest");

            //Assert
            Assert.False(IfTableOrViewExistsTask.IsExisting(connection, "DropIfExistsTableTest"));
        }
    }
}
