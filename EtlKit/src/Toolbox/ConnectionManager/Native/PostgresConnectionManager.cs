using System.Linq;

using EtlKit.Primitives;

using EtlKit.Common;

using Npgsql;

namespace EtlKit.ConnectionManager
{
    /// <summary>
    /// Connection manager of a classic ADO.NET connection to a (Microsoft) Sql Server.
    /// </summary>
    /// <example>
    /// <code>
    /// ControlFlow.DefaultDbConnection = new SqlConnectionManager(new ConnectionString("Data Source=.;"));
    /// </code>
    /// </example>
    [PublicAPI]
    public class PostgresConnectionManager : DbConnectionManager<NpgsqlConnection>
    {
        public override ConnectionManagerType ConnectionManagerType { get; } =
            ConnectionManagerType.Postgres;
        public override string QB { get; } = @"""";
        public override string QE { get; } = @"""";
        public override CultureInfo ConnectionCulture => CultureInfo.CurrentCulture;

        public PostgresConnectionManager() { }

        public PostgresConnectionManager(PostgresConnectionString connectionString)
            : base(connectionString) { }

        public PostgresConnectionManager(string connectionString)
            : base(new PostgresConnectionString(connectionString)) { }

        private TableDefinition DestTableDef { get; set; }

        private Dictionary<string, TableColumn> DestinationColumns { get; set; }

        public override void BulkInsert(ITableData data, string tableName)
        {
            if (DbConnection is null)
            {
                throw new EtlKitException("Database connection is not established!");
            }

            var tn = new ObjectNameDescriptor(tableName, QB, QE);
            var destColumnNames = data.GetColumnMapping()
                .Cast<IColumnMapping>()
                .Select(cm => cm.DataSetColumn)
                .ToList();
            var quotedDestColumns = destColumnNames.Select(col => tn.QB + col + tn.QE);

            var copyCommandSql = $"""
                COPY {tn.QuotedFullName} ({string.Join(", ", quotedDestColumns)})
                FROM STDIN (FORMAT BINARY)
                """;

            using var writer = DbConnection.BeginBinaryImport(copyCommandSql);
            while (data.Read())
            {
                writer.StartRow();
                foreach (var destCol in destColumnNames)
                {
                    TableColumn colDef = DestinationColumns[destCol];
                    var ordinal = data.GetOrdinal(destCol);
                    var val = data.GetValue(ordinal);
                    if (val != null)
                    {
                        object convertedVal;
                        if (colDef.NETDataType == typeof(Guid) && (val is string s))
                        {
                            convertedVal = Guid.Parse(s);
                        }
                        else
                        {
                            convertedVal = Convert.ChangeType(val, colDef.NETDataType);
                        }
                        writer.Write(convertedVal, colDef.InternalDataType.ToLower());
                    }
                    else
                    {
                        writer.WriteNull();
                    }
                }
            }
            writer.Complete();
        }

        public override void PrepareBulkInsert(string tableName)
        {
            ReadTableDefinition(tableName);
        }

        private void ReadTableDefinition(string tableName)
        {
            DestTableDef = TableDefinition.GetDefinitionFromTableName(this, tableName);
            DestinationColumns = [];
            foreach (var colDef in DestTableDef.Columns)
            {
                DestinationColumns.Add(colDef.Name, colDef);
            }
        }

        public override void CleanUpBulkInsert(string tableName) { }

        public override void BeforeBulkInsert(string tableName)
        {
            if (DestinationColumns == null)
                ReadTableDefinition(tableName);
        }

        public override void AfterBulkInsert(string tableName) { }

        [MustDisposeResource]
        public override IConnectionManager Clone()
        {
            return new PostgresConnectionManager((PostgresConnectionString)ConnectionString)
            {
                MaxLoginAttempts = MaxLoginAttempts,
            };
        }

        protected override void MapQueryParameterToCommandParameter(
            IQueryParameter source,
            IDbDataParameter destination
        )
        {
            destination.ParameterName = source.Name;
            // (fix for https://www.npgsql.org/doc/release-notes/6.0.html#major-changes-to-timestamp-mapping in NpgSql 6.0+)
            destination.DbType =
                source.DBType == DbType.DateTime ? DbType.DateTime2 : source.DBType;
            destination.Value = source.Value;
        }
    }
}
