using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.Sort
{
    [Collection("Transformations")]
    public class SortStringArrayTests : TransformationsTestBase
    {
        public SortStringArrayTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void SortSimpleDataDescending()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("SortSourceNonGeneric");
            source2Columns.InsertTestData();
            var source = new DbSource<string[]>(SqlConnection, "SortSourceNonGeneric");

            //Act
            var actual = new List<string[]>();
            var dest = new CustomDestination<string[]>(row => actual.Add(row));
            int Comp(string[] x, string[] y) => int.Parse(y[0]) - int.Parse(x[0]);
            var block = new Sort<string[]>(Comp);
            source.LinkTo(block);
            block.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            var expected = new List<int> { 3, 2, 1 };
            Assert.Equal(expected, actual.Select(row => int.Parse(row[0])).ToList());
        }
    }
}
