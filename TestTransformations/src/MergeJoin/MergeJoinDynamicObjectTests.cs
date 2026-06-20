using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.MergeJoin
{
    [Collection("Transformations")]
    public class MergeJoinDynamicObjectTests : TransformationsTestBase
    {
        public MergeJoinDynamicObjectTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void MergeJoinUsingOneObject()
        {
            //Arrange
            var source1Table = new TwoColumnsTableFixture("MergeJoinDynamicSource1");
            source1Table.InsertTestData();
            var source2Table = new TwoColumnsTableFixture("MergeJoinDynamicSource2");
            source2Table.InsertTestDataSet2();
            var _ = new TwoColumnsTableFixture("MergeJoinDynamicDestination");

            var source1 = new DbSource<ExpandoObject>(SqlConnection, "MergeJoinDynamicSource1");
            var source2 = new DbSource<ExpandoObject>(SqlConnection, "MergeJoinDynamicSource2");
            var dest = new DbDestination<ExpandoObject>(
                SqlConnection,
                "MergeJoinDynamicDestination"
            );

            //Act
            var join = new MergeJoin<ExpandoObject>(
                (inputRow1, inputRow2) =>
                {
                    dynamic ir1 = inputRow1;
                    dynamic ir2 = inputRow2;
                    ir1.Col1 += ir2.Col1;
                    ir1.Col2 += ir2.Col2;
                    return inputRow1;
                }
            );
            source1.LinkTo(join.Target1);
            source2.LinkTo(join.Target2);
            join.LinkTo(dest);
            source1.Execute();
            source2.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(3, RowCountTask.Count(SqlConnection, "MergeJoinDynamicDestination"));
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "MergeJoinDynamicDestination",
                    "Col1 = 5 AND Col2='Test1Test4'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "MergeJoinDynamicDestination",
                    "Col1 = 7 AND Col2='Test2Test5'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "MergeJoinDynamicDestination",
                    "Col1 = 9 AND Col2='Test3Test6'"
                )
            );
        }
    }
}
