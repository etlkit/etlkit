using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.MergeJoin
{
    [Collection("Transformations")]
    public class MergeJoinTests : TransformationsTestBase
    {
        public MergeJoinTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void MergeJoinUsingOneObject()
        {
            //Arrange
            var source1Table = new TwoColumnsTableFixture("MergeJoinSource1");
            source1Table.InsertTestData();
            var source2Table = new TwoColumnsTableFixture("MergeJoinSource2");
            source2Table.InsertTestDataSet2();
            var _ = new TwoColumnsTableFixture("MergeJoinDestination");

            var source1 = new DbSource<MySimpleRow>(SqlConnection, "MergeJoinSource1");
            var source2 = new DbSource<MySimpleRow>(SqlConnection, "MergeJoinSource2");
            var dest = new DbDestination<MySimpleRow>(SqlConnection, "MergeJoinDestination");

            //Act
            var join = new MergeJoin<MySimpleRow, MySimpleRow, MySimpleRow>(
                (inputRow1, inputRow2) =>
                {
                    inputRow1.Col1 += inputRow2.Col1;
                    inputRow1.Col2 += inputRow2.Col2;
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
            Assert.Equal(3, RowCountTask.Count(SqlConnection, "MergeJoinDestination"));
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "MergeJoinDestination",
                    "Col1 = 5 AND Col2='Test1Test4'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "MergeJoinDestination",
                    "Col1 = 7 AND Col2='Test2Test5'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "MergeJoinDestination",
                    "Col1 = 9 AND Col2='Test3Test6'"
                )
            );
        }
    }
}
