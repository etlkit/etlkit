using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.CsvSource
{
    [Collection("FlatFilesToDatabase")]
    public class CsvSourceNoHeaderTests : FlatFileConnectorsTestBase
    {
        public CsvSourceNoHeaderTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        public class MySimpleRow
        {
            [Index(1)]
            public string Col2 { get; set; }

            [Index(0)]
            public int Col1 { get; set; }
        }

        [Fact]
        public void CsvSourceNoHeader()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("CsvSourceNoHeader");
            var dest = new DbDestination<MySimpleRow>(SqlConnection, "CsvSourceNoHeader");

            //Act
            var source = new CsvSource<MySimpleRow>("res/CsvSource/TwoColumnsNoHeader.csv")
            {
                Configuration = { HasHeaderRecord = false },
            };
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }
    }
}
