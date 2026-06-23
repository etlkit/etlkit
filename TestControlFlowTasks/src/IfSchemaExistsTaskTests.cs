using EtlKit.ConnectionManager;
using EtlKit.ControlFlow;
using EtlKit.Primitives;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks
{
    [Collection(nameof(ControlFlowCollection))]
    public class IfSchemaExistsTaskTests : ControlFlowTestBase
    {
        public IfSchemaExistsTaskTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllConnectionsWithoutSQLiteAndClickHouse;

        [Theory, MemberData(nameof(Connections))]
        public void IfSchemaExists(IConnectionManager connection)
        {
            if (connection.GetType() == typeof(MySqlConnectionManager))
            {
                return;
            }

            //Arrange
            var existsBefore = IfSchemaExistsTask.IsExisting(connection, "testschema");
            CreateSchemaTask.Create(connection, "testschema");

            //Act
            var existsAfter = IfSchemaExistsTask.IsExisting(connection, "testschema");

            //Assert
            Assert.False(existsBefore);
            Assert.True(existsAfter);
        }
    }
}
