using EtlKit.ConnectionManager;
using EtlKit.Primitives;

namespace EtlKit
{
    /// <summary>
    /// Concrete <see cref="IQueryParameter"/> implementation: a named, typed parameter value bound to
    /// a SQL command.
    /// </summary>
    [PublicAPI]
    public class QueryParameter : IQueryParameter
    {
        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public string Type { get; }

        /// <inheritdoc />
        public object Value { get; }

        /// <inheritdoc />
        public DbType DBType => DataTypeConverter.GetDBType(Type);

        /// <summary>
        /// Creates a parameter with the given name, type, and value. A <see langword="null"/> <paramref
        /// name="value"/> is stored as <see cref="DBNull.Value"/>.
        /// </summary>
        /// <param name="name">Parameter name, without the driver-specific prefix.</param>
        /// <param name="type">The .NET or SQL type name of the parameter.</param>
        /// <param name="value">The parameter value.</param>
        public QueryParameter(string name, string type, object value)
        {
            Name = name;
            Type = type;
            Value = value ?? DBNull.Value;
        }
    }
}
