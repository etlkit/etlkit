using JetBrains.Annotations;

namespace EtlKit.Primitives
{
    /// <summary>
    /// The generic defintion of a connection string
    /// </summary>
    [PublicAPI]
    public interface IDbConnectionString
    {
        string Value { get; set; }
        string ToString();
        IDbConnectionString Clone();
        string DbName { get; set; }

        string MasterDbName { get; }
        IDbConnectionString CloneWithNewDbName(string value = null);
        IDbConnectionString CloneWithMasterDbName();
    }
}
