namespace EtlKit.DataFlow
{
    internal sealed class LookupTypeInfo : MappingTypeInfo
    {
        internal List<AttributeMappingInfo> MatchColumns { get; set; } = new();
        internal List<AttributeMappingInfo> RetrieveColumns { get; set; } = new();

        internal LookupTypeInfo(Type inputType, Type sourceType)
            : base(inputType, sourceType)
        {
            InitMappings(inputType, sourceType);
        }

        protected override void AddAttributeInfoMapping(PropertyInfo propInfo)
        {
            AddRetrieveColumn(propInfo);
            AddMatchColumn(propInfo);
        }

        private void AddMatchColumn(PropertyInfo propInfo)
        {
            if (
                propInfo.GetCustomAttribute(typeof(MatchColumnAttribute))
                is MatchColumnAttribute attr
            )
                MatchColumns.Add(
                    new AttributeMappingInfo
                    {
                        PropInOutput = propInfo,
                        PropNameInInput = attr.LookupSourcePropertyName
                    }
                );
        }

        private void AddRetrieveColumn(PropertyInfo propInfo)
        {
            if (
                propInfo.GetCustomAttribute(typeof(RetrieveColumnAttribute))
                is RetrieveColumnAttribute attr
            )
                RetrieveColumns.Add(
                    new AttributeMappingInfo
                    {
                        PropInOutput = propInfo,
                        PropNameInInput = attr.LookupSourcePropertyName
                    }
                );
        }

        protected override void CombineInputAndOutputMapping()
        {
            AssignInputProperty(MatchColumns);
            AssignInputProperty(RetrieveColumns);
        }
    }
}
