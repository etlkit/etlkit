using EtlKit.Primitives;

namespace EtlKit.Helper
{
    /// <summary>
    /// Extension methods for <see cref="IConnectionManager"/>.
    /// </summary>
    public static class ConnectionManagerExtensions
    {
        /// <summary>
        /// Formats <paramref name="source"/> using <paramref name="manager"/>'s identifier quoting.
        /// Interpolated arguments with the <c>:q</c> format specifier (e.g. <c>$"SELECT * FROM {tableName:q}"</c>)
        /// are wrapped in the connection's <see cref="IConnectionManager.QB"/>/<see cref="IConnectionManager.QE"/> characters.
        /// </summary>
        /// <param name="manager">The connection manager whose quoting convention to use.</param>
        /// <param name="source">The interpolated SQL string to format.</param>
        public static string FormatQuery(
            this IConnectionManager manager,
            FormattableString source
        ) => source.ToString(QueryFormatter.GetForConnection(manager));
    }
}
