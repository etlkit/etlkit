using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.CsvSource
{
    [Collection("FlatFilesToDatabase")]
    public class CsvSourceTests : FlatFileConnectorsTestBase
    {
        public CsvSourceTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        public class MySimpleRow
        {
            [Name("Header2")]
            public string Col2 { get; set; }

            [Name("Header1")]
            public int Col1 { get; set; }
        }

        [Fact]
        public void SimpleFlowWithObject()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("CsvSource2Cols");
            var dest = new DbDestination<MySimpleRow>(SqlConnection, "CsvSource2Cols");

            //Act
            var source = new CsvSource<MySimpleRow>("res/CsvSource/TwoColumns.csv");
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }

        [Fact]
        public void CSVGenericWithSkipRows_DB()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("CsvSourceSkipRows");
            var dest = new DbDestination<MySimpleRow>(SqlConnection, "CsvSourceSkipRows");

            //Act
            var source = new CsvSource<MySimpleRow>("res/CsvSource/TwoColumnsSkipRows.csv")
            {
                SkipRows = 2,
            };
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }
    }
}
