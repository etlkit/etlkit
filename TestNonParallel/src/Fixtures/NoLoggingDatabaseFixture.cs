using EtlKit.ConnectionManager;
using EtlKit.TestShared.Helper;
using JetBrains.Annotations;

namespace EtlKit.TestNonParallel.Fixtures
{
    [UsedImplicitly]
    public sealed class NoLoggingDatabaseFixture : IDisposable
    {
        public NoLoggingDatabaseFixture()
        {
            DatabaseHelper.RecreateDatabase(Config.SqlConnection, "NoLog");
        }

        public static SqlConnectionManager SqlConnection =>
            Config.SqlConnection.ConnectionManager("NoLog");

        public void Dispose()
        {
            DatabaseHelper.DropDatabase(Config.SqlConnection, "NoLog");
        }
    }
}
