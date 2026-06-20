using EtlKit;
using EtlKit.Common;
using EtlKit.ControlFlow;

namespace EtlKit.TestControlFlowTasks
{
    public class NoConnectionManagerTests
    {
        [Fact]
        public void CheckSqlTask()
        {
            //Arrange
            //Act & Assert
            Assert.Throws<EtlKitException>(() =>
            {
                SqlTask.ExecuteNonQuery("test", "SELECT 1");
            });
        }

        [Fact]
        public void CheckRowCountTask()
        {
            //Arrange
            //Act & Assert
            Assert.Throws<EtlKitException>(() =>
            {
                RowCountTask.Count("test");
            });
        }

        [Fact]
        public void CheckCreateTableTask()
        {
            //Arrange
            //Act & Assert
            Assert.Throws<EtlKitException>(() =>
            {
                CreateTableTask.Create("test", new List<TableColumn>());
            });
        }

        [Fact]
        public void CheckIfExistsDatabaseTask()
        {
            //Arrange
            //Act & Assert
            Assert.Throws<EtlKitException>(() =>
            {
                IfDatabaseExistsTask.IsExisting("test");
            });
        }

        [Fact]
        public void CheckCreateSchemaTask()
        {
            //Arrange
            //Act & Assert
            Assert.Throws<EtlKitException>(() =>
            {
                CreateSchemaTask.Create("test");
            });
        }

        [Fact]
        public void CheckDropSchemaTask()
        {
            //Arrange
            //Act & Assert
            Assert.Throws<EtlKitException>(() =>
            {
                DropSchemaTask.DropIfExists("test");
            });
        }
    }
}
