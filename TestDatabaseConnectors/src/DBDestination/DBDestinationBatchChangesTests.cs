using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBDestination
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbDestinationBatchChangesTests : DatabaseConnectorsTestBase
    {
        public DbDestinationBatchChangesTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void WithBatchChanges(IConnectionManager connection)
        {
            //Arrange
            TwoColumnsTableFixture d2C = new TwoColumnsTableFixture(
                connection,
                "DbDestinationBatchChanges"
            );
            DbDestination<string[]> dest = new DbDestination<string[]>(
                connection,
                "DbDestinationBatchChanges",
                batchSize: 2
            )
            {
                BeforeBatchWrite = rowArray =>
                {
                    rowArray[0][1] = "NewValue";
                    return rowArray;
                },
            };

            //Act
            CsvSource<string[]> source = new CsvSource<string[]>("res/BatchChanges/TwoColumns.csv");
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(3, RowCountTask.Count(connection, "DbDestinationBatchChanges"));
            Assert.Equal(
                2,
                RowCountTask.Count(
                    connection,
                    "DbDestinationBatchChanges",
                    $"{d2C.QB}Col2{d2C.QE}='NewValue'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    "DbDestinationBatchChanges",
                    $"{d2C.QB}Col1{d2C.QE} = 2 AND {d2C.QB}Col2{d2C.QE}='Test2'"
                )
            );
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void AfterBatchWrite(IConnectionManager connection)
        {
            //Arrange
            var wasExecuted = false;
            _ = new TwoColumnsTableFixture(connection, "DbDestinationBatchChanges");
            DbDestination<string[]> dest = new DbDestination<string[]>(
                connection,
                "DbDestinationBatchChanges",
                batchSize: 1
            )
            {
                AfterBatchWrite = rowArray =>
                {
                    Assert.True(rowArray.Length == 1);
                    wasExecuted = true;
                },
            };

            //Act
            CsvSource<string[]> source = new CsvSource<string[]>("res/BatchChanges/TwoColumns.csv");
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(3, RowCountTask.Count(connection, "DbDestinationBatchChanges"));
            Assert.True(wasExecuted);
        }
    }
}
