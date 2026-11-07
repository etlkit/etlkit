using EtlKit.Primitives;

namespace EtlKit.ConnectionManager
{
    /// <summary>
    /// Implemented by connection managers that supply engine-specific <see cref="IDbTypeConventions"/>,
    /// so that code holding only an <see cref="IConnectionManager"/> can reach them.
    /// </summary>
    internal interface IDbTypeConventionsProvider
    {
        IDbTypeConventions TypeConventions { get; }
    }
}
