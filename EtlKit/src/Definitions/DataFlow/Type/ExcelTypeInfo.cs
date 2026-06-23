using TypeInfo = EtlKit.Common.DataFlow.TypeInfo;

namespace EtlKit.DataFlow
{
    internal sealed class ExcelTypeInfo : TypeInfo
    {
        internal Dictionary<int, int> ExcelIndex2PropertyIndex { get; set; } = new();
        internal Dictionary<string, int> ExcelColumnName2PropertyIndex { get; set; } = new();

        internal ExcelTypeInfo(Type type)
            : base(type)
        {
            GatherTypeInfo();
        }

        protected override void RetrieveAdditionalTypeInfo(PropertyInfo propInfo, int currentIndex)
        {
            AddExcelColumnAttribute(propInfo, currentIndex);
        }

        private void AddExcelColumnAttribute(PropertyInfo propInfo, int curIndex)
        {
            if (
                propInfo.GetCustomAttribute(typeof(ExcelColumnAttribute))
                is ExcelColumnAttribute attr
            )
            {
                if (attr.Index != null)
                    ExcelIndex2PropertyIndex.Add(attr.Index ?? 0, curIndex);
                else if (!string.IsNullOrEmpty(attr.ColumnName))
                    ExcelColumnName2PropertyIndex.Add(attr.ColumnName, curIndex);
            }
            else
                ExcelColumnName2PropertyIndex.Add(propInfo.Name, curIndex);
        }

        internal static object CastPropertyValue(PropertyInfo property, string value)
        {
            if (property == null || string.IsNullOrEmpty(value))
                return null;
            if (property.PropertyType == typeof(bool))
                return value is "1" or "true" or "on" or "checked";
            Type t = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            return Convert.ChangeType(value, t);
        }
    }
}
