using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.ExcelSource
{
    [Collection("FlatFilesToDatabase")]
    public class ExcelSourceNameAttributeTests : FlatFileConnectorsTestBase
    {
        public ExcelSourceNameAttributeTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        public class MySimpleRow
        {
            [ExcelColumn("Col1")]
            [ColumnMap("Col1")]
            public int Column1 { get; set; }

            [ExcelColumn("Col2")]
            [ColumnMap("Col2")]
            public string Column2 { get; set; }
            public string ExtraColumn { get; set; }
        }

        [Fact]
        public void SimpleData()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("ExcelDestinationWithNameAttribute");
            var source = new ExcelSource<MySimpleRow>("res/Excel/TwoColumnWithHeader.xlsx");
            var dest = new DbDestination<MySimpleRow>(
                SqlConnection,
                "ExcelDestinationWithNameAttribute"
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
