using System.Dynamic;
using EtlKit.TestOtherConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestOtherConnectors.MemoryDestination
{
    [Collection("OtherConnectors")]
    public class MemoryDestinationDynamicObjectTests : OtherConnectorsTestBase
    {
        public MemoryDestinationDynamicObjectTests(OtherConnectorsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void DataIsInList()
        {
            //Arrange
            TwoColumnsTableFixture source2Columns = new TwoColumnsTableFixture(
                "MemoryDestinationSource"
            );
            source2Columns.InsertTestData();

            DbSource<ExpandoObject> source = new DbSource<ExpandoObject>(
                SqlConnection,
                "MemoryDestinationSource"
            );
            MemoryDestination<ExpandoObject> dest = new MemoryDestination<ExpandoObject>();

            //Act
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            int index = 1;
            foreach (dynamic d in dest.Data)
            {
                Assert.True(d.Col1 == index && d.Col2 == "Test" + index);
                index++;
            }
        }
    }
}
