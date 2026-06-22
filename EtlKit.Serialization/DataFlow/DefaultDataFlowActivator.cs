using System;
using System.Dynamic;
using System.Globalization;
using CsvHelper.Configuration;
using JetBrains.Annotations;

namespace EtlKit.Serialization.DataFlow;

/// <summary>
/// Default activator that uses <see cref="Activator.CreateInstance(Type)"/> to create instances.
/// Handles special cases for known types without parameterless constructors.
/// </summary>
[PublicAPI]
public class DefaultDataFlowActivator : IDataFlowActivator
{
    /// <inheritdoc />
    public object? CreateInstance(Type type)
    {
        // Special cases for library classes without default constructors
        if (type == typeof(CsvConfiguration))
        {
            return new CsvConfiguration(CultureInfo.InvariantCulture);
        }

        var constructedType = type;
        if (type.IsGenericType && type.IsGenericTypeDefinition)
        {
            constructedType = type.MakeGenericType(typeof(ExpandoObject));
        }

        return Activator.CreateInstance(constructedType);
    }
}
