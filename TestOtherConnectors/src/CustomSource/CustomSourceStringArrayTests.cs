using EtlKit.TestOtherConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestOtherConnectors.CustomSource
{
    [Collection("OtherConnectors")]
    public class CustomSourceStringArrayTests : OtherConnectorsTestBase
    {
        public CustomSourceStringArrayTests(OtherConnectorsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void SimpleFlow()
        {
            //Arrange
            TwoColumnsTableFixture dest2Columns = new TwoColumnsTableFixture(
                "Destination4CustomSourceNonGeneric"
            );
            List<string> data = new List<string> { "Test1", "Test2", "Test3" };
            int readIndex = 0;

            string[] ReadData()
            {
                string[] result = new string[2];
                result[0] = (readIndex + 1).ToString();
                result[1] = data[readIndex];
                readIndex++;
                return result;
            }

            bool EndOfData() => readIndex >= data.Count;

            //Act
            CustomSource<string[]> source = new CustomSource<string[]>(ReadData, EndOfData);
            DbDestination<string[]> dest = new DbDestination<string[]>(
                SqlConnection,
                "Destination4CustomSourceNonGeneric"
            );
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }
    }
}
