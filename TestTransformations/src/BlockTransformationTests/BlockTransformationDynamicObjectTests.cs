using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.BlockTransformationTests
{
    [Collection("Transformations")]
    public class BlockTransformationDynamicObjectTests : TransformationsTestBase
    {
        public BlockTransformationDynamicObjectTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void ModifyInputDataList()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("BlockTransSourceDynamic");
            source2Columns.InsertTestData();
            var _ = new TwoColumnsTableFixture("BlockTransDestDynamic");

            var source = new DbSource<ExpandoObject>(SqlConnection, "BlockTransSourceDynamic");
            var dest = new DbDestination<ExpandoObject>(SqlConnection, "BlockTransDestDynamic");

            //Act
            var block = new BlockTransformation<ExpandoObject>(inputData =>
            {
                inputData.RemoveRange(1, 2);
                dynamic nr = new ExpandoObject();
                nr.Col1 = 4;
                nr.Col2 = "Test4";
                inputData.Add(nr);
                return inputData;
            });
            source.LinkTo(block);
            block.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(2, RowCountTask.Count(SqlConnection, "BlockTransDestDynamic"));
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "BlockTransDestDynamic",
                    "Col1 = 1 AND Col2='Test1'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "BlockTransDestDynamic",
                    "Col1 = 4 AND Col2='Test4'"
                )
            );
        }
    }
}
