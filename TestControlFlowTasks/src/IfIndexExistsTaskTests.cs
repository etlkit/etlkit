using EtlKit.ControlFlow;
using EtlKit.Primitives;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks
{
    [Collection(nameof(ControlFlowCollection))]
    public class IfIndexExistsTaskTests : ControlFlowTestBase
    {
        public IfIndexExistsTaskTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        [Theory, MemberData(nameof(Connections))]
        public void IfIndexExists(IConnectionManager connection)
        {
            //Arrange
            if (IfTableOrViewExistsTask.IsExisting(connection, "indextable"))
            {
                DropTableTask.Drop(connection, "indextable");
            }
            CreateTableTask.Create(
                connection,
                "indextable",
                new List<EtlKit.TableColumn>
                {
                    new EtlKit.TableColumn("col1", "INT", false, true),
                    new EtlKit.TableColumn("col2", "INT", true),
                }
            );

            //Act
            var existsBefore = IfIndexExistsTask.IsExisting(connection, "index_test", "indextable");

            CreateIndexTask.CreateOrRecreate(
                connection,
                "index_test",
                "indextable",
                new List<string> { "col2" }
            );

            var existsAfter = IfIndexExistsTask.IsExisting(connection, "index_test", "indextable");

            //Assert
            Assert.False(existsBefore);
            Assert.True(existsAfter);
        }
    }
}
