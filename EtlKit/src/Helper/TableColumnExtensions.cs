using System.Linq;

namespace EtlKit.Helper
{
    /// <summary>
    /// Extension methods for rendering <see cref="ITableColumn"/> names into SQL fragments.
    /// </summary>
    [PublicAPI]
    public static class TableColumnExtensions
    {
        /// <summary>
        /// Renders <paramref name="column"/>'s name, optionally table-qualified and/or wrapped in a
        /// prefix/suffix (e.g. for quoting).
        /// </summary>
        /// <param name="column">The column to render.</param>
        /// <param name="tableName">Table name to qualify the column with, or empty for none.</param>
        /// <param name="prefix">Text inserted before the column name (e.g. an opening quote).</param>
        /// <param name="suffix">Text inserted after the column name (e.g. a closing quote).</param>
        public static string AsString(
            this ITableColumn column,
            string tableName = "",
            string prefix = "",
            string suffix = ""
        ) => (tableName != "" ? tableName + "." : "") + prefix + column.Name + suffix;

        /// <summary>
        /// Renders every column's name via <see cref="AsString(ITableColumn,string,string,string)"/>,
        /// joined with <c>", "</c>.
        /// </summary>
        /// <param name="columns">The columns to render.</param>
        /// <param name="tableName">Table name to qualify each column with, or empty for none.</param>
        /// <param name="prefix">Text inserted before each column name (e.g. an opening quote).</param>
        /// <param name="suffix">Text inserted after each column name (e.g. a closing quote).</param>
        public static string AsString(
            this IEnumerable<ITableColumn> columns,
            string tableName = "",
            string prefix = "",
            string suffix = ""
        ) => string.Join(", ", columns.Select(col => col.AsString(tableName, prefix, suffix)));

        /// <summary>
        /// Renders every column's name via <see cref="AsString(ITableColumn,string,string,string)"/>,
        /// joined with a newline followed by a comma — useful for one-column-per-line SQL generation.
        /// </summary>
        /// <param name="columns">The columns to render.</param>
        /// <param name="tableName">Table name to qualify each column with, or empty for none.</param>
        /// <param name="prefix">Text inserted before each column name (e.g. an opening quote).</param>
        /// <param name="suffix">Text inserted after each column name (e.g. a closing quote).</param>
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
