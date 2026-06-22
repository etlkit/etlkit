using MySql.Data.MySqlClient;

namespace EtlKit
{
    /// <summary>
    /// A helper class for encapsulating a connection string to a MySql server in an object.
    /// Internally the MySqlConnectionStringBuilder is used to access the values of the given connection string.
    /// </summary>
    public class MySqlConnectionString
        : DbConnectionString<MySqlConnectionString, MySqlConnectionStringBuilder>
    {
        public MySqlConnectionString() { }

        public MySqlConnectionString(string value)
            : base(value) { }

        public override string DbName
        {
            get => Builder.Database;
            set => Builder.Database = value;
        }
        public override string MasterDbName => "mysql";
        protected override string DbNameKeyword => "Database";

        public static implicit operator MySqlConnectionString(string value) => new(value);
    }
}
