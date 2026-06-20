using EtlKit;
using EtlKit.Common;
using EtlKit.ControlFlow;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks
{
    [Collection(nameof(ControlFlowCollection))]
    public class DropDatabaseTaskTests : ControlFlowTestBase
    {
        public DropDatabaseTaskTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        [Theory, MemberData(nameof(DbConnectionTypesWithMaster))]
        public void Drop(string dbType)
        {
            //Arrange
            using var connection = CreateConnectionManager(dbType);
            string dbName = "ETLBox_" + HashHelper.RandomString(10);
            CreateDatabaseTask.Create(connection, dbName);
            bool existsBefore = IfDatabaseExistsTask.IsExisting(connection, dbName);

            //Act
            DropDatabaseTask.Drop(connection, dbName);

            //Assert
            bool existsAfter = IfDatabaseExistsTask.IsExisting(connection, dbName);
            Assert.True(existsBefore);
            Assert.False(existsAfter);
        }

        [Theory, MemberData(nameof(DbConnectionTypesWithMaster))]
        public void DropIfExists(string dbType)
        {
            //Arrange
            using var connection = CreateConnectionManager(dbType);
            string dbName = "ETLBox_" + HashHelper.RandomString(10);
            DropDatabaseTask.DropIfExists(connection, dbName);
            CreateDatabaseTask.Create(connection, dbName);
            bool existsBefore = IfDatabaseExistsTask.IsExisting(connection, dbName);

            //Act
            DropDatabaseTask.DropIfExists(connection, dbName);

            //Assert
            bool existsAfter = IfDatabaseExistsTask.IsExisting(connection, dbName);
            Assert.True(existsBefore);
            Assert.False(existsAfter);
        }

        [Fact]
        public void NotSupportedWithSQLite()
        {
            Assert.Throws<ETLBoxNotSupportedException>(
                () => DropDatabaseTask.Drop(SqliteConnection, "Test")
            );
        }
    }
}
