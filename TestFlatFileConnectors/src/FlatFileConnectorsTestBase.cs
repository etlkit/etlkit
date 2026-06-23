using EtlKit.ConnectionManager;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.Helper;

namespace EtlKit.TestFlatFileConnectors
{
    [CollectionDefinition("FlatFilesToDatabase")]
    public class DataFlowCollectionClass : ICollectionFixture<FlatFileToDatabaseFixture> { }

    [Collection("FlatFilesToDatabase")]
    public class FlatFileConnectorsTestBase
    {
        protected readonly FlatFileToDatabaseFixture Fixture;

        public FlatFileConnectorsTestBase(FlatFileToDatabaseFixture fixture)
        {
            Fixture = fixture;
        }

        protected static SqlConnectionManager SqlConnection =>
            Config.SqlConnection.ConnectionManager("DataFlow");
    }
}
