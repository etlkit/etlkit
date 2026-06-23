using EtlKit;
using EtlKit.ControlFlow;
using EtlKit.TestControlFlowTasks.Fixtures;

namespace EtlKit.TestControlFlowTasks.SqlServer
{
    [Collection(nameof(ControlFlowCollection))]
    public class TableDefinitionTests : ControlFlowTestBase
    {
        public TableDefinitionTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void BigIntIdentity()
        {
            //Arrange
            SqlTask.ExecuteNonQuery(
                SqlConnection,
                "Create table",
                @"
CREATE TABLE BigIntIdentity (
    Id BIGINT NOT NULL PRIMARY KEY IDENTITY(100,10)
)"
            );

            //Act
            var result = TableDefinition.GetDefinitionFromTableName(
                SqlConnection,
                "BigIntIdentity"
            );

            //Assert
            Assert.Collection(
                result.Columns,
                tc =>
                    Assert.True(
                        tc.DataType == "BIGINT"
                            && tc.NETDataType == typeof(long)
                            && tc.IsIdentity
                            && tc.IdentityIncrement == 10
                            && tc.IdentitySeed == 100
                    )
            );
        }

        [Fact]
        public void NumbericDataTypes()
        {
            //Arrange
            SqlTask.ExecuteNonQuery(
                SqlConnection,
                "Create table",
                @"
CREATE TABLE NumericDataTypes (
    Id BIGINT NOT NULL,
    Col1 NUMERIC,
    Col2 BIT,
    Col3 SMALLINT,
    Col4 DECIMAL,
    Col5 SMALLMONEY,
    Col6 INT,
    Col7 TINYINT,
    Col8 MONEY,
    Col9 FLOAT,
    Col10 REAL
)"
            );

            //Act
            var result = TableDefinition.GetDefinitionFromTableName(
                SqlConnection,
                "NumericDataTypes"
            );

            //Assert
            Assert.Collection(
                result.Columns,
                tc => AssertTypes(tc, "BIGINT", typeof(long)),
                tc => AssertTypes(tc, "NUMERIC(18,0)", typeof(decimal)),
                tc => AssertTypes(tc, "BIT", typeof(bool)),
                tc => AssertTypes(tc, "SMALLINT", typeof(short)),
                tc => AssertTypes(tc, "DECIMAL(18,0)", typeof(decimal)),
                tc => AssertTypes(tc, "SMALLMONEY", typeof(decimal)),
                tc => AssertTypes(tc, "INT", typeof(int)),
                tc => AssertTypes(tc, "TINYINT", typeof(ushort)),
                tc => AssertTypes(tc, "MONEY", typeof(decimal)),
                tc => AssertTypes(tc, "FLOAT", typeof(double)),
                tc => AssertTypes(tc, "REAL", typeof(double))
            );
        }

        [Fact]
        public void DateTimeTypes()
        {
            //Arrange
            SqlTask.ExecuteNonQuery(
                SqlConnection,
                "Create table",
                @"
CREATE TABLE ReadTableDefinition (
    Col11 DATE,
    --Col12 DATETIMEOFFSET,
    Col13 DATETIME2,
    Col14 SMALLDATETIME,
    Col15 DATETIME,
    Col16 TIME

)"
            );

            //Act
            var result = TableDefinition.GetDefinitionFromTableName(
                SqlConnection,
                "ReadTableDefinition"
            );

            //Assert
            Assert.True(result.Columns.TrueForAll(tc => tc.NETDataType == typeof(DateTime)));
            Assert.True(result.Columns.Count == 5);
        }

        [Fact]
        public void TypesWithLengthOrPrecision()
        {
            //Arrange
            SqlTask.ExecuteNonQuery(
                SqlConnection,
                "Create table",
                @"
CREATE TABLE LengthOrPrecisionTypes (    
    Col1 DECIMAL (12,3),
    Col2 NVARCHAR(100),
    Col3 VARCHAR(10),
    Col4 CHAR(4),
    Col5 NCHAR(4),
    Col6 NUMERIC(3,2),    
    Col9 BINARY(10),
    Col10 VARBINARY(20)
)"
            );

            //Act
            var result = TableDefinition.GetDefinitionFromTableName(
                SqlConnection,
                "LengthOrPrecisionTypes"
            );

            //Assert
            Assert.Collection(
                result.Columns,
                tc => AssertTypes(tc, "DECIMAL(12,3)", typeof(decimal)),
                tc => AssertTypes(tc, "NVARCHAR(100)", typeof(string)),
                tc => AssertTypes(tc, "VARCHAR(10)", typeof(string)),
                tc => AssertTypes(tc, "CHAR(4)", typeof(string)),
                tc => AssertTypes(tc, "NCHAR(4)", typeof(string)),
                tc => AssertTypes(tc, "NUMERIC(3,2)", typeof(decimal)),
                tc => AssertTypes(tc, "BINARY(10)", typeof(string)),
                tc => AssertTypes(tc, "VARBINARY(20)", typeof(string))
            );
        }

        [Fact]
        public void TextTypes()
        {
            //Arrange
            SqlTask.ExecuteNonQuery(
                SqlConnection,
                "Create table",
                @"
CREATE TABLE TextTypes (    
    Col1 TEXT,
    Col2 NTEXT,
    Col3 IMAGE    
)"
            );

            //Act
            var result = TableDefinition.GetDefinitionFromTableName(SqlConnection, "TextTypes");

            //Assert
            Assert.Collection(
                result.Columns,
                tc => AssertTypes(tc, "TEXT", typeof(string)),
                tc => AssertTypes(tc, "NTEXT", typeof(string)),
                tc => AssertTypes(tc, "IMAGE", typeof(string))
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
