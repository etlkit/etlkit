using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.ExcelSource
{
    [Collection("FlatFilesToDatabase")]
    public class ExcelSourceHeaderTests : FlatFileConnectorsTestBase
    {
        public ExcelSourceHeaderTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
            public string ExtraColumn { get; set; }
        }

        [Fact]
        public void SimpleData()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("ExcelDestinationWithHeader");
            var source = new ExcelSource<MySimpleRow>("res/Excel/TwoColumnWithHeader.xlsx");
            var dest = new DbDestination<MySimpleRow>(SqlConnection, "ExcelDestinationWithHeader");

            //Act
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }

        [Fact]
        public void MoreHeaderColumns()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("ExcelDestinationManyColsWithHeader");
            var source = new ExcelSource<MySimpleRow>("res/Excel/ManyColumnsWithHeader.xlsx");
            var dest = new DbDestination<MySimpleRow>(
                SqlConnection,
                "ExcelDestinationManyColsWithHeader"
            );

            //Act
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }
    }
}
