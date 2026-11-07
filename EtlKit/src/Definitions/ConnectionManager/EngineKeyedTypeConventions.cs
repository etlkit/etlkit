using EtlKit.Common;
using EtlKit.Primitives;

namespace EtlKit.ConnectionManager
{
    /// <summary>
    /// The type conventions of a database engine, looked up by its <see cref="ConnectionManagerType"/>.
    /// A connection manager that does not supply its own
    /// <see cref="DbConnectionManager{TConnection}.TypeConventions"/> gets the conventions of the engine
    /// it reports, so a custom manager that reports <see cref="ConnectionManagerType.Postgres"/> is still
    /// treated as PostgreSQL, as it was when the rules were picked by engine. It also serves a custom
    /// <see cref="EtlKit.Primitives.IConnectionManager"/> that is not a
    /// <see cref="DbConnectionManager{TConnection}"/> and the obsolete
    /// <see cref="DataTypeConverter.TryGetDBSpecificType"/>.
    /// </summary>
    internal sealed class EngineKeyedTypeConventions : IDbTypeConventions
    {
        private readonly IDbTypeConventions _conventions;

        public EngineKeyedTypeConventions(ConnectionManagerType engine)
        {
            _conventions = engine switch
            {
                ConnectionManagerType.SqlServer => new SqlServerTypeConventions(),
                ConnectionManagerType.Access => new AccessTypeConventions(),
                ConnectionManagerType.SQLite => new SqliteTypeConventions(),
                ConnectionManagerType.Postgres => new PostgresTypeConventions(),
                // The ClickHouse rule lives in EtlKit.ClickHouse, which this assembly cannot reference.
                ConnectionManagerType.ClickHouse => new ClickHouseTypeConventionsCopy(),
                _ => new PassThroughTypeConventions(),
            };
        }

        public string GetDbSpecificType(ITableColumn column) =>
            _conventions.GetDbSpecificType(column);

        // Second copy of the rule in EtlKit.ClickHouse. A test keeps the two in step.
        private sealed class ClickHouseTypeConventionsCopy : IDbTypeConventions
        {
            public string GetDbSpecificType(ITableColumn column)
            {
                var typeName = column.DataType.Trim().ToUpper();
                var type = column.DataType;
                if (SqlTypeMapper.IsCharTypeDefinition(typeName))
                {
                    type = "String";
                }
                if (column.AllowNulls && !type.StartsWith("Nullable"))
                {
                    return $"Nullable({type})";
                }
                return type;
            }
        }
    }
}
