using EtlKit.Primitives;

namespace EtlKit.TestShared.Helper;

/// <summary>
/// Connection details for a specific connection string name.
/// </summary>
/// <typeparam name="TConnectionString"></typeparam>
/// <typeparam name="TConnectionManager"></typeparam>
public class ConnectionDetails<TConnectionString, TConnectionManager>
    where TConnectionString : IDbConnectionString, new()
    where TConnectionManager : IConnectionManager, new()
{
    public ConnectionDetails(string connectionStringName)
    {
        ConnectionStringName = connectionStringName;
    }

    public string ConnectionStringName { get; set; }

    public string RawConnectionString(string section)
    {
        return Config.DefaultConfigFile.GetSection(section)[ConnectionStringName];
    }

    public TConnectionString ConnectionString(string section)
    {
        return new TConnectionString { Value = RawConnectionString(section) };
    }

    public TConnectionManager ConnectionManager(string section)
    {
        return new TConnectionManager { ConnectionString = ConnectionString(section) };
    }
}
