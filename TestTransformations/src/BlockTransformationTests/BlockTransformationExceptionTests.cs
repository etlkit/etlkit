using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.BlockTransformationTests
{
    [Collection("Transformations")]
    public class BlockTransformationExceptionTests : TransformationsTestBase
    {
        public BlockTransformationExceptionTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void ThrowExceptionWithoutHandling()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("BlockTransSource");
            source2Columns.InsertTestData();
            var _ = new TwoColumnsTableFixture("BlockTransDest");

            var source = new DbSource<MySimpleRow>(SqlConnection, "BlockTransSource");
            var dest = new DbDestination<MySimpleRow>(SqlConnection, "BlockTransDest");

            //Act
            var block = new BlockTransformation<MySimpleRow>(_ => throw new Exception("Test"));
            source.LinkTo(block);
            block.LinkTo(dest);

            //Assert
            Assert.Throws<AggregateException>(() =>
            {
                source.Execute();
                dest.Wait();
            });
        }
    }
}
