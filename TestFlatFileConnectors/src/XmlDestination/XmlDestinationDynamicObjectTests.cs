using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestFlatFileConnectors.Helpers;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.XmlDestination
{
    [Collection("FlatFilesToDatabase")]
    public class XmlDestinationDynamicObjectTests : FlatFileConnectorsTestBase
    {
        public XmlDestinationDynamicObjectTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void SimpleFlowWithObject()
        {
            //Arrange
            var s2C = new TwoColumnsTableFixture("XmlDestDynamic");
            s2C.InsertTestDataSet3();
            var source = new DbSource<ExpandoObject>(SqlConnection, "XmlDestDynamic");

            //Act
            var dest = new XmlDestination<ExpandoObject>(
                "./SimpleWithDynamicObject.xml",
                ResourceType.File
            );
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(
                File.ReadAllText("res/XmlDestination/TwoColumnsSet3DynamicObject.xml")
                    .NormalizeLineEndings(),
                File.ReadAllText("./SimpleWithDynamicObject.xml")
            );
        }
    }
}
