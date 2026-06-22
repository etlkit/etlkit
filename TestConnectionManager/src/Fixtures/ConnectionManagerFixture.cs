using System.Diagnostics.CodeAnalysis;
using EtlKit.TestShared.Helper;

namespace EtlKit.TestConnectionManager.Fixtures
{
    [CollectionDefinition("Connection Manager", DisableParallelization = true)]
    public class CollectionConnectionManagerFixture
        : ICollectionFixture<ConnectionManagerFixture> { }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    public sealed class ConnectionManagerFixture : IDisposable
    {
        public ConnectionManagerFixture()
        {
            DatabaseHelper.RecreateDatabase(Config.SqlConnection, "ConnectionManager");
            DatabaseHelper.RecreateDatabase(Config.PostgresConnection, "ConnectionManager");
        }

        public void Dispose()
        {
            DatabaseHelper.DropDatabase(Config.SqlConnection, "ConnectionManager");
            DatabaseHelper.DropDatabase(Config.PostgresConnection, "ConnectionManager");
        }
    }
}
