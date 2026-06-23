using EtlKit.Primitives;
using EtlKit.TestConnectionManager.Fixtures;
using EtlKit.TestShared.Helper;

namespace EtlKit.TestConnectionManager
{
    [Collection("Connection Manager")]
    public class ConnectionManagerTestBase
    {
        private readonly ConnectionManagerType _connectionManagerType;

        protected ConnectionManagerTestBase(
            ConnectionManagerType connectionManagerType,
            ConnectionManagerFixture fixture
        )
        {
            _connectionManagerType = connectionManagerType;
        }

        protected string ConnectionStringParameter =>
            _connectionManagerType switch
            {
                ConnectionManagerType.SqlServer => Config.SqlConnection.RawConnectionString(
                    "ConnectionManager"
                ),
                ConnectionManagerType.Postgres => Config.PostgresConnection.RawConnectionString(
                    "ConnectionManager"
                ),
                _ => throw new NotSupportedException(
                    $"Provider '{_connectionManagerType}' not implemented"
                ),
            };
    }
}
