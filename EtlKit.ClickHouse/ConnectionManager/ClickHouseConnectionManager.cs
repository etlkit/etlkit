using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using EtlKit.Common;
using EtlKit.ConnectionManager;
using EtlKit.ControlFlow;
using ClickHouse.Ado;
using CsvHelper.Configuration;

using EtlKit.ClickHouse.ConnectionStrings;
using EtlKit.Primitives;
using JetBrains.Annotations;

namespace EtlKit.ClickHouse.ConnectionManager
{
    [PublicAPI]
    public class ClickHouseConnectionManager : DbConnectionManager<ClickHouseConnection>
    {
        public override ConnectionManagerType ConnectionManagerType { get; } =
            ConnectionManagerType.ClickHouse;
        public override string QB { get; } = @"`";
        public override string QE { get; } = @"`";
        public override CultureInfo ConnectionCulture => CultureInfo.CurrentCulture;
        public CsvConfiguration Configuration { get; set; }

        public ClickHouseConnectionManager()
        {
            Configuration = new CsvConfiguration(CultureInfo.InvariantCulture);
        }

        public ClickHouseConnectionManager(ClickHouseConnectionString connectionString)
            : base(connectionString)
        {
            Configuration = new CsvConfiguration(CultureInfo.InvariantCulture);
        }

        public ClickHouseConnectionManager(string connectionString)
            : base(new ClickHouseConnectionString(connectionString))
        {
            Configuration = new CsvConfiguration(CultureInfo.InvariantCulture);
        }

        private TableDefinition? DestTableDef { get; set; }
        private Dictionary<string, TableColumn>? DestinationColumns { get; set; }

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

        public override void CleanUpBulkInsert(string tableName)
        {
            // Nothing here
        }

        public override void BeforeBulkInsert(string tableName)
        {
            if (DestinationColumns == null)
                ReadTableDefinition(tableName);
        }

        public override void AfterBulkInsert(string tableName)
        {
            // Nothing here
        }

        [MustDisposeResource]
        public override IConnectionManager Clone()
        {
            return new ClickHouseConnectionManager((ClickHouseConnectionString)ConnectionString)
            {
                MaxLoginAttempts = MaxLoginAttempts,
            };
        }

        public override bool IndexExists(ITask callingTask, string sql)
        {
            var res = new SqlTask(callingTask, sql).ExecuteScalar();
            return (!string.IsNullOrEmpty(res?.ToString()));
        }
    }
}
