using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.Sort
{
    [Collection("Transformations")]
    public class SortDynamicObjectTests : TransformationsTestBase
    {
        public SortDynamicObjectTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void SortSimpleDataDescending()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("SortSourceNonGeneric");
            source2Columns.InsertTestData();
            var source = new DbSource<ExpandoObject>(SqlConnection, "SortSourceNonGeneric");

            //Act
            var actual = new List<ExpandoObject>();
            var dest = new CustomDestination<ExpandoObject>(row => actual.Add(row));

            int Comp(ExpandoObject x, ExpandoObject y)
            {
                dynamic xo = x;
                dynamic yo = y;
                return yo.Col1 - xo.Col1;
            }

            var block = new Sort<ExpandoObject>(Comp);
            source.LinkTo(block);
            block.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            var expected = new List<int> { 3, 2, 1 };
            Assert.Equal(
                expected,
                actual
                    .Select(row =>
                    {
                        dynamic r = row;
                        return r.Col1;
                    })
                    .Cast<int>()
                    .ToList()
            );
        }
    }
}
