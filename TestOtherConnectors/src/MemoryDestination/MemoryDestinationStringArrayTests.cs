using EtlKit.TestOtherConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestOtherConnectors.MemoryDestination
{
    [Collection("OtherConnectors")]
    public class MemoryDestinationStringArrayTests : OtherConnectorsTestBase
    {
        public MemoryDestinationStringArrayTests(OtherConnectorsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void DataIsInList()
        {
            //Arrange
            TwoColumnsTableFixture source2Columns = new TwoColumnsTableFixture(
                "MemoryDestinationNonGenericSource"
            );
            source2Columns.InsertTestData();

            DbSource<string[]> source = new DbSource<string[]>(
                SqlConnection,
                "MemoryDestinationNonGenericSource"
            );
            MemoryDestination<string[]> dest = new MemoryDestination<string[]>();

            //Act
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Collection(
                dest.Data,
                d => Assert.True(d[0] == "1" && d[1] == "Test1"),
                d => Assert.True(d[0] == "2" && d[1] == "Test2"),
                d => Assert.True(d[0] == "3" && d[1] == "Test3")
            );
        }
    }
}
