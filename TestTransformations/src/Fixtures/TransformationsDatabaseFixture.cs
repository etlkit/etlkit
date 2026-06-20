using EtlKit.TestShared.Helper;

namespace EtlKit.TestTransformations.Fixtures
{
    public sealed class TransformationsDatabaseFixture : IDisposable
    {
        private const string Section = "DataFlow";

        private readonly string _dbNameSuffix = null;

        public TransformationsDatabaseFixture()
        {
            DatabaseHelper.RecreateDatabase(Config.SqlConnection, Section, _dbNameSuffix);
            DatabaseHelper.RecreateDatabase(Config.MySqlConnection, Section, _dbNameSuffix);
            DatabaseHelper.RecreateDatabase(Config.PostgresConnection, Section, _dbNameSuffix);
        }

        public void Dispose()
        {
            DatabaseHelper.DropDatabase(Config.SqlConnection, Section, _dbNameSuffix);
            DatabaseHelper.DropDatabase(Config.MySqlConnection, Section, _dbNameSuffix);
            DatabaseHelper.DropDatabase(Config.PostgresConnection, Section, _dbNameSuffix);
        }
    }
}
