using System.Data.Common;
using System.Data.Odbc;
using System.Linq;
using System.Text;

using EtlKit.Primitives;

using EtlKit.Common;

namespace EtlKit.ConnectionManager
{
    /// <summary>
    /// This class creates the necessary sql statements that simulate the missing bulk insert function in Odbc connections.
    /// Normally this will be a insert into with multiple values.
    /// For access databases this will differ.
    /// </summary>
    /// <see cref="OdbcConnectionManager"/>
    /// <see cref="AccessOdbcConnectionManager"/>
    [PublicAPI]
    internal class BulkInsertSql<T>
        where T : DbParameter, new()
    {
        private bool IsAccessDatabase => ConnectionType == ConnectionManagerType.Access;
        internal bool UseParameterQuery { get; set; } = true;
        internal bool UseNamedParameters { get; set; }
        internal List<T> Parameters { get; set; }
        private StringBuilder QueryText { get; set; }
        private List<string> SourceColumnNames { get; set; }
        private List<string> DestColumnNames { get; set; }
        internal string AccessDummyTableName { get; set; }
        internal ConnectionManagerType ConnectionType { get; set; }
        public string QB { get; set; }
        public string QE { get; set; }
        public ObjectNameDescriptor TN => new(TableName, QB, QE);
        internal string TableName { get; set; }
        private int ParameterNameCount { get; set; }

        internal string CreateBulkInsertStatement(ITableData data, string tableName)
        {
            InitObjects();
            TableName = tableName;
            GetSourceAndDestColumnNames(data);
            AppendBeginSql();
            ReadDataAndCreateQuery(data);
            AppendEndSql();
            return QueryText.ToString();
        }

        private void InitObjects()
        {
            QueryText = new StringBuilder();
            Parameters = new List<T>();
        }

        private void GetSourceAndDestColumnNames(ITableData data)
        {
            SourceColumnNames = data.GetColumnMapping()
                .Cast<IColumnMapping>()
                .Select(cm => cm.SourceColumn)
                .ToList();
            DestColumnNames = data.GetColumnMapping()
                .Cast<IColumnMapping>()
                .Select(cm => cm.DataSetColumn)
                .ToList();
        }

        private void ReadDataAndCreateQuery(ITableData data)
        {
            while (data.Read())
            {
                var values = new List<string>();
                foreach (var destColumnName in DestColumnNames)
                {
                    var colIndex = data.GetOrdinal(destColumnName);
                    if (data.IsDBNull(colIndex))
                        AddNullValue(values, destColumnName);
                    else
                        AddNonNullValue(data, values, destColumnName, colIndex);
                }
                AppendValueListSql(values, data.NextResult());
            }
        }

        private void AddNullValue(List<string> values, string destColumnName)
        {
            if (UseParameterQuery)
            {
                values.Add(CreateParameterWithValue(DBNull.Value));
            }
            else
            {
                var value = IsAccessDatabase ? $"NULL AS {destColumnName}" : "NULL";
                values.Add(value);
            }
        }

        private void AddNonNullValue(
            ITableData data,
            List<string> values,
            string destColumnName,
            int colIndex
        )
        {
            if (UseParameterQuery)
            {
                values.Add(CreateParameterWithValue(data.GetValue(colIndex)));
            }
            else
            {
                var value = data.GetString(colIndex).Replace("'", "''");
                var valueSql = IsAccessDatabase
                    ? $"'{value}' AS {destColumnName}"
                    : $"'{value}'";
                values.Add(valueSql);
            }
        }

        private string CreateParameterWithValue(object parValue)
        {
            var par = new T { Value = parValue };
            Parameters.Add(par);
            if (!UseNamedParameters)
            {
                return "?";
            }

            var parName = $"@P{ParameterNameCount++}";
            par.ParameterName = parName;
            return parName;
        }

        private void AppendBeginSql()
        {
            QueryText.AppendLine(
                $@"INSERT INTO {TN.QuotedFullName} ({string.Join(",", SourceColumnNames.Select(col => QB + col + QE))})"
            );
            QueryText.AppendLine(IsAccessDatabase ? "  SELECT * FROM (" : "VALUES");
        }

        private void AppendValueListSql(IEnumerable<string> values, bool lastItem)
        {
            if (IsAccessDatabase)
            {
                QueryText.AppendLine(
                    "SELECT " + string.Join(",", values) + $"  FROM {AccessDummyTableName} "
                );
                if (lastItem)
                    QueryText.AppendLine(" UNION ALL ");
            }
            else
            {
                QueryText.Append("(" + string.Join(",", values) + ")");
                if (lastItem)
                    QueryText.AppendLine(",");
            }
        }

        private void AppendEndSql()
        {
            if (IsAccessDatabase)
                QueryText.AppendLine(") a;");
        }
    }

    internal sealed class BulkInsertSql : BulkInsertSql<OdbcParameter> { }
}
