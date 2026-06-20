using EtlKit;
using EtlKit.ControlFlow;
using EtlKit.Primitives;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks
{
    [Collection(nameof(ControlFlowCollection))]
    public class CreateProcedureTaskTests : ControlFlowTestBase
    {
        public CreateProcedureTaskTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        [Theory, MemberData(nameof(AllConnectionsWithoutSQLiteAndClickHouse))]
        public void CreateProcedure(IConnectionManager connection)
        {
            //Arrange
            //Act
            CreateProcedureTask.CreateOrAlter(connection, "Proc1", "SELECT 1;");
            //Assert
            Assert.True(IfProcedureExistsTask.IsExisting(connection, "Proc1"));
        }

        [Theory, MemberData(nameof(AllConnectionsWithoutSQLiteAndClickHouse))]
        public void AlterProcedure(IConnectionManager connection)
        {
            //Arrange
            CreateProcedureTask.CreateOrAlter(connection, "Proc2", "SELECT 1;");
            IfProcedureExistsTask.IsExisting(connection, "Proc2");

            //Act
            CreateProcedureTask.CreateOrAlter(connection, "Proc2", "SELECT 5;");

            //Assert
            Assert.True(IfProcedureExistsTask.IsExisting(connection, "Proc2"));
        }

        [Theory, MemberData(nameof(AllConnectionsWithoutSQLiteAndClickHouse))]
        public void CreateProcedureWithParameter(IConnectionManager connection)
        {
            //Arrange
            List<ProcedureParameter> pars = new List<ProcedureParameter>
            {
                new("Par1", "VARCHAR(10)"),
                new("Par2", "INT", "7"),
            };
            //Act
            CreateProcedureTask.CreateOrAlter(connection, "Proc3", "SELECT 1;", pars);
            //Assert
            Assert.True(IfProcedureExistsTask.IsExisting(connection, "Proc3"));
        }

        [Theory, MemberData(nameof(AllConnectionsWithoutSQLiteAndClickHouse))]
        public void CreateProcedureWithProcedureObject(IConnectionManager connection)
        {
            //Arrange
            List<ProcedureParameter> pars = new List<ProcedureParameter>
            {
                new("Par1", "varchar(10)"),
                new("Par2", "int", "7"),
            };
            ProcedureDefinition procDef = new ProcedureDefinition("Proc4", "SELECT 1;", pars);
            //Act
            CreateProcedureTask.CreateOrAlter(connection, procDef);
            //Assert
            Assert.True(IfProcedureExistsTask.IsExisting(connection, "Proc4"));
        }

        [Fact]
        public void NotSupportedWithSQLite()
        {
            Assert.Throws<ETLBoxNotSupportedException>(
                () => CreateDatabaseTask.Create(SqliteConnection, "Test")
            );
        }
    }
}
