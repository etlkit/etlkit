using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.RowTransformation
{
    [Collection("Transformations")]
    public class RowTransformationTests : TransformationsTestBase
    {
        public RowTransformationTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void ConvertIntoObject()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("DestinationRowTransformation");
            var source = new CsvSource<string[]>("res/RowTransformation/TwoColumns.csv");

            //Act
            var trans = new RowTransformation<string[], MySimpleRow>(csvdata => new MySimpleRow
            {
                Col1 = int.Parse(csvdata[0]),
                Col2 = csvdata[1],
            });
            var dest = new DbDestination<MySimpleRow>(
                SqlConnection,
                "DestinationRowTransformation"
            );
            source.LinkTo(trans);
            trans.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }

        [Fact]
        public void InitAction()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("DestinationRowTransformation");
            var source = new CsvSource<MySimpleRow>("res/RowTransformation/TwoColumnsIdMinus1.csv");

            //Act
            var IdOffset = 0;
            var trans = new RowTransformation<MySimpleRow, MySimpleRow>(
                "RowTransformation testing init Action",
                row =>
                {
                    row.Col1 += IdOffset;
                    return row;
                },
                () => IdOffset += 1
            );
            var dest = new DbDestination<MySimpleRow>(
                SqlConnection,
                "DestinationRowTransformation"
            );
            source.LinkTo(trans);
            trans.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }
    }
}
