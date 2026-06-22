using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.XmlSource
{
    [Collection("FlatFilesToDatabase")]
    public class XmlSourceErrorLinkingTests : FlatFileConnectorsTestBase
    {
        public XmlSourceErrorLinkingTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void WithObjectErrorLinking()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("XmlSourceErrorLinking");
            var dest = new DbDestination<MySimpleRow>(SqlConnection, "XmlSourceErrorLinking");
            var errorDest = new MemoryDestination<EtlKitError>();

            //Act
            var source = new XmlSource<MySimpleRow>(
                "res/XmlSource/TwoColumnsErrorLinking.xml",
                ResourceType.File
            );

            source.LinkTo(dest);
            source.LinkErrorTo(errorDest);
            source.Execute();
            dest.Wait();
            errorDest.Wait();

            //Assert
            dest2Columns.AssertTestData();
            Assert.Collection(
                errorDest.Data,
                d =>
                    Assert.True(
                        !string.IsNullOrEmpty(d.RecordAsJson) && !string.IsNullOrEmpty(d.ErrorText)
                    )
            );
        }

        [Fact]
        public void WithoutErrorLinking()
        {
            //Arrange
            var dest = new MemoryDestination<MySimpleRow>();

            //Act
            var source = new XmlSource<MySimpleRow>(
                "res/XmlSource/TwoColumnsErrorLinking.xml",
                ResourceType.File
            );

            //Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                source.LinkTo(dest);
                source.Execute();
                dest.Wait();
            });
        }
    }
}
