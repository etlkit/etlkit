using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.Sort
{
    [Collection("Transformations")]
    public class SortTests : TransformationsTestBase
    {
        public SortTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        private class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void SortSimpleDataDescending()
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture("SortSource");
            source2Columns.InsertTestData();
            var source = new DbSource<MySimpleRow>(SqlConnection, "SortSource");

            //Act
            var actual = new List<MySimpleRow>();
            var dest = new CustomDestination<MySimpleRow>(row => actual.Add(row));
            int Comp(MySimpleRow x, MySimpleRow y) => y.Col1 - x.Col1;
            var block = new Sort<MySimpleRow>(Comp);
            source.LinkTo(block);
            block.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            var expected = new List<int> { 3, 2, 1 };
            Assert.Equal(expected, actual.Select(row => row.Col1).ToList());
        }
    }
}
