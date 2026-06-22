using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.CsvDestination
{
    [Collection("FlatFilesToDatabase")]
    public class CsvDestinationDynamicObjectTests : FlatFileConnectorsTestBase
    {
        public CsvDestinationDynamicObjectTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void SimpleFlow()
        {
            //Arrange
            var s2C = new TwoColumnsTableFixture("CSVDestDynamicObject");
            s2C.InsertTestDataSet3();
            var source = new DbSource<ExpandoObject>(SqlConnection, "CSVDestDynamicObject");

            //Act
            var dest = new CsvDestination<ExpandoObject>("./SimpleWithDynamicObject.csv");
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(
                File.ReadAllText("./SimpleWithDynamicObject.csv"),
                File.ReadAllText("res/CsvDestination/TwoColumnsSet3DynamicObject.csv")
            );
        }
    }
}
