using EtlKit;
using EtlKit.ControlFlow;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks.MySql
{
    [Collection(nameof(ControlFlowCollection))]
    public class TableDefinitionTests : ControlFlowTestBase
    {
        public TableDefinitionTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void AutoIncrement()
        {
            //Arrange
            SqlTask.ExecuteNonQuery(
                MySqlConnection,
                "Create table",
                @"
CREATE TABLE identity (
      `Id` INT AUTO_INCREMENT NOT NULL PRIMARY KEY
)"
            );

            //Act
            var result = TableDefinition.GetDefinitionFromTableName(MySqlConnection, "identity");

            //Assert
            Assert.Collection(
                result.Columns,
                tc =>
                    Assert.True(
                        tc.DataType == "int"
                            && tc.NETDataType == typeof(int)
                            && tc.IsIdentity
                            && tc.IsPrimaryKey
                    )
            );
        }

        [Fact]
        public void TypesWithLengthAndPrecision()
        {
            //Arrange
            SqlTask.ExecuteNonQuery(
                MySqlConnection,
                "Create table",
                @"
CREATE TABLE length_and_precision (
      `Value1` VARCHAR (255) NULL,
      `Value2` NVARCHAR (200) NULL,
      `Value3` CHAR (10) NULL,
      `Value4` NCHAR (20) NULL,
      `Value5` DECIMAL (12,3) NULL,
      `Value6` NUMERIC (3,2) NULL
)"
            );

            //Act
            var result = TableDefinition.GetDefinitionFromTableName(
                MySqlConnection,
                "length_and_precision"
            );

            //Assert
            Assert.Collection(
                result.Columns,
                tc => AssertTypes(tc, "varchar(255)", typeof(string)),
                tc => AssertTypes(tc, "varchar(200)", typeof(string)),
                tc => AssertTypes(tc, "char(10)", typeof(string)),
                tc => AssertTypes(tc, "char(20)", typeof(string)),
                tc => AssertTypes(tc, "decimal(12,3)", typeof(decimal)),
                tc => AssertTypes(tc, "decimal(3,2)", typeof(decimal))
            );
        }

        [Fact]
        public void TestComment()
        {
            //Arrange
            SqlTask.ExecuteNonQuery(
                MySqlConnection,
                "Create table",
                @"
CREATE TABLE testcomment (
      `comment` INT NULL COMMENT 'test'
)"
            );

            //Act
            var result = TableDefinition.GetDefinitionFromTableName(MySqlConnection, "testcomment");

            //Assert
            Assert.Collection(
                result.Columns,
                tc => Assert.True(tc.DataType == "int" && tc.Comment == "test")
            );
        }

        private static void AssertTypes(TableColumn tc, string dataType, Type dotnetType)
        {
            Assert.Multiple(
                () => Assert.Equal(tc.DataType, dataType),
                () => Assert.Equal(tc.NETDataType, dotnetType)
            );
        }
    }
}
