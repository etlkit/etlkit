using EtlKit.Primitives;

namespace EtlKit.Helper
{
    public static class ConnectionManagerExtensions
    {
        public static string FormatQuery(
            this IConnectionManager manager,
            FormattableString source
        ) => source.ToString(QueryFormatter.GetForConnection(manager));
    }
}
