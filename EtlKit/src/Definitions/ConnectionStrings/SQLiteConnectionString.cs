using Microsoft.Data.Sqlite;

namespace EtlKit
{
    /// <summary>
    /// A helper class for encapsulating a connection string in an object.
    /// Internally the SQLiteConnectionStringBuilder is used to access the values of the given connection string.
    /// </summary>
    public class SQLiteConnectionString
        : DbConnectionString<SQLiteConnectionString, SqliteConnectionStringBuilder>
    {
        public SQLiteConnectionString() { }

        public SQLiteConnectionString(string value)
            : base(value) { }

        public override string DbName
        {
            get => Builder.DataSource;
            set => Builder.DataSource = value;
        }

        public override string MasterDbName =>
            throw new ETLBoxNotSupportedException("N/A for SQLite connection strings!");
        protected override string DbNameKeyword => "Data Source";

        public static implicit operator SQLiteConnectionString(string value) => new(value);
    }
}
