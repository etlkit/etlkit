namespace EtlKit.ConnectionManager
{
    /// <summary>
    /// SQLite rules: <c>INT</c> and <c>BIGINT</c> become <c>INTEGER</c>; every other type is returned
    /// verbatim.
    /// </summary>
    internal sealed class SqliteTypeConventions : IDbTypeConventions
    {
        public string GetDbSpecificType(ITableColumn column)
        {
            var typeName = column.DataType.Trim().ToUpper();
            return typeName is "INT" or "BIGINT" ? "INTEGER" : column.DataType;
        }
    }
}
