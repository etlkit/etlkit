using EtlKit.Helper;
using Microsoft.Data.SqlClient;

namespace EtlKit
{
    /// <summary>
    /// A helper class for encapsulating a connection string to a sql server in an object.
    /// Internally the SqlConnectionStringBuilder is used to access the values of the given connection string.
    /// </summary>
    public class SqlConnectionString
        : DbConnectionString<SqlConnectionString, SqlConnectionStringBuilder>
    {
        public SqlConnectionString() { }

        public SqlConnectionString(string value)
            : base(value) { }

        protected sealed override string GetConnectionString() =>
            Builder.ConnectionString.ReplaceIgnoreCase(
                "Integrated Security=true",
                "Integrated Security=SSPI"
            );

        public override string DbName
        {
            get => Builder.InitialCatalog;
            set => Builder.InitialCatalog = value;
        }
        public override string MasterDbName => "master";
        protected override string DbNameKeyword => "Database";

        public static implicit operator SqlConnectionString(string value) => new(value);
    }
}
