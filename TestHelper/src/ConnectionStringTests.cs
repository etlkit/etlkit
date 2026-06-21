using EtlKit;

namespace EtlKit.TestHelper
{
    public class ConnectionStringTests
    {
        [Fact]
        public void NormalSqlServerString()
        {
            //Arrange
            var connString = new SqlConnectionString
            {
                //Act
                Value = "Server=.;User Id=test;Password=test;Database=TestDB;",
            };

            var withoutDbName = connString.CloneWithoutDbName().Value;
            var withMaster = connString.CloneWithMasterDbName().Value;
            var newDbName = connString.CloneWithNewDbName("test").Value;

            //Assert
            Assert.Equal("Data Source=.;User ID=test;Password=test", withoutDbName);
            Assert.Equal(
                "Data Source=.;Initial Catalog=master;User ID=test;Password=test",
                withMaster
            );
            Assert.Equal(
                "Data Source=.;Initial Catalog=test;User ID=test;Password=test",
                newDbName
            );
        }

        [Fact]
        public void NormalSQLiteString()
        {
            //Arrange
            var connString = new SQLiteConnectionString
            {
                Value =
                    $"Data Source=.{Path.DirectorySeparatorChar}db{Path.DirectorySeparatorChar}SQLiteControlFlow.db;",
            };

            //Act
            var withoutDbName = connString.CloneWithoutDbName().Value;
            var newDbName = connString.CloneWithNewDbName("test").Value;

            //Assert
            Assert.Equal("", withoutDbName);
            Assert.Equal("Data Source=test", newDbName);
            Assert.Throws<EtlKitNotSupportedException>(
                () => connString.CloneWithMasterDbName().Value
            );
        }

        [Fact]
        public void NormalPostgresString()
        {
            //Arrange
            var connString = new PostgresConnectionString
            {
                //Act
                Value =
                    "Server=10.211.55.2;Database=TestDb;User Id=postgres;Password=etlkitpassword;",
            };

            var withoutDbName = connString.CloneWithoutDbName().Value;
            var withMaster = connString.CloneWithMasterDbName().Value;
            var newDbName = connString.CloneWithNewDbName("test").Value;

            //Assert
            Assert.Equal(
                "Host=10.211.55.2;Username=postgres;Password=etlkitpassword",
                withoutDbName
            );
            Assert.Equal(
                "Host=10.211.55.2;Database=postgres;Username=postgres;Password=etlkitpassword",
                withMaster
            );
            Assert.Equal(
                "Host=10.211.55.2;Database=test;Username=postgres;Password=etlkitpassword",
                newDbName
            );
        }

        [Fact]
        public void NormalMySqlString()
        {
            //Arrange
            var connString = new MySqlConnectionString
            {
                //Act
                Value = "Server=10.211.55.2;Database=TestDb;Uid=root;Pwd=etlkitpassword;",
            };

            var withoutDbName = connString.CloneWithoutDbName().Value;
            var withMaster = connString.CloneWithMasterDbName().Value;
            var newDbName = connString.CloneWithNewDbName("test").Value;

            //Assert
            Assert.Equal("server=10.211.55.2;user id=root;password=etlkitpassword", withoutDbName);
            Assert.Equal(
                "server=10.211.55.2;database=mysql;user id=root;password=etlkitpassword",
                withMaster
            );
            Assert.Equal(
                "server=10.211.55.2;database=test;user id=root;password=etlkitpassword",
                newDbName
            );
        }
    }
}
