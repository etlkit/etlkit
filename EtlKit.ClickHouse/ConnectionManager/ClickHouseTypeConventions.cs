using EtlKit.Common;
using EtlKit.ConnectionManager;

namespace EtlKit.ClickHouse.ConnectionManager
{
    /// <summary>
    /// ClickHouse rules: a <c>char(N)</c>-style type becomes <c>String</c>, and a nullable column's
    /// type is wrapped in <c>Nullable(...)</c> unless it already starts with <c>Nullable</c>; every
    /// other type is returned verbatim.
    /// </summary>
    internal sealed class ClickHouseTypeConventions : IDbTypeConventions
    {
        public string GetDbSpecificType(ITableColumn column)
        {
            var typeName = column.DataType.Trim().ToUpper();
            var type = column.DataType;
            if (SqlTypeMapper.IsCharTypeDefinition(typeName))
            {
                type = "String";
            }
            if (column.AllowNulls && !type.StartsWith("Nullable"))
            {
                return $"Nullable({type})";
            }
            return type;
        }
    }
}
