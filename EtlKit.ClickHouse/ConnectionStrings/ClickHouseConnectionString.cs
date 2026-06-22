namespace EtlKit.ClickHouse.ConnectionStrings
{
    public class ClickHouseConnectionString : DbConnectionString<ClickHouseConnectionString, ClickHouseConnectionStringBuilder>
    {
        public ClickHouseConnectionString() { }

        public ClickHouseConnectionString(string connectionString) : base(connectionString)
        {
        }

        public override string DbName
        {
            get => Builder.Database;
            set => Builder.Database = value;
        }
        public override string MasterDbName => "default";

        protected override string DbNameKeyword => "Database";

        public static implicit operator ClickHouseConnectionString(string value) => new(value);
    }
}
