using EtlKit;
using EtlKit.Common;
using EtlKit.ControlFlow;
using EtlKit.ControlFlow.SqlServer;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks.SqlServer
{
    [Collection(nameof(ControlFlowCollection))]
    public class CleanUpSchemaTaskTests : ControlFlowTestBase
    {
        public CleanUpSchemaTaskTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void CleanUpSchema()
        {
            //Arrange
            string schemaName = "s" + HashHelper.RandomString(9);
            SqlTask.ExecuteNonQuery(SqlConnection, "Create schema", $"CREATE SCHEMA {schemaName}");
            SqlTask.ExecuteNonQuery(
                SqlConnection,
                "Create table",
                $"CREATE TABLE {schemaName}.Table1 ( Nothing INT NULL )"
            );
            SqlTask.ExecuteNonQuery(
                SqlConnection,
                "Create view",
                $"CREATE VIEW {schemaName}.View1 AS SELECT * FROM {schemaName}.Table1"
            );
            SqlTask.ExecuteNonQuery(
                SqlConnection,
                "Create procedure",
                $"CREATE PROCEDURE {schemaName}.Proc1 AS SELECT * FROM {schemaName}.Table1"
            );
            var objCountSql = new SqlTask(
                "Count object",
                $@"SELECT COUNT(*) FROM sys.objects obj 
 INNER JOIN sys.schemas sch  ON sch.schema_id = obj.schema_id
WHERE sch.name = '{schemaName}'"
            )
            {
                ConnectionManager = SqlConnection,
            };
            Assert.Equal(3, objCountSql.ExecuteScalar<int>());

            //Act
            CleanUpSchemaTask.CleanUp(SqlConnection, schemaName);

            //Assert
            Assert.Equal(0, objCountSql.ExecuteScalar<int>());
        }

        [Fact]
        public void NotSupportedWithOtherDBs()
        {
            Assert.Throws<ETLBoxNotSupportedException>(
                () => CleanUpSchemaTask.CleanUp(SqliteConnection, "Test")
            );
        }
    }
}
