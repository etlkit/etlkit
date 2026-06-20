using System.Linq;

namespace EtlKit.Helper
{
    [PublicAPI]
    public static class TableColumnExtensions
    {
        public static string AsString(
            this ITableColumn column,
            string tableName = "",
            string prefix = "",
            string suffix = ""
        ) => (tableName != "" ? tableName + "." : "") + prefix + column.Name + suffix;

        public static string AsString(
            this IEnumerable<ITableColumn> columns,
            string tableName = "",
            string prefix = "",
            string suffix = ""
        ) => string.Join(", ", columns.Select(col => col.AsString(tableName, prefix, suffix)));

        public static string AsStringWithNewLine(
            this IEnumerable<ITableColumn> columns,
            string tableName = "",
            string prefix = "",
            string suffix = ""
        ) =>
            string.Join(
                Environment.NewLine + ",",
                columns.Select(col => col.AsString(tableName, prefix, suffix))
            );
    }
}
