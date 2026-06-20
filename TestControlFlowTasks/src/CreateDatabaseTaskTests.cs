using System.Globalization;
using EtlKit;
using EtlKit.Common;
using EtlKit.ConnectionManager;
using EtlKit.ControlFlow;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks
{
    [Collection(nameof(ControlFlowCollection))]
    public class CreateDatabaseTaskTests : ControlFlowTestBase
    {
        public CreateDatabaseTaskTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        [Theory, MemberData(nameof(DbConnectionTypesWithMaster))]
        public void CreateSimple(string dbType)
        {
            //Arrange
            using var connection = CreateConnectionManager(dbType);
            var dbName = "ETLBox_" + HashHelper.RandomString(10);
            var dbListBefore = GetDatabaseListTask.List(connection);
            Assert.DoesNotContain(dbName, dbListBefore);

            //Act
            CreateDatabaseTask.Create(connection, dbName);

            //Assert
            var dbListAfter = GetDatabaseListTask.List(connection);
            Assert.Contains(dbName, dbListAfter);

            //Cleanup
            DropDatabaseTask.Drop(connection, dbName);
        }

        [Theory, MemberData(nameof(DbConnectionTypesWithMaster))]
        public void CreateWithCollation(string dbType)
        {
            //Arrange
            using var save = SetCurrentCulture(CultureInfo.GetCultureInfo("en-US"));
            using var connection = CreateConnectionManager(dbType);
            var dbName = "ETLBox_" + HashHelper.RandomString(10);
            var collation = "Latin1_General_CS_AS";
            if (connection.GetType() == typeof(PostgresConnectionManager))
                collation = "en_US.utf8";
            if (connection.GetType() == typeof(MySqlConnectionManager))
                collation = "latin1_swedish_ci";
            //Act
            CreateDatabaseTask.Create(connection, dbName, collation);

            //Assert
            var dbList = GetDatabaseListTask.List(connection);
            Assert.Contains(dbName, dbList);

            //Cleanup
            DropDatabaseTask.Drop(connection, dbName);
        }

        [Fact]
        public void NotSupportedWithSQLite()
        {
            Assert.Throws<ETLBoxNotSupportedException>(
                () => CreateDatabaseTask.Create(SqliteConnection, "Test")
            );
        }

        internal static IDisposable SetCurrentCulture(CultureInfo culture)
        {
            var oldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = culture;

            return new DeferredExecutionDisposable(() => CultureInfo.CurrentCulture = oldCulture);
        }
    }

    public sealed class DeferredExecutionDisposable : IDisposable
    {
        private readonly Action _action;

        internal DeferredExecutionDisposable(Action action) => _action = action;

        public void Dispose() => _action();
    }
}
