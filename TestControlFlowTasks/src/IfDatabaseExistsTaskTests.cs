using EtlKit;
using EtlKit.Common;
using EtlKit.ControlFlow;
using EtlKit.Primitives;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks
{
    [Collection(nameof(ControlFlowCollection))]
    public class IfDatabaseExistsTaskTests : ControlFlowTestBase
    {
        public IfDatabaseExistsTaskTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllConnectionsWithoutSQLite;

        [Theory, MemberData(nameof(Connections))]
        public void IfDatabaseExists(IConnectionManager connection)
        {
            //Arrange
            string dbName = ("EtlKit_" + HashHelper.RandomString(10)).ToLower();
            var existsBefore = IfDatabaseExistsTask.IsExisting(connection, dbName);

            //Act
            SqlTask.ExecuteNonQuery(connection, "Create DB", $"CREATE DATABASE {dbName}");
            var existsAfter = IfDatabaseExistsTask.IsExisting(connection, dbName);

            //Assert
            Assert.False(existsBefore);
            Assert.True(existsAfter);

            //Cleanup
            DropDatabaseTask.Drop(connection, dbName);
        }

        [Fact]
        public void NotSupportedWithSQLite()
        {
            Assert.Throws<EtlKitNotSupportedException>(
                () => IfDatabaseExistsTask.IsExisting(SqliteConnection, "Test")
            );
        }
    }
}
