using EtlKit.TestShared.Helper;

namespace EtlKit.TestFlatFileConnectors.Fixture
{
    public sealed class FlatFileToDatabaseFixture : IDisposable
    {
        private const string Section = "DataFlow";

        private readonly string _dbNameSuffix = null;

        public FlatFileToDatabaseFixture()
        {
            DatabaseHelper.RecreateDatabase(Config.SqlConnection, Section, _dbNameSuffix);
        }

        public void Dispose()
        {
            DatabaseHelper.DropDatabase(Config.SqlConnection, Section, _dbNameSuffix);
        }
    }
}
