using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestFlatFileConnectors.Helpers;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.JsonDestination
{
    [Collection("FlatFilesToDatabase")]
    public class JsonDestinationTests : FlatFileConnectorsTestBase
    {
        public JsonDestinationTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        [UsedImplicitly(ImplicitUseTargetFlags.Members)]
        public class MySimpleRow
        {
            public string Col2 { get; set; }
            public int Col1 { get; set; }
        }

        [Fact]
        public void SimpleFlowWithObject()
        {
            //Arrange
            var s2C = new TwoColumnsTableFixture("JsonDestSimple");
            s2C.InsertTestDataSet3();
            var source = new DbSource<MySimpleRow>(SqlConnection, "JsonDestSimple");

            //Act
            var dest = new JsonDestination<MySimpleRow>(
                "./SimpleWithObject.json",
                ResourceType.File
            );
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(
                File.ReadAllText("res/JsonDestination/TwoColumnsSet3.json").NormalizeLineEndings(),
                File.ReadAllText("./SimpleWithObject.json")
            );
        }
    }
}
