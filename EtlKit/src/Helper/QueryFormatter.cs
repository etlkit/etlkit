using System.Collections.Concurrent;
using EtlKit.Primitives;

namespace EtlKit.Helper
{
    internal sealed class QueryFormatter : IFormatProvider, ICustomFormatter
    {
        private static readonly ConcurrentDictionary<
            ConnectionManagerType,
            QueryFormatter
        > s_cache = new();
        private readonly string _qb;
        private readonly string _qe;

        private QueryFormatter(IConnectionManager connectionManager)
        {
            _qb = connectionManager.QB;
            _qe = connectionManager.QE;
        }

        // We assume here that all connection managers of the same type format query the same way
        public static QueryFormatter GetForConnection(IConnectionManager connectionManager) =>
            s_cache.GetOrAdd(
                connectionManager.ConnectionManagerType,
                _ => new QueryFormatter(connectionManager)
            );

        // IFormatProvider.GetFormat implementation.
        public object GetFormat(Type formatType)
        {
            // Determine whether custom formatting object is requested.
            return formatType == typeof(ICustomFormatter) ? this : null;
        }

        /// <summary>
        /// Format query string
        /// </summary>
        /// <param name="format">Available modifiers are :q (quoted)</param>
        /// <param name="arg"></param>
        /// <param name="formatProvider"></param>
        /// <returns></returns>
        /// <exception cref="FormatException"></exception>
        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (format is "q" or "Q" && arg is string identifier)
            {
                return $"{_qb}{identifier}{_qe}";
            }
            else
            {
                try
                {
                    return HandleOtherFormats(format, arg);
                }
                catch (FormatException e)
                {
                    throw new FormatException($"The format of '{format}' is invalid.", e);
                }
            }
        }

        private static string HandleOtherFormats(string format, object arg) =>
            arg switch
            {
                IFormattable formattable
                    => formattable.ToString(format, CultureInfo.CurrentCulture),
                null => string.Empty,
                _ => arg.ToString()
            };
    }
}
