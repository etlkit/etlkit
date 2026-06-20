using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.BlockTransformationTests
{
    [Collection("Transformations")]
    public class BlockTransformationTests : TransformationsTestBase
    {
        public BlockTransformationTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void ModifyInputDataList()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("BlockTransSource");
            source2Columns.InsertTestData();
            var _ = new TwoColumnsTableFixture("BlockTransDest");

            var source = new DbSource<MySimpleRow>(SqlConnection, "BlockTransSource");
            var dest = new DbDestination<MySimpleRow>(SqlConnection, "BlockTransDest");

            //Act
            var block = new BlockTransformation<MySimpleRow>(inputData =>
            {
                inputData.RemoveRange(1, 2);
                inputData.Add(new MySimpleRow { Col1 = 4, Col2 = "Test4" });
                return inputData;
            });
            source.LinkTo(block);
            block.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(2, RowCountTask.Count(SqlConnection, "BlockTransDest"));
            Assert.Equal(
                1,
                RowCountTask.Count(SqlConnection, "BlockTransDest", "Col1 = 1 AND Col2='Test1'")
            );
            Assert.Equal(
                1,
                RowCountTask.Count(SqlConnection, "BlockTransDest", "Col1 = 4 AND Col2='Test4'")
            );
        }

        public class MyOtherRow
        {
            [ColumnMap("Col1")]
            public int Col3 { get; set; }

            [ColumnMap("Col2")]
            public string Col4 { get; set; }
        }

        [Fact]
        public void ConvertObjects()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("BlockTransSource");
            source2Columns.InsertTestData();
            var dest2Columns = new TwoColumnsTableFixture("BlockTransDest");

            var source = new DbSource<MySimpleRow>(SqlConnection, "BlockTransSource");
            var dest = new DbDestination<MyOtherRow>(SqlConnection, "BlockTransDest");

            //Act
            var block = new BlockTransformation<MySimpleRow, MyOtherRow>(inputData =>
            {
                return inputData
                    .Select(row => new MyOtherRow { Col3 = row.Col1, Col4 = row.Col2 })
                    .ToList();
            });
            source.LinkTo(block);
            block.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }
    }
}
