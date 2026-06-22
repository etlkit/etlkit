using EtlKit;
using EtlKit.ControlFlow;
using EtlKit.Primitives;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks
{
    [Collection(nameof(ControlFlowCollection))]
    public class DropIndexTaskTests : ControlFlowTestBase
    {
        public DropIndexTaskTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void Drop(IConnectionManager connection)
        {
            //Arrange
            CreateTableTask.Create(
                connection,
                "DropIndexTable",
                new List<TableColumn> { new("Id", "INT", false, true), new("Test1", "INT") }
            );
            CreateIndexTask.CreateOrRecreate(
                connection,
                "IndexToDrop",
                "DropIndexTable",
                new List<string> { "Test1" }
            );
            Assert.True(IfIndexExistsTask.IsExisting(connection, "IndexToDrop", "DropIndexTable"));

            //Act
            DropIndexTask.Drop(connection, "IndexToDrop", "DropIndexTable");

            //Assert
            Assert.False(IfIndexExistsTask.IsExisting(connection, "IndexToDrop", "DropIndexTable"));
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void DropIfExists(IConnectionManager connection)
        {
            //Arrange
            DropIndexTask.DropIfExists(connection, "IndexIfExists", "DropIfExistsIndexTable");
            CreateTableTask.Create(
                connection,
                "DropIfExistsIndexTable",
                new List<TableColumn> { new("Id", "INT", false, true), new("Test1", "INT", true) }
            );
            CreateIndexTask.CreateOrRecreate(
                connection,
                "IndexIfExists",
                "DropIfExistsIndexTable",
                new List<string> { "Test1" }
            );
            Assert.True(
                IfIndexExistsTask.IsExisting(connection, "IndexIfExists", "DropIfExistsIndexTable")
            );

            //Act
            DropIndexTask.DropIfExists(connection, "IndexIfExists", "DropIfExistsIndexTable");

            //Assert
            Assert.False(
                IfIndexExistsTask.IsExisting(connection, "IndexIfExists", "DropIfExistsIndexTable")
            );
        }
    }
}
