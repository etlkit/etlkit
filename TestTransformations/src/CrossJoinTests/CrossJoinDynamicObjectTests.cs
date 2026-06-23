using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.CrossJoinTests
{
    [Collection("Transformations")]
    public class CrossJoinDynamicObjectTests : TransformationsTestBase
    {
        public CrossJoinDynamicObjectTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void DynamicObjectJoin()
        {
            //Arrange
            var table1 = new TwoColumnsTableFixture(SqlConnection, "CrossJoinSource1");
            table1.InsertTestData();
            var table2 = new TwoColumnsTableFixture(SqlConnection, "CrossJoinSource2");
            table2.InsertTestData();
            var source1 = new DbSource<ExpandoObject>(SqlConnection, "CrossJoinSource1");
            var source2 = new DbSource<ExpandoObject>(SqlConnection, "CrossJoinSource2");
            var dest = new MemoryDestination();

            var crossJoin = new CrossJoin(
                (data1, _) =>
                {
                    dynamic d1 = data1;
                    dynamic d2 = data1;
                    dynamic res = new ExpandoObject();
                    res.Val = d1.Col1 + d2.Col2;
                    return res;
                }
            );

            //Act
            source1.LinkTo(crossJoin.InMemoryTarget);
            source2.LinkTo(crossJoin.PassingTarget);
            crossJoin.LinkTo(dest);
            source1.Execute();
            source2.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(9, dest.Data.Count);
        }
    }
}
