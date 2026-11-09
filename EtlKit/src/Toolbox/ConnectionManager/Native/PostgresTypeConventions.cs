using EtlKit.Common;

namespace EtlKit.ConnectionManager
{
    /// <summary>
    /// PostgreSQL rules: a <c>char(N)</c>-style type loses a leading <c>N</c> and comes back
    /// upper-cased (<c>NVARCHAR(50)</c> becomes <c>VARCHAR(50)</c>), <c>DATETIME</c> becomes
    /// <c>TIMESTAMP</c>; every other type is returned verbatim.
    /// </summary>
    internal sealed class PostgresTypeConventions : IDbTypeConventions
    {
        public string GetDbSpecificType(ITableColumn column)
        {
            var typeName = column.DataType.Trim().ToUpper();
            if (SqlTypeMapper.IsCharTypeDefinition(typeName))
            {
                if (typeName.StartsWith("N"))
                    return typeName.Substring(1);
            }
            else if (typeName == "DATETIME")
                return "TIMESTAMP";
            return column.DataType;
        }
    }
}
