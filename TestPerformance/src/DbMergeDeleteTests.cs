using EtlKit.Common;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.TestPerformance.Fixtures;
using EtlKit.TestShared.Helper;

namespace EtlKit.TestPerformance
{
    [Collection("Performance")]
    public class DbMergeDeleteTests : PerformanceTestBase
    {
        public DbMergeDeleteTests(PerformanceDatabaseFixture fixture)
            : base(fixture) { }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        public class MyMergeRow : MergeableRow
        {
            [IdColumn]
            public Guid Id { get; set; }

            [CompareColumn]
            public DateTimeOffset LastUpdated { get; set; }

            public string Value { get; set; }
        }

        [Theory, InlineData(10000, 5000)]
        public void NoUpdateWithGuid(int rowsInDest, int rowsInSource)
        {
            //Arrange
            CreateDestinationTable("MergeDestination");
            List<MyMergeRow> knownGuids = CreateTestData(rowsInSource);
            TransferTestDataIntoDestination(knownGuids);
            MemorySource<MyMergeRow> source = AddNewTestData(rowsInDest, knownGuids);
            var mergeDest = new DbMerge<MyMergeRow>(SqlConnectionManager, "MergeDestination");
            source.LinkTo(mergeDest);

            //Act
            var executionTime = BigDataHelper.LogExecutionTime(
                "Execute merge",
                () =>
                {
                    source.Execute();
                    mergeDest.Wait();
                }
            );

            //Assert
            Assert.Equal(
                rowsInDest + rowsInSource,
                RowCountTask.Count(SqlConnectionManager, "MergeDestination") ?? 0
            );
            Assert.InRange(
                executionTime,
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds((rowsInDest + rowsInSource) * 2)
            );
        }

        private static void CreateDestinationTable(string tableName)
        {
            DropTableTask.DropIfExists(SqlConnectionManager, tableName);
            CreateTableTask.Create(
                SqlConnectionManager,
                tableName,
                new List<TableColumn>
                {
                    new("Id", "UNIQUEIDENTIFIER", allowNulls: false, isPrimaryKey: true),
                    new("LastUpdated", "DATETIMEOFFSET", allowNulls: false),
                    new("Value", "CHAR(1)", allowNulls: false),
                }
            );
        }

        private static List<MyMergeRow> CreateTestData(int rowsInSource)
        {
            var knownGuids = new List<MyMergeRow>();
            for (var i = 0; i < rowsInSource; i++)
                knownGuids.Add(
                    new MyMergeRow
                    {
                        Id = Guid.NewGuid(),
                        LastUpdated = DateTime.Now,
                        Value = HashHelper.RandomString(1),
                    }
                );
            return knownGuids;
        }

        private void TransferTestDataIntoDestination(List<MyMergeRow> knownGuids)
        {
            var source = new MemorySource<MyMergeRow> { DataAsList = knownGuids };
            var dest = new DbDestination<MyMergeRow>(SqlConnectionManager, "MergeDestination");
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();
        }

        private static MemorySource<MyMergeRow> AddNewTestData(
            int rowsInDest,
            List<MyMergeRow> knownGuids
        )
        {
            var source = new MemorySource<MyMergeRow> { DataAsList = knownGuids };
            for (var i = 0; i < rowsInDest; i++)
                knownGuids.Add(
                    new MyMergeRow
                    {
                        Id = Guid.NewGuid(),
                        LastUpdated = DateTime.Now,
                        Value = HashHelper.RandomString(1),
                    }
                );
            return source;
        }
    }
}
