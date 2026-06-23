using System.Linq;

namespace EtlKit.DataFlow;

public class DynamicAggregationTypeInfo : IAggregationTypeInfo<ExpandoObject, ExpandoObject>
{
    public DynamicAggregationTypeInfo(Dictionary<string, InputAggregationField> mappings)
    {
        GroupColumns = mappings
            .Where(
                m =>
                    m.Value.AggregationMethod
                    == InputAggregationField.InputAggregationMethod.GroupBy
            )
            .Select(
                column =>
                    new AttributeMappingInfo
                    {
                        PropNameInOutput = column.Key,
                        PropNameInInput = column.Value.Name,
                    }
            )
            .ToList();
        AggregateColumns = mappings
            .Where(
                m =>
                    m.Value.AggregationMethod
                    != InputAggregationField.InputAggregationMethod.GroupBy
            )
            .Select(
                mapping =>
                    new AggregateAttributeMapping
                    {
                        PropNameInOutput = mapping.Key,
                        PropNameInInput = mapping.Value.Name,
                        AggregationMethod = Map(mapping.Value.AggregationMethod),
                    }
            )
            .ToList();
    }

    private static AggregationMethod Map(
        InputAggregationField.InputAggregationMethod aggregationMethod
    )
    {
        return aggregationMethod switch
        {
            InputAggregationField.InputAggregationMethod.Sum => AggregationMethod.Sum,
            InputAggregationField.InputAggregationMethod.Min => AggregationMethod.Min,
            InputAggregationField.InputAggregationMethod.Max => AggregationMethod.Max,
            InputAggregationField.InputAggregationMethod.Count => AggregationMethod.Count,
            _
                => throw new ArgumentOutOfRangeException(
                    $"aggregationMethod: {aggregationMethod} is not supported."
                ),
        };
    }

    public bool IsArrayOutput => false;

    public void SetOutputValueOrThrow(
        ExpandoObject outputRow,
        object value,
        AttributeMappingInfo attributeMapping,
        bool convertToUnderlyingType
    )
    {
        var row = outputRow as IDictionary<string, object>;
        if (row.TryGetValue(attributeMapping.PropNameInOutput, out var existingValue))
        {
            if (convertToUnderlyingType)
            {
                var conversionType = Common.DataFlow.TypeInfo.TryGetUnderlyingType(
                    existingValue.GetType()
                );
                var output = Convert.ChangeType(value, conversionType);
                row[attributeMapping.PropNameInOutput] = output;
            }
            else
            {
                row[attributeMapping.PropNameInOutput] = value;
            }
        }
        else
        {
            row.Add(attributeMapping.PropNameInOutput, value);
        }
    }

    public object GetInputValue(ExpandoObject inputRow, AttributeMappingInfo attributeMapping)
    {
        return (inputRow as IDictionary<string, object>)[attributeMapping.PropNameInInput];
    }

    [CanBeNull]
    public object GetOutputValueOrNull(
        ExpandoObject outputRow,
        AggregateAttributeMapping attributeMapping
    )
    {
        var row = outputRow as IDictionary<string, object>;
        return row.TryGetValue(attributeMapping.PropNameInOutput, out var value) ? value : null;
    }

    public IList<AggregateAttributeMapping> AggregateColumns { get; }
    public IList<AttributeMappingInfo> GroupColumns { get; }
}
