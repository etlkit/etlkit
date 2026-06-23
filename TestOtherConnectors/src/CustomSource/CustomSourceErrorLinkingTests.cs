using EtlKit.Primitives;
using EtlKit.TestOtherConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestOtherConnectors.CustomSource
{
    [Collection("OtherConnectors")]
    public class CustomSourceErrorLinkingTests : OtherConnectorsTestBase
    {
        public CustomSourceErrorLinkingTests(OtherConnectorsDatabaseFixture fixture)
            : base(fixture) { }

        [Serializable]
        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void SimpleFlow()
        {
            //Arrange
            TwoColumnsTableFixture dest2Columns = new TwoColumnsTableFixture(
                "ErrorLinkingCustomSource"
            );
            List<string> data = new List<string> { "Test1", "Test2", "Test3", "Test4" };
            int readIndex = 0;

            MySimpleRow ReadData()
            {
                var result = new MySimpleRow { Col1 = readIndex + 1, Col2 = data[readIndex] };
                readIndex++;
                if (readIndex == 4)
                    throw new Exception("Error record!");
                return result;
            }

            bool EndOfData() => readIndex >= data.Count;

            CustomSource<MySimpleRow> source = new CustomSource<MySimpleRow>(ReadData, EndOfData);
            DbDestination<MySimpleRow> dest = new DbDestination<MySimpleRow>(
                SqlConnection,
                "ErrorLinkingCustomSource"
            );
            MemoryDestination<EtlKitError> errorDest = new MemoryDestination<EtlKitError>();

            //Act
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
    }
}
