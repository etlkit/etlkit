using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.CsvDestination
{
    [Collection("FlatFilesToDatabase")]
    public class CsvDestinationStringArrayTests : FlatFileConnectorsTestBase
    {
        public CsvDestinationStringArrayTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void SimpleNonGeneric()
        {
            //Arrange
            var s2C = new TwoColumnsTableFixture("CSVDestSimpleNonGeneric");
            s2C.InsertTestDataSet3();
            var source = new DbSource<string[]>(SqlConnection, "CSVDestSimpleNonGeneric");

            //Act
            var dest = new CsvDestination<string[]>("./SimpleNonGeneric.csv");
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            //Assert
            Assert.Equal(
                File.ReadAllText("./SimpleNonGeneric.csv"),
                File.ReadAllText("res/CsvDestination/TwoColumnsSet3NoHeader.csv")
            );
        }
    }
}
