using System.Linq;

using EtlKit.Primitives;

using EtlKit.ConnectionManager.Helpers;

using Microsoft.Data.Sqlite;

namespace EtlKit.ConnectionManager
{
    /// <summary>
    /// Connection manager for an SQLite connection based on ADO.NET.
    /// </summary>
    /// <example>
    /// <code>
    /// ControlFlow.DefaultDbConnection =
    ///   new.SQLiteConnectionManager(new SQLiteConnectionString(
    ///     "Data Source=.\db\SQLite.db;Version=3;"));
    /// </code>
    /// </example>
    [PublicAPI]
    public class SQLiteConnectionManager : DbConnectionManager<SqliteConnection>
    {
        public override ConnectionManagerType ConnectionManagerType { get; } =
            ConnectionManagerType.SQLite;
        public override string QB { get; } = @"""";
        public override string QE { get; } = @"""";
        public override CultureInfo ConnectionCulture => CultureInfo.InvariantCulture;
        public override bool SupportDatabases { get; }
        public override bool SupportProcedures { get; }
        public override bool SupportSchemas { get; }
        public override bool SupportComputedColumns { get; }

        public bool ModifyDBSettings { get; set; }

        public SQLiteConnectionManager() { }

        public SQLiteConnectionManager(SQLiteConnectionString connectionString)
            : base(connectionString) { }

        public SQLiteConnectionManager(string connectionString)
            : base(new SQLiteConnectionString(connectionString)) { }

        private string Synchronous { get; set; }
        private string JournalMode { get; set; }

        public override void BulkInsert(ITableData data, string tableName)
        {
            var sourceColumnNames = data.GetColumnMapping()
                .Cast<IColumnMapping>()
                .Select(cm => cm.SourceColumn)
                .ToList();
            var paramNames = data.GetColumnMapping()
                .Cast<IColumnMapping>()
                .Select((_, i) => $"$p{i}")
                .ToArray();
            var destColumnNames = data.GetColumnMapping()
                .Cast<IColumnMapping>()
                .Select(cm => cm.DataSetColumn)
                .ToList();

            var existingTransaction = Transaction as SqliteTransaction;
            SqliteTransaction bulkTransaction = null;
            if (existingTransaction == null)
                bulkTransaction = DbConnection.BeginTransaction();
            using (bulkTransaction)
            using (var command = DbConnection.CreateCommand())
            {
                command.Transaction = existingTransaction ?? bulkTransaction;
                command.CommandText =
                    $@"INSERT INTO {tableName} 
({string.Join(",", sourceColumnNames)})
VALUES ({string.Join(",", paramNames)})";
                command.Prepare();
                while (data.Read())
                {
                    command.Parameters.AddRange(
                        destColumnNames.Select(
                            (n, i) =>
                                ConstructSqliteParameter(
                                    paramNames[i],
                                    data.GetValue(data.GetOrdinal(n))
                                )
                        )
                    );
                    command.ExecuteNonQuery();
                    command.Parameters.Clear();
                }

                bulkTransaction?.Commit();
            }
        }

        private static SqliteParameter ConstructSqliteParameter(string parameterName, object value)
        {
            return new SqliteParameter
            {
                ParameterName = parameterName,
                Value = value ?? DBNull.Value,
                IsNullable = value is null,
                DbType = SqliteConvert.TypeToDbType(value),
                SqliteType = SqliteConvert.TypeToAffinity(value),
            };
        }

        public override void PrepareBulkInsert(string tableName)
        {
            if (!ModifyDBSettings)
            {
                return;
            }

            try
            {
                Synchronous = ExecuteScalar("PRAGMA synchronous").ToString();
                JournalMode = ExecuteScalar("PRAGMA journal_mode").ToString();
                ExecuteNonQuery("PRAGMA synchronous = OFF");
                ExecuteNonQuery("PRAGMA journal_mode = MEMORY");
            }
            catch
            {
                ModifyDBSettings = false;
            }
        }

        public override void CleanUpBulkInsert(string tableName)
        {
            if (!ModifyDBSettings)
            {
                return;
            }

            try
            {
                ExecuteNonQuery($"PRAGMA synchronous = {Synchronous}");
                ExecuteNonQuery($"PRAGMA journal_mode = {JournalMode}");
            }
            catch
            {
                // ignored
            }
        }

        public override void BeforeBulkInsert(string tableName) { }

        public override void AfterBulkInsert(string tableName) { }

        public override IConnectionManager Clone()
        {
            var clone = new SQLiteConnectionManager((SQLiteConnectionString)ConnectionString)
            {
                MaxLoginAttempts = MaxLoginAttempts,
                ModifyDBSettings = ModifyDBSettings,
            };
            return clone;
        }
    }
}
