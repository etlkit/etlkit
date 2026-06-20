using System.Dynamic;
using EtlKit.TestOtherConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestOtherConnectors.CustomSource
{
    [Collection("OtherConnectors")]
    public class CustomSourceDynamicObjectTests : OtherConnectorsTestBase
    {
        public CustomSourceDynamicObjectTests(OtherConnectorsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void SimpleFlow()
        {
            //Arrange
            TwoColumnsTableFixture dest2Columns = new TwoColumnsTableFixture(
                "Destination4CustomSourceDynamic"
            );
            List<string> data = new List<string> { "Test1", "Test2", "Test3" };
            int readIndex = 0;

            ExpandoObject ReadData()
            {
                dynamic result = new ExpandoObject();
                result.Col1 = (readIndex + 1).ToString();
                result.Col2 = data[readIndex];
                readIndex++;
                return result;
            }

            bool EndOfData() => readIndex >= data.Count;

            //Act
            CustomSource<ExpandoObject> source = new CustomSource<ExpandoObject>(
                ReadData,
                EndOfData
            );
            DbDestination<ExpandoObject> dest = new DbDestination<ExpandoObject>(
                SqlConnection,
                "Destination4CustomSourceDynamic"
            );
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }
    }
}
