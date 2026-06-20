using System.Collections.Generic;
using EtlKit.Helper;

namespace EtlKit.TestHelper
{
    public class SqlParserTests
    {
        [Fact]
        public void SqlSimple()
        {
            //Arrange
            var expected = new List<string> { "Col1", "Col2" };
            const string sql = @"SELECT Col1, Col2 FROM Table";

            //Act
            var actual = SqlParser.ParseColumnNames(sql);

            //Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SqlOneColumn()
        {
            //Arrange
            var expected = new List<string> { "Col1" };
            const string sql = @"SELECT Col1";

            //Act
            var actual = SqlParser.ParseColumnNames(sql);

            //Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SqlWithFunction()
        {
            //Arrange
            var expected = new List<string> { "Col1", "Col2" };
            const string sql =
                @"SELECT CASE WHEN ISNULL(Col1,'') IS NOT NULL THEN Col1 ELSE Col1 END AS Col1, 
Col2 
FROM Table";

            //Act
            var actual = SqlParser.ParseColumnNames(sql);

            //Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SqlWithSeveralFunctions()
        {
            //Arrange
            var expected = new List<string> { "Col1", "Col2" };
            const string sql =
                @"SELECT CONCAT('','Test',ISNULL(Col1,''), GETDATE()) AS Col1, 
( CONVERT(INT, Col2) * 5) Col2 
FROM Table";

            //Act
            var actual = SqlParser.ParseColumnNames(sql);

            //Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SqlSelectStart()
        {
            //Arrange
            var expected = new List<string>();
            const string sql = @"SELECT * FROM Table";

            //Act
            var actual = SqlParser.ParseColumnNames(sql);

            //Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SqlWithSchema()
        {
            //Arrange
            var expected = new List<string> { "Col1", "Col2" };
            const string sql = @"SELECT sou.Col1, sou.Col2 FROM table AS sou";

            //Act
            var actual = SqlParser.ParseColumnNames(sql);

            //Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SqlWithSchemaOneColumn()
        {
            //Arrange
            var expected = new List<string> { "Col1" };
            const string sql = @"SELECT sou.Col1 FROM table AS sou";

            //Act
            var actual = SqlParser.ParseColumnNames(sql);

            //Assert
            Assert.Equal(expected, actual);
        }
    }
}
