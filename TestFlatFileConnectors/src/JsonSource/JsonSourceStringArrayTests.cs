using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.JsonSource
{
    [Collection("FlatFilesToDatabase")]
    public class JsonSourceStringArrayTests : FlatFileConnectorsTestBase
    {
        public JsonSourceStringArrayTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void SimpleFlowWithStringArray()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("JsonSource2ColsNonGen");
            var dest = new DbDestination<string[]>(SqlConnection, "JsonSource2ColsNonGen");

            //Act
            var source = new JsonSource<string[]>(
                "res/JsonSource/TwoColumnsStringArray.json",
                ResourceType.File
            );
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }
    }
}
