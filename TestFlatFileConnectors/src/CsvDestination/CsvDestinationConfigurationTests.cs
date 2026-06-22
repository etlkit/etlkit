using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.CsvDestination
{
    [Collection("FlatFilesToDatabase")]
    public class CsvDestinationConfigurationTests : FlatFileConnectorsTestBase
    {
        public CsvDestinationConfigurationTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        public class MySimpleRow
        {
            [Index(1)]
            public int Col1 { get; set; }

            [Index(2)]
            public string Col2 { get; set; }
        }

        [Fact]
        public void DisableHeader()
        {
            //Arrange
            var s2C = new TwoColumnsTableFixture("CsvSourceNoHeader");
            s2C.InsertTestData();
            var source = new DbSource<MySimpleRow>(SqlConnection, "CsvSourceNoHeader");

            //Act
            var dest = new CsvDestination<MySimpleRow>("./ConfigurationNoHeader.csv")
            {
                Configuration = { HasHeaderRecord = false },
            };
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(
                File.ReadAllText("./ConfigurationNoHeader.csv"),
                File.ReadAllText("res/CsvDestination/TwoColumnsNoHeader.csv")
            );
        }
    }
}
