using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using ClickHouse.Ado;
using CsvHelper.Configuration;
using EtlKit.ClickHouse.ConnectionStrings;
using EtlKit.Common;
using EtlKit.ConnectionManager;
using EtlKit.ControlFlow;
using EtlKit.Primitives;
using JetBrains.Annotations;

namespace EtlKit.ClickHouse.ConnectionManager
{
    /// <summary>
    /// Connection manager for a ClickHouse database, based on the <c>ClickHouse.Ado</c> driver.
    /// </summary>
    /// <example>
    /// <code>
    /// ControlFlow.DefaultDbConnection =
    ///   new ClickHouseConnectionManager(new ClickHouseConnectionString("Host=localhost;Port=9000;Database=default;"));
    /// </code>
    /// </example>
    [PublicAPI]
    public class ClickHouseConnectionManager : DbConnectionManager<ClickHouseConnection>
    {
        /// <summary>
        /// Identifies this connection manager as <see cref="EtlKit.Primitives.ConnectionManagerType.ClickHouse"/>.
        /// </summary>
        public override ConnectionManagerType ConnectionManagerType { get; } =
            ConnectionManagerType.ClickHouse;

        /// <inheritdoc />
        /// <remarks>ClickHouse uses a backtick.</remarks>
        public override string QB { get; } = @"`";

        /// <inheritdoc />
        /// <remarks>ClickHouse uses a backtick.</remarks>
        public override string QE { get; } = @"`";

        /// <inheritdoc />
        /// <remarks>Always <see cref="CultureInfo.CurrentCulture"/>.</remarks>
        public override CultureInfo ConnectionCulture => CultureInfo.CurrentCulture;

        /// <summary>
        /// CSV configuration settings, initialized with <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public CsvConfiguration Configuration { get; set; }

        /// <summary>
        /// Creates a connection manager with no connection string set yet.
        /// </summary>
        public ClickHouseConnectionManager()
        {
            Configuration = new CsvConfiguration(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Creates a connection manager for the given <see cref="ClickHouseConnectionString"/>.
        /// </summary>
        /// <param name="connectionString">Connection string pointing at the ClickHouse server.</param>
        public ClickHouseConnectionManager(ClickHouseConnectionString connectionString)
            : base(connectionString)
        {
            Configuration = new CsvConfiguration(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Creates a connection manager from a raw ClickHouse connection string.
        /// </summary>
        /// <param name="connectionString">A ClickHouse connection string, e.g. <c>"Host=localhost;Port=9000;Database=default;"</c>.</param>
        public ClickHouseConnectionManager(string connectionString)
            : base(new ClickHouseConnectionString(connectionString))
        {
            Configuration = new CsvConfiguration(CultureInfo.InvariantCulture);
        }

        private TableDefinition? DestTableDef { get; set; }
        private Dictionary<string, TableColumn>? DestinationColumns { get; set; }

        /// <summary>
        /// Bulk-loads <paramref name="data"/> into <paramref name="tableName"/> using ClickHouse's
        /// <c>INSERT ... FORMAT CSV</c> statement. Rows are converted per destination column type and
        /// sent as a single CSV payload in one statement; the whole batch is buffered in memory
        /// before it is sent, so batch size bounds memory usage.
        /// </summary>
        /// <param name="data">Row-by-row source data to insert.</param>
        /// <param name="tableName">Destination table name.</param>
        public override void BulkInsert(ITableData data, string tableName)
        {
            if (DestinationColumns is null)
            {
                throw new EtlKitException("DestinationColumns is null");
            }
            if (DbConnection is null)
            {
                throw new EtlKitException("Database connection is not established!");
            }
            var csvData = new StringBuilder();
            var destColumnNames = data.GetColumnMapping()
                .Cast<IColumnMapping>()
                .Select(cm => cm.DataSetColumn)
                .ToList();

            while (data.Read())
            {
                var valSeparator = "";
                foreach (var destColumn in DestinationColumns.Keys)
                {
                    csvData.Append(valSeparator);
                    valSeparator = ",";
                    TableColumn colDef = DestinationColumns[destColumn];
                    object? val;
                    if (destColumnNames.Contains(colDef.Name))
                    {
                        var ordinal = data.GetOrdinal(destColumn);
                        val = data.GetValue(ordinal);
                    }
                    else
                    {
                        val = null;
                    }
                    csvData.Append(GetValue(val, colDef));
                }
                csvData.AppendLine();
            }

            if (DbConnection!.State != ConnectionState.Open)
            {
                DbConnection.Open();
            }
            using var cmd = DbConnection.CreateCommand();
            cmd.CommandText =
                $@"
INSERT INTO {QB}{tableName}{QE}
FORMAT CSV
{csvData}";

            cmd.ExecuteNonQuery();
        }

        private static string? GetValue(object? r, TableColumn col)
        {
            var dataType = col.DataType.ToUpper();
            return r switch
            {
                null => "",
                DateTime when dataType is "DATE" or "NULLABLE(DATE)" => $"{r:yyyy-MM-dd}",
                DateTime => $"{r:yyyy-MM-dd HH:mm:ss}",
                bool b => b ? "1" : "0",
                decimal or int or long or double or float => Convert.ToString(
                    r,
                    CultureInfo.InvariantCulture
                ),
                _ => ConvertToValueType(r, dataType),
            };
        }

        private static string? ConvertToValueType(object r, string dataType)
        {
            return !DataTypeConverter.IsCharTypeDefinition(dataType) && !dataType.Contains("STR")
                ? ConvertStringToNonStringType(r, dataType)
                : $@"""{r.ToString()!.Replace(@"""", @"""""")}""";
        }

        private static string? ConvertStringToNonStringType(object r, string dataType)
        {
            if (dataType.Contains("DECIMAL"))
            {
                return Convert.ToDecimal(r).ToString(CultureInfo.InvariantCulture);
            }
            if (dataType.Contains("INT"))
            {
                return Convert.ToInt64(r, CultureInfo.InvariantCulture).ToString();
            }
            if (dataType.Contains("DATETIME"))
            {
                return Convert.ToDateTime(r).ToString("yyyy-MM-dd HH:mm:ss");
            }
            if (dataType.Contains("DATE"))
            {
                return Convert.ToDateTime(r).ToString("yyyy-MM-dd");
            }
            if (dataType.Contains("BOOL") || dataType.Contains("BIT"))
            {
                return Convert.ToBoolean(r, CultureInfo.InvariantCulture).ToString();
            }
            return r.ToString();
        }

        /// <summary>
        /// Reads and caches the destination table's column definitions before <see cref="BulkInsert"/> runs.
        /// </summary>
        /// <param name="tableName">Destination table name.</param>
        public override void PrepareBulkInsert(string tableName)
        {
            ReadTableDefinition(tableName);
        }

        private void ReadTableDefinition(string tableName)
        {
            DestTableDef = TableDefinition.GetDefinitionFromTableName(this, tableName);
            DestinationColumns = new Dictionary<string, TableColumn>();
            foreach (var colDef in DestTableDef.Columns)
            {
                DestinationColumns.Add(colDef.Name, colDef);
            }
        }

        /// <summary>
        /// No cleanup is required for ClickHouse bulk inserts; this is a no-op override.
        /// </summary>
        /// <param name="tableName">Destination table name.</param>
        public override void CleanUpBulkInsert(string tableName)
        {
            // Nothing here
        }

        /// <summary>
        /// Ensures the destination column definitions are loaded before the first batch, in case
        /// <see cref="PrepareBulkInsert"/> was not called explicitly.
        /// </summary>
        /// <param name="tableName">Destination table name.</param>
        public override void BeforeBulkInsert(string tableName)
        {
            if (DestinationColumns == null)
                ReadTableDefinition(tableName);
        }

        /// <summary>
        /// No follow-up action is required after ClickHouse bulk inserts; this is a no-op override.
        /// </summary>
        /// <param name="tableName">Destination table name.</param>
        public override void AfterBulkInsert(string tableName)
        {
            // Nothing here
        }

        /// <summary>
        /// Creates a new <see cref="ClickHouseConnectionManager"/> with the same connection string and
        /// <see cref="EtlKit.ConnectionManager.DbConnectionManager{TConnection}.MaxLoginAttempts"/>.
        /// </summary>
        [MustDisposeResource]
        public override IConnectionManager Clone()
        {
            return new ClickHouseConnectionManager((ClickHouseConnectionString)ConnectionString)
            {
                MaxLoginAttempts = MaxLoginAttempts,
            };
        }

        /// <summary>
        /// Executes <paramref name="sql"/> and reports whether it returned a non-empty scalar result.
        /// </summary>
        /// <param name="callingTask">The task requesting the check, used for logging context.</param>
        /// <param name="sql">A scalar query that returns a value only if the index exists.</param>
        public override bool IndexExists(ITask callingTask, string sql)
        {
            var res = new SqlTask(callingTask, sql).ExecuteScalar();
            return (!string.IsNullOrEmpty(res?.ToString()));
        }
    }
}
