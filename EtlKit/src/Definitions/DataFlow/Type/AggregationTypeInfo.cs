using System.Linq;

namespace EtlKit.DataFlow
{
    internal sealed class AggregationTypeInfo<TInput, TOutput>
        : MappingTypeInfo<TInput, TOutput>,
            IAggregationTypeInfo<TInput, TOutput>
    {
        public IList<AggregateAttributeMapping> AggregateColumns { get; } =
            new List<AggregateAttributeMapping>();
        public IList<AttributeMappingInfo> GroupColumns { get; } = new List<AttributeMappingInfo>();

        internal AggregationTypeInfo()
        {
            InitMappings(typeof(TInput), typeof(TOutput));
        }

        protected override void AddAttributeInfoMapping(PropertyInfo propInfo)
        {
            AddAggregateColumn(propInfo);
            AddGroupColumn(propInfo);
        }

        private void AddGroupColumn(PropertyInfo propInfo)
        {
            if (
                propInfo.GetCustomAttribute(typeof(GroupColumnAttribute))
                is GroupColumnAttribute attr
            )
            {
                GroupColumns.Add(
                    new AttributeMappingInfo
                    {
                        PropInOutput = propInfo,
                        PropNameInInput = attr.AggregationGroupingProperty
                    }
                );
            }
        }

        private void AddAggregateColumn(PropertyInfo propInfo)
        {
            if (
                propInfo.GetCustomAttribute(typeof(AggregateColumnAttribute))
                is AggregateColumnAttribute attr
            )
            {
                AggregateColumns.Add(
                    new AggregateAttributeMapping
                    {
                        PropInOutput = propInfo,
                        PropNameInInput = attr.AggregationProperty,
                        AggregationMethod = attr.AggregationMethod
                    }
                );
            }
        }

        protected override void CombineInputAndOutputMapping()
        {
            AssignInputProperty(GroupColumns);
            AssignInputProperty(AggregateColumns.Cast<AttributeMappingInfo>().ToList());
        }

        public object GetOutputValueOrNull(
            TOutput outputRow,
            AggregateAttributeMapping attributeMapping
        )
        {
            var aggVal = attributeMapping.PropInOutput.GetValue(outputRow);
            return aggVal;
        }
    }
}
