using TypeInfo = EtlKit.Common.DataFlow.TypeInfo;

namespace EtlKit.DataFlow
{
    internal sealed class DBTypeInfo : TypeInfo
    {
        internal List<string> PropertyNames { get; } = new();
        internal Dictionary<PropertyInfo, Type> UnderlyingPropType { get; } = new();
        private Dictionary<string, string> ColumnMap2Property { get; } = new();

        internal DBTypeInfo(Type type)
            : base(type)
        {
            GatherTypeInfo();
        }

        protected override void RetrieveAdditionalTypeInfo(PropertyInfo propInfo, int currentIndex)
        {
            PropertyNames.Add(propInfo.Name);
            AddColumnMappingAttribute(propInfo);
            AddUnderlyingType(propInfo);
        }

        private void AddColumnMappingAttribute(PropertyInfo propInfo)
        {
            if (propInfo.GetCustomAttribute(typeof(ColumnMapAttribute)) is ColumnMapAttribute attr)
                ColumnMap2Property.Add(attr.ColumnName, propInfo.Name);
        }

        private void AddUnderlyingType(PropertyInfo propInfo)
        {
            Type t = TryGetUnderlyingType(propInfo.PropertyType);
            UnderlyingPropType.Add(propInfo, t);
        }

        internal bool HasPropertyOrColumnMapping(string name)
        {
            return ColumnMap2Property.ContainsKey(name) || PropertyNames.Contains(name);
        }

        internal PropertyInfo GetInfoByPropertyNameOrColumnMapping(string propNameOrColMapName)
        {
            return ColumnMap2Property.TryGetValue(propNameOrColMapName, out var value)
                ? Properties[PropertyIndex[value]]
                : Properties[PropertyIndex[propNameOrColMapName]];
        }

        internal int GetIndexByPropertyNameOrColumnMapping(string propNameOrColMapName)
        {
            return ColumnMap2Property.TryGetValue(propNameOrColMapName, out var value)
                ? PropertyIndex[value]
                : PropertyIndex[propNameOrColMapName];
        }
    }
}
