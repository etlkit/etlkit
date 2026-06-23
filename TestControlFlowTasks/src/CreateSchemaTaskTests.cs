using EtlKit.Common;
using EtlKit.ConnectionManager;
using EtlKit.ControlFlow;
using EtlKit.Primitives;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks
{
    [Collection(nameof(ControlFlowCollection))]
    public class CreateSchemaTaskTests : ControlFlowTestBase
    {
        public CreateSchemaTaskTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        [Theory, MemberData(nameof(AllConnectionsWithoutSQLiteAndClickHouse))]
        public void CreateSchema(IConnectionManager connection)
        {
            if (connection.GetType() == typeof(MySqlConnectionManager))
            {
                return;
            }

            //Arrange
            string schemaName = "s" + HashHelper.RandomString(9);
            //Act
            CreateSchemaTask.Create(connection, schemaName);
            //Assert
            Assert.True(IfSchemaExistsTask.IsExisting(connection, schemaName));
        }

        [Theory, MemberData(nameof(AllConnectionsWithoutSQLiteAndClickHouse))]
        public void CreateSchemaWithSpecialChar(IConnectionManager connection)
        {
            if (connection.GetType() == typeof(MySqlConnectionManager))
            {
                return;
            }

            string qb = connection.QB;
            string qe = connection.QE;
            //Arrange
            string schemaName = $"{qb} s#!/ {qe}";
            //Act
            CreateSchemaTask.Create(connection, schemaName);
            //Assert
            Assert.True(IfSchemaExistsTask.IsExisting(connection, schemaName));
        }
    }
}
