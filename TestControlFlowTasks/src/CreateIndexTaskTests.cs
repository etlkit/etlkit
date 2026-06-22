using EtlKit;
using EtlKit.ControlFlow;
using EtlKit.Primitives;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks
{
    [Collection(nameof(ControlFlowCollection))]
    public class CreateIndexTaskTests : ControlFlowTestBase
    {
        public CreateIndexTaskTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void CreateIndex(IConnectionManager connection)
        {
            //Arrange
            const string indexName = "ix_IndexTest1";
            CreateTableTask.Create(
                connection,
                "IndexCreationTable1",
                new List<TableColumn>
                {
                    new("Key1", "INT", allowNulls: false, true),
                    new("Key2", "INT", allowNulls: true),
                }
            );

            //Act
            CreateIndexTask.CreateOrRecreate(
                connection,
                indexName,
                "IndexCreationTable1",
                new List<string> { "Key1", "Key2" }
            );

            //Assert
            Assert.True(
                IfIndexExistsTask.IsExisting(connection, "ix_IndexTest1", "IndexCreationTable1")
            );
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void ReCreateIndex(IConnectionManager connection)
        {
            //Arrange
            const string indexName = "ix_IndexTest2";
            CreateTableTask.Create(
                connection,
                "IndexCreationTable2",
                new List<TableColumn>
                {
                    new("Key1", "INT", allowNulls: false, true),
                    new("Key2", "INT", allowNulls: true),
                }
            );
            CreateIndexTask.CreateOrRecreate(
                connection,
                indexName,
                "IndexCreationTable2",
                new List<string> { "Key1", "Key2" }
            );

            //Act
            CreateIndexTask.CreateOrRecreate(
                connection,
                indexName,
                "IndexCreationTable2",
                new List<string> { "Key1", "Key2" }
            );

            //Assert
            Assert.True(
                IfIndexExistsTask.IsExisting(connection, "ix_IndexTest2", "IndexCreationTable2")
            );
        }

        [Fact]
        public void CreateIndexWithInclude()
        {
            //Arrange
            const string indexName = "ix_IndexTest3";
            CreateTableTask.Create(
                SqlConnection,
                "dbo.IndexCreation3",
                new List<TableColumn>
                {
                    new("Key1", "INT", allowNulls: false),
                    new("Key2", "CHAR(2)", allowNulls: true),
                    new("Value1", "NVARCHAR(10)", allowNulls: true),
                    new("Value2", "DECIMAL(10,2)", allowNulls: false),
                }
            );
            //Act
            CreateIndexTask.CreateOrRecreate(
                SqlConnection,
                indexName,
                "dbo.IndexCreation3",
                new List<string> { "Key1", "Key2" },
                new List<string> { "Value1", "Value2" }
            );
            //Assert
            Assert.True(
                IfIndexExistsTask.IsExisting(SqlConnection, "ix_IndexTest3", "dbo.IndexCreation3")
            );
        }
    }
}
