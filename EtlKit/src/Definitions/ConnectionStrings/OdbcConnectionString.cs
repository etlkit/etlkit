using System.Data.Odbc;

namespace EtlKit
{
    /// <summary>
    /// A helper class for encapsulating a connection string in an object.
    /// Internally the OdbcConnectionStringBuilder is used to access the values of the given connection string.
    /// </summary>
    public class OdbcConnectionString
        : DbConnectionString<OdbcConnectionString, OdbcConnectionStringBuilder>
    {
        public OdbcConnectionString() { }

        public OdbcConnectionString(string value)
            : base(value) { }

        public override string DbName
        {
            get =>
                throw new EtlKitNotSupportedException(
                    "Odbc connection string are not fully supported yet."
                );
            set =>
                throw new EtlKitNotSupportedException(
                    "Odbc connection string are not fully supported yet."
                );
        }

        public override string MasterDbName =>
            throw new EtlKitNotSupportedException(
                "Odbc connection string are not fully supported yet."
            );
        protected override string DbNameKeyword => "Database";

        public static implicit operator OdbcConnectionString(string value) => new(value);
    }
}
