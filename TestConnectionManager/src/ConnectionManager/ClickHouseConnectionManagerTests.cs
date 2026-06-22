using EtlKit.ClickHouse.ConnectionManager;
using EtlKit.ControlFlow;
using EtlKit.Primitives;
using EtlKit.TestShared.Helper;

namespace EtlKit.TestConnectionManager.ConnectionManager
{
    public class ClickHouseConnectionManagerTests
    {
        [Fact]
        public void TableDefinitionShouldNotDoubles()
        {
            // Arrange
            var connectionString = Config
                .ClickHouseConnection.ConnectionString("ConnectionManager")
                .Value;

            var testTable = "test";
            var dbName1 = "db1";
            var dbName2 = "db2";
            var table = new TableDefinition(
                testTable,
                new List<TableColumn> { new TableColumn("Id", "Int", false, true) }
            );

            var builder =
                new EtlKit.ClickHouse.ConnectionStrings.ClickHouseConnectionStringBuilder();
            builder.ConnectionString = connectionString;

            using var con = new ClickHouseConnectionManager(builder.ConnectionString);

            RecreateDatabase(con, dbName1);
            RecreateDatabase(con, dbName2);

            builder.Database = dbName1;
            using var con1 = new ClickHouseConnectionManager(builder.ConnectionString);
            CreateTableTask.Create(con1, table);

            builder.Database = dbName2;
            using var con2 = new ClickHouseConnectionManager(builder.ConnectionString);
            CreateTableTask.Create(con2, table);

            // Act
            var dbDefinition = TableDefinition.GetDefinitionFromTableName(con1, table.Name);

            // Assert
            Assert.NotNull(dbDefinition);
            Assert.Single(dbDefinition.Columns);
            Assert.Equal(table.Columns[0].Name, dbDefinition.Columns[0].Name);
        }

        private static void RecreateDatabase(IConnectionManager con, string dbName)
        {
            new DropDatabaseTask(dbName)
            {
                TaskName = $"Drop database {dbName}",
                DisableLogging = true,
                ConnectionManager = con,
            }.DropIfExists();

            new CreateDatabaseTask(dbName)
            {
                TaskName = $"Create database {dbName}",
                DisableLogging = true,
                ConnectionManager = con,
            }.Execute();
        }
    }
}
