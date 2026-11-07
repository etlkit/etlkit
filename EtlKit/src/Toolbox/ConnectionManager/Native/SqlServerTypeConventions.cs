namespace EtlKit.ConnectionManager
{
    /// <summary>
    /// SQL Server rules: <c>TEXT</c> (ignoring case and spaces) becomes <c>VARCHAR(MAX)</c>; every
    /// other type is returned verbatim.
    /// </summary>
    internal sealed class SqlServerTypeConventions : IDbTypeConventions
    {
        public string GetDbSpecificType(ITableColumn column)
        {
            var typeName = column.DataType.Trim().ToUpper();
            return typeName.Replace(" ", "") == "TEXT" ? "VARCHAR(MAX)" : column.DataType;
        }
    }
}
