using System.Threading.Tasks;
using EtlKit;
using EtlKit.ControlFlow;
using EtlKit.TestControlFlowTasks.Fixtures;
using EtlKit.TestShared.Attributes;

namespace EtlKit.TestControlFlowTasks;

public class SqlTaskInParallelTests : ControlFlowTestBase
{
    public SqlTaskInParallelTests(ControlFlowDatabaseFixture fixture)
        : base(fixture)
    {
        CreateTableTask.Create(
            SqlConnection,
            "FastParallel",
            new List<TableColumn> { new("id", "int") }
        );
    }

    [MultiprocessorOnlyFact]
    public void FastExecutingSqlsInParallel()
    {
        //Arrange
        var array = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        //Act
        Parallel.ForEach(
            array,
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            curNr =>
                SqlTask.ExecuteNonQuery(
                    SqlConnection,
                    $"Test statement {curNr}",
                    $"INSERT INTO FastParallel VALUES({curNr})"
                )
        );
        //Assert
        Assert.Equal(10, RowCountTask.Count(SqlConnection, "FastParallel"));
    }

    [MultiprocessorOnlyFact]
    [Trait("Category", "Performance")]
    public void LongExecutingSqlTaskInParallel()
    {
        //Arrange
        var array = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        //Act
        Parallel.ForEach(
            array,
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            curNr =>
                SqlTask.ExecuteNonQuery(
                    SqlConnection,
                    $"Test statement {curNr}",
                    $@"
                    DECLARE @counter INT = 0;
                    CREATE TABLE dbo.LongParallel{curNr} (
                        Col1 nvarchar(50)
                    )
                    WHILE @counter < 5000
                    BEGIN
                        SET @counter = @counter + 1;
                        INSERT INTO dbo.LongParallel{curNr} values('Lorem ipsum Lorem ipsum Lorem ipsum Lorem')
                    END
                "
                )
        );
        //Assert
        Parallel.ForEach(
            array,
            curNr => Assert.Equal(5000, RowCountTask.Count(SqlConnection, $"LongParallel{curNr}"))
        );
    }
}
