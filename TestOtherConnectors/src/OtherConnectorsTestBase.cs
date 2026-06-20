using EtlKit.TestOtherConnectors.Fixture;

namespace EtlKit.TestOtherConnectors
{
    [CollectionDefinition("OtherConnectors")]
    public class OtherConnectorsCollectionClass
        : ICollectionFixture<OtherConnectorsDatabaseFixture> { }

    [Collection("OtherConnectors")]
    public class OtherConnectorsTestBase
    {
        protected readonly OtherConnectorsDatabaseFixture Fixture;

        public OtherConnectorsTestBase(OtherConnectorsDatabaseFixture fixture)
        {
            Fixture = fixture;
        }

        protected static SqlConnectionManager SqlConnection =>
            Config.SqlConnection.ConnectionManager("DataFlow");
    }
}
