using System.Diagnostics.CodeAnalysis;
using EtlKit.TestShared.Helper;

namespace EtlKit.TestPerformance.ETLBoxTests.Performance.Fixtures
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    public sealed class PerformanceDatabaseFixture : IDisposable
    {
        public PerformanceDatabaseFixture()
        {
            DatabaseHelper.RecreateDatabase(Config.SqlConnection, "Performance");
            DatabaseHelper.RecreateDatabase(Config.MySqlConnection, "Performance");
            DatabaseHelper.RecreateDatabase(Config.PostgresConnection, "Performance");
        }

        public void Dispose()
        {
            DatabaseHelper.DropDatabase(Config.PostgresConnection, "Performance");
            DatabaseHelper.DropDatabase(Config.MySqlConnection, "Performance");
            DatabaseHelper.DropDatabase(Config.SqlConnection, "Performance");
        }
    }
}
