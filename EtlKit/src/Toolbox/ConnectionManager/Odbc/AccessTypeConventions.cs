using EtlKit.Common;

namespace EtlKit.ConnectionManager
{
    /// <summary>
    /// MS Access rules: <c>INT</c> becomes <c>INTEGER</c>; a <c>char(N)</c>-style type loses a
    /// leading <c>N</c> and comes back upper-cased, or becomes <c>LONGTEXT</c> above 255
    /// characters; every other type is returned verbatim.
    /// </summary>
    internal sealed class AccessTypeConventions : IDbTypeConventions
    {
        public string GetDbSpecificType(ITableColumn column)
        {
            var typeName = column.DataType.Trim().ToUpper();
            if (typeName == "INT")
                return "INTEGER";
            if (SqlTypeMapper.IsCharTypeDefinition(typeName))
            {
                if (typeName.StartsWith("N"))
                    typeName = typeName.Substring(1);
                return SqlTypeMapper.GetStringLengthFromCharString(typeName) > 255
                    ? "LONGTEXT"
                    : typeName;
            }
            return column.DataType;
        }
    }
}
