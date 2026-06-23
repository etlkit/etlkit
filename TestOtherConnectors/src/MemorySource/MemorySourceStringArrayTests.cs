using EtlKit.TestOtherConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestOtherConnectors.MemorySource
{
    [Collection("OtherConnectors")]
    public class MemorySourceStringArrayTests : OtherConnectorsTestBase
    {
        public MemorySourceStringArrayTests(OtherConnectorsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void DataIsFromList()
        {
            //Arrange
            TwoColumnsTableFixture dest2Columns = new TwoColumnsTableFixture(
                "MemoryDestinationNonGeneric"
            );
            MemorySource<string[]> source = new MemorySource<string[]>();
            DbDestination<string[]> dest = new DbDestination<string[]>(
                SqlConnection,
                "MemoryDestinationNonGeneric"
            );

            //Act
            source.DataAsList = new List<string[]>
            {
                new[] { "1", "Test1" },
                new[] { "2", "Test2" },
                new[] { "3", "Test3" },
            };
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }
    }
}
