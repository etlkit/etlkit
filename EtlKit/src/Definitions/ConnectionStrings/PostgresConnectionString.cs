using Npgsql;

namespace EtlKit
{
    /// <summary>
    /// A helper class for encapsulating a connection string to a MySql server in an object.
    /// Internally the MySqlConnectionStringBuilder is used to access the values of the given connection string.
    /// </summary>
    public class PostgresConnectionString
        : DbConnectionString<PostgresConnectionString, NpgsqlConnectionStringBuilder>
    {
        public PostgresConnectionString() { }

        public PostgresConnectionString(string value)
            : base(value) { }

        public override string DbName
        {
            get => Builder.Database;
            set => Builder.Database = value;
        }
        public override string MasterDbName => "postgres";
        protected override string DbNameKeyword => "Database";

        public static implicit operator PostgresConnectionString(string value) => new(value);
    }
}
