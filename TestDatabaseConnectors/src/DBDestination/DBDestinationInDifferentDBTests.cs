using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBDestination
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbDestinationDifferentDBTests : DatabaseConnectorsTestBase
    {
        public DbDestinationDifferentDBTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        [Theory, MemberData(nameof(MixedSourceDestinations))]
        public void TestTransferBetweenDBs(Type sourceConnectionType, Type destConnectionType)
        {
            //Arrange
            IConnectionManager sourceConnection = GetConnectionManager(
                sourceConnectionType,
                DatabaseSourceDestinationFixture.SourceConfigSection
            );
            IConnectionManager destConnection = GetConnectionManager(
                destConnectionType,
                DatabaseSourceDestinationFixture.DestinationConfigSection
            );
            var source2Columns = new TwoColumnsTableFixture(sourceConnection, "Source");
            source2Columns.InsertTestData();
            var dest2Columns = new TwoColumnsTableFixture(destConnection, "Destination");

            //Act
            var source = new DbSource<string[]>(sourceConnection, "Source");
            var dest = new DbDestination<string[]>(destConnection, "Destination");
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }
    }
}
