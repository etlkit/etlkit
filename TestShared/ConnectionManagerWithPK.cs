using EtlKit.Primitives;

namespace EtlKit.TestShared
{
    /// <summary>
    /// TheoryData model (IConnectionManager, WithPK)
    /// </summary>
    public class ConnectionManagerWithPK
    {
        /// <summary>
        /// Connection manager
        /// </summary>
        public IConnectionManager Connection { get; }

        /// <summary>
        /// Using primary key on test table with current connection manager
        /// </summary>
        public bool WithPK { get; }

        public ConnectionManagerWithPK(IConnectionManager connection)
        {
            Connection = connection;
            WithPK = connection.ConnectionManagerType switch
            {
                ConnectionManagerType.SQLite => false,
                _ => true,
            };
        }
    }
}
