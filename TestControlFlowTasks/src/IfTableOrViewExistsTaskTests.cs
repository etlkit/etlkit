using EtlKit.ConnectionManager;
using EtlKit.ControlFlow;
using EtlKit.Primitives;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks
{
    [Collection(nameof(ControlFlowCollection))]
    public class IfTableOrViewExistsTaskTests : ControlFlowTestBase
    {
        public IfTableOrViewExistsTaskTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        public static IEnumerable<object[]> Access => AccessConnection;

        [Theory, MemberData(nameof(Connections)), MemberData(nameof(Access))]
        public void IfTableExists(IConnectionManager connection)
        {
            //Arrange
            if (connection.GetType() != typeof(AccessOdbcConnectionManager))
                DropTableTask.DropIfExists(connection, "existtable_test");

            //Act
            var existsBefore = IfTableOrViewExistsTask.IsExisting(connection, "existtable_test");

            CreateTableTask.Create(
                connection,
                "existtable_test",
                new List<EtlKit.TableColumn>()
                {
                    new EtlKit.TableColumn("Id", "Int", false, true),
                    new EtlKit.TableColumn("Col1", "Int", true),
                }
            );

            var existsAfter = IfTableOrViewExistsTask.IsExisting(connection, "existtable_test");

            //Assert
            Assert.False(existsBefore);
            Assert.True(existsAfter);
        }

        [Theory, MemberData(nameof(Connections)), MemberData(nameof(Access))]
        public void IfViewExists(IConnectionManager connection)
        {
            //Arrange
            if (connection.GetType() != typeof(AccessOdbcConnectionManager))
                DropViewTask.DropIfExists(connection, "existview_test");

            //Act
            var existsBefore = IfTableOrViewExistsTask.IsExisting(connection, "existview_test");

            SqlTask.ExecuteNonQuery(
                connection,
                "Create test data table",
                @"CREATE VIEW existview_test AS SELECT 1 AS test"
            );
            var existsAfter = IfTableOrViewExistsTask.IsExisting(connection, "existview_test");

            //Assert
            Assert.False(existsBefore);
            Assert.True(existsAfter);
        }

        [Theory, MemberData(nameof(Connections))]
        public void ThrowException(IConnectionManager connection)
        {
            //Arrange
            //Act
            //Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                IfTableOrViewExistsTask.ThrowExceptionIfNotExists(
                    connection,
                    "xyz.Somestrangenamehere"
                );
            });
        }
    }
}
