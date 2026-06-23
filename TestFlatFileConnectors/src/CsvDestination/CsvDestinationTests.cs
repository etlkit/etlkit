using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.CsvDestination
{
    [Collection("FlatFilesToDatabase")]
    public class CsvDestinationTests : FlatFileConnectorsTestBase
    {
        public CsvDestinationTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        public class MySimpleRow
        {
            [Name("Header2")]
            [Index(2)]
            public string Col2 { get; set; }

            [Name("Header1")]
            [Index(1)]
            public int Col1 { get; set; }
        }

        [Fact]
        public void SimpleFlowWithObject()
        {
            //Arrange
            var s2C = new TwoColumnsTableFixture("CSVDestSimple");
            s2C.InsertTestDataSet3();
            var source = new DbSource<MySimpleRow>(SqlConnection, "CSVDestSimple");

            //Act
            var dest = new CsvDestination<MySimpleRow>("./SimpleWithObject.csv");
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(
                File.ReadAllText("./SimpleWithObject.csv"),
                File.ReadAllText("res/CsvDestination/TwoColumnsSet3.csv")
            );
        }
    }
}
