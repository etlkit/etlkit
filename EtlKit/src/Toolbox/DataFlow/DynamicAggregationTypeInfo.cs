using System.Linq;

namespace EtlKit.DataFlow;

/// <summary>
/// <see cref="IAggregationTypeInfo{TInput,TOutput}"/> for the non-generic, dynamic-object form of
/// <see cref="Aggregation{TInput,TOutput}"/>: builds group/aggregate column mappings from a
/// dictionary of field configurations instead of attribute reflection.
/// </summary>
public class DynamicAggregationTypeInfo : IAggregationTypeInfo<ExpandoObject, ExpandoObject>
{
    /// <summary>
    /// Splits <paramref name="mappings"/> into group columns (<see
    /// cref="InputAggregationField.InputAggregationMethod.GroupBy"/>) and aggregate columns (every
    /// other method), populating <see cref="GroupColumns"/> and <see cref="AggregateColumns"/>.
    /// </summary>
    /// <param name="mappings">Output field name to input field/aggregation method configuration.</param>
    public DynamicAggregationTypeInfo(Dictionary<string, InputAggregationField> mappings)
    {
        GroupColumns = mappings
            .Where(m =>
                m.Value.AggregationMethod == InputAggregationField.InputAggregationMethod.GroupBy
            )
            .Select(column => new AttributeMappingInfo
            {
                PropNameInOutput = column.Key,
                PropNameInInput = column.Value.Name,
            })
            .ToList();
        AggregateColumns = mappings
            .Where(m =>
                m.Value.AggregationMethod != InputAggregationField.InputAggregationMethod.GroupBy
            )
            .Select(mapping => new AggregateAttributeMapping
            {
                PropNameInOutput = mapping.Key,
                PropNameInInput = mapping.Value.Name,
                AggregationMethod = Map(mapping.Value.AggregationMethod),
            })
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
            _ => throw new ArgumentOutOfRangeException(
                $"aggregationMethod: {aggregationMethod} is not supported."
            ),
        };
    }

    /// <inheritdoc />
    /// <remarks>Always <see langword="false"/>; dynamic aggregation output is never an array.</remarks>
    public bool IsArrayOutput => false;

    /// <inheritdoc />
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

    /// <inheritdoc />
    public object GetInputValue(ExpandoObject inputRow, AttributeMappingInfo attributeMapping)
    {
        return (inputRow as IDictionary<string, object>)[attributeMapping.PropNameInInput];
    }

    /// <inheritdoc />
    [CanBeNull]
    public object GetOutputValueOrNull(
        ExpandoObject outputRow,
        AggregateAttributeMapping attributeMapping
    )
    {
        var row = outputRow as IDictionary<string, object>;
        return row.TryGetValue(attributeMapping.PropNameInOutput, out var value) ? value : null;
    }

    /// <inheritdoc />
    public IList<AggregateAttributeMapping> AggregateColumns { get; }

    /// <inheritdoc />
    public IList<AttributeMappingInfo> GroupColumns { get; }
}
