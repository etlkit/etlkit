using EtlKit;
using EtlKit.ControlFlow;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks
{
    [Collection(nameof(ControlFlowCollection))]
    public class GetDatabaseListTaskTests : ControlFlowTestBase
    {
        public GetDatabaseListTaskTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        private static string DBName => SqlConnection.ConnectionString.DbName;

        [Fact]
        public void GetDatabaseList()
        {
            //Arrange

            //Act
            List<string> allDatabases = GetDatabaseListTask.List(SqlConnection);

            //Assert
            Assert.True(allDatabases.Count >= 1);
            Assert.Contains(allDatabases, name => name == DBName);
        }

        [Fact]
        public void NotSupportedWithSQLite()
        {
            Assert.Throws<ETLBoxNotSupportedException>(
                () => GetDatabaseListTask.List(SqliteConnection)
            );
        }
    }
}
