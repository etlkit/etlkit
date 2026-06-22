using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestFlatFileConnectors.Helpers;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.JsonDestination
{
    [Collection("FlatFilesToDatabase")]
    public class JsonDestinationStringArrayTests : FlatFileConnectorsTestBase
    {
        public JsonDestinationStringArrayTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void SimpleNonGeneric()
        {
            //Arrange
            var s2C = new TwoColumnsTableFixture("JsonDestSimpleNonGeneric");
            s2C.InsertTestDataSet3();
            var source = new DbSource<string[]>(SqlConnection, "JsonDestSimpleNonGeneric");

            //Act
            var dest = new JsonDestination<string[]>("./SimpleNonGeneric.json", ResourceType.File);
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(
                File.ReadAllText("res/JsonDestination/TwoColumnsSet3StringArray.json")
                    .NormalizeLineEndings(),
                File.ReadAllText("./SimpleNonGeneric.json")
            );
        }
    }
}
