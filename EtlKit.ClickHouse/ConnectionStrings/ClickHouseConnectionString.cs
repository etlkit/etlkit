namespace EtlKit.ClickHouse.ConnectionStrings
{
    /// <summary>
    /// Encapsulates a connection string to a ClickHouse server. Values are accessed through the
    /// underlying <see cref="ClickHouseConnectionStringBuilder"/>.
    /// </summary>
    public class ClickHouseConnectionString
        : DbConnectionString<ClickHouseConnectionString, ClickHouseConnectionStringBuilder>
    {
        /// <summary>
        /// Creates an empty connection string; set <see cref="EtlKit.DbConnectionString{T,TBuilder}.Value"/>
        /// or the individual <see cref="EtlKit.DbConnectionString{T,TBuilder}.Builder"/> properties before use.
        /// </summary>
        public ClickHouseConnectionString() { }

        /// <summary>
        /// Creates a connection string from an existing ClickHouse connection string value.
        /// </summary>
        /// <param name="connectionString">A ClickHouse connection string, e.g. <c>"Host=localhost;Port=9000;Database=default;"</c>.</param>
        public ClickHouseConnectionString(string connectionString)
            : base(connectionString) { }

        /// <inheritdoc />
        /// <remarks>Backed by <see cref="ClickHouseConnectionStringBuilder.Database"/>.</remarks>
        public override string DbName
        {
            get => Builder.Database;
            set => Builder.Database = value;
        }

        /// <inheritdoc />
        /// <remarks>ClickHouse's built-in default database is <c>"default"</c>.</remarks>
        public override string MasterDbName => "default";

        /// <inheritdoc />
        /// <remarks>ClickHouse's connection string key is <c>"Database"</c>.</remarks>
        protected override string DbNameKeyword => "Database";

        /// <summary>
        /// Implicitly wraps a plain connection string value in a <see cref="ClickHouseConnectionString"/>.
        /// </summary>
        /// <param name="value">A ClickHouse connection string.</param>
        public static implicit operator ClickHouseConnectionString(string value) => new(value);
    }
}
