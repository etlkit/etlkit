using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.BlockTransformationTests
{
    [Collection("Transformations")]
    public class BlockTransformationStringArrayTests : TransformationsTestBase
    {
        public BlockTransformationStringArrayTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void ModifyInputDataList()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("BlockTransSourceNonGeneric");
            source2Columns.InsertTestData();
            var _ = new TwoColumnsTableFixture("BlockTransDestNonGeneric");

            var source = new DbSource<string[]>(SqlConnection, "BlockTransSourceNonGeneric");
            var dest = new DbDestination<string[]>(SqlConnection, "BlockTransDestNonGeneric");

            //Act
            var block = new BlockTransformation<string[]>(inputData =>
            {
                inputData.RemoveRange(1, 2);
                inputData.Add(new[] { "4", "Test4" });
                return inputData;
            });
            source.LinkTo(block);
            block.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(2, RowCountTask.Count(SqlConnection, "BlockTransDestNonGeneric"));
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "BlockTransDestNonGeneric",
                    "Col1 = 1 AND Col2='Test1'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "BlockTransDestNonGeneric",
                    "Col1 = 4 AND Col2='Test4'"
                )
            );
        }
    }
}
