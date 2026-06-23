using TypeInfo = EtlKit.Common.DataFlow.TypeInfo;

namespace EtlKit.DataFlow
{
    internal sealed class DBMergeTypeInfo : TypeInfo
    {
        internal List<string> IdColumnNames { get; set; } = new();
        internal List<PropertyInfo> IdAttributeProps { get; } = new();
        internal List<PropertyInfo> CompareAttributeProps { get; } = new();
        internal List<Tuple<PropertyInfo, object>> DeleteAttributeProps { get; } = new();
        internal PropertyInfo ChangeDateProperty { get; private set; }
        internal PropertyInfo ChangeActionProperty { get; private set; }
        private MergeProperties MergeProps { get; }

        internal DBMergeTypeInfo(Type type, MergeProperties mergeProps)
            : base(type)
        {
            MergeProps = mergeProps;
            GatherTypeInfo();
        }

        protected override void RetrieveAdditionalTypeInfo(PropertyInfo propInfo, int currentIndex)
        {
            AddMergeIdColumnNameAttribute(propInfo);
            AddIdAddAttributeProps(propInfo);
            AddCompareAttributeProps(propInfo);
            AddDeleteAttributeProps(propInfo);
            AddChangeActionProp(propInfo);
            AddChangeDateProp(propInfo);
        }

        private void AddMergeIdColumnNameAttribute(PropertyInfo propInfo)
        {
            if (MergeProps.IdPropertyNames.Contains(propInfo.Name))
            {
                IdColumnNames.Add(propInfo.Name);
                return;
            }

            if (propInfo.GetCustomAttribute(typeof(IdColumnAttribute)) is not IdColumnAttribute)
            {
                return;
            }

            if (
                propInfo.GetCustomAttribute(typeof(ColumnMapAttribute)) is ColumnMapAttribute cmattr
            )
                IdColumnNames.Add(cmattr.ColumnName);
            else
                IdColumnNames.Add(propInfo.Name);
        }

        private void AddIdAddAttributeProps(PropertyInfo propInfo)
        {
            if (MergeProps.IdPropertyNames.Contains(propInfo.Name))
                IdAttributeProps.Add(propInfo);
            else
            {
                if (propInfo.GetCustomAttribute(typeof(IdColumnAttribute)) is IdColumnAttribute)
                    IdAttributeProps.Add(propInfo);
            }
        }

        private void AddCompareAttributeProps(PropertyInfo propInfo)
        {
            if (MergeProps.ComparePropertyNames.Contains(propInfo.Name))
                CompareAttributeProps.Add(propInfo);
            else
            {
                if (
                    propInfo.GetCustomAttribute(typeof(CompareColumnAttribute))
                    is CompareColumnAttribute
                )
                    CompareAttributeProps.Add(propInfo);
            }
        }

        private void AddDeleteAttributeProps(PropertyInfo propInfo)
        {
            if (MergeProps.DeletionProperties.TryGetValue(propInfo.Name, out var property))
                DeleteAttributeProps.Add(Tuple.Create(propInfo, property));
            else
            {
                if (
                    propInfo.GetCustomAttribute(typeof(DeleteColumnAttribute))
                    is DeleteColumnAttribute deleteAttr
                )
                    DeleteAttributeProps.Add(Tuple.Create(propInfo, deleteAttr.DeleteOnMatchValue));
            }
        }

        private void AddChangeActionProp(PropertyInfo propInfo)
        {
            if (
                string.Equals(
                    propInfo.Name,
                    MergeProps.ChangeActionPropertyName,
                    StringComparison.CurrentCultureIgnoreCase
                )
            )
                ChangeActionProperty = propInfo;
        }

        private void AddChangeDateProp(PropertyInfo propInfo)
        {
            if (
                string.Equals(
                    propInfo.Name,
                    MergeProps.ChangeDatePropertyName,
                    StringComparison.CurrentCultureIgnoreCase
                )
            )
                ChangeDateProperty = propInfo;
        }
    }
}
