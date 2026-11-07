using System;
using System.Data;
using EtlKit.Common;
using EtlKit.Primitives;
using JetBrains.Annotations;

namespace EtlKit
{
    /// <summary>
    /// Concrete <see cref="IQueryParameter"/> implementation: a named, typed parameter value bound to
    /// a SQL command.
    /// </summary>
    [PublicAPI]
    public class QueryParameter : IQueryParameter
    {
        private readonly DbType? _dbType;

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        /// <remarks>
        /// For a parameter created with an explicit <see cref="System.Data.DbType"/>, this is the name
        /// of that <see cref="System.Data.DbType"/> value.
        /// </remarks>
        public string Type { get; }

        /// <inheritdoc />
        public object Value { get; }

        /// <inheritdoc />
        /// <remarks>
        /// The <see cref="System.Data.DbType"/> given at construction if there was one, otherwise
        /// derived from <see cref="Type"/> by <see cref="SqlTypeMapper.GetDBType"/>.
        /// </remarks>
        public DbType DBType => _dbType ?? SqlTypeMapper.GetDBType(Type);

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

        /// <summary>
        /// Creates a parameter with an explicit <see cref="System.Data.DbType"/>, for callers that
        /// already know it and should not go through a SQL type name. A <see langword="null"/>
        /// <paramref name="value"/> is stored as <see cref="DBNull.Value"/>.
        /// </summary>
        /// <param name="name">Parameter name, without the driver-specific prefix.</param>
        /// <param name="dbType">The <see cref="System.Data.DbType"/> of the parameter.</param>
        /// <param name="value">The parameter value.</param>
        public QueryParameter(string name, DbType dbType, object value)
        {
            Name = name;
            Type = dbType.ToString();
            Value = value ?? DBNull.Value;
            _dbType = dbType;
        }
    }
}
