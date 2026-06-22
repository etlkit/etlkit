using System;
using System.ComponentModel;
using System.Globalization;

namespace EtlKit.Serialization.DataFlow;

internal static class DataFlowTypesExtensions
{
    public static bool IsNullable(this Type type) =>
        Nullable.GetUnderlyingType(type) != null || !type.IsValueType;

    public static bool TryParse(
        this string value,
        Type type,
        CultureInfo cultureInfo,
        out object? objValue
    )
    {
        if (type == typeof(string))
        {
            objValue = value;
            return true;
        }

        return type switch
        {
            not null when IsOfType<char>(type)
                => TryParse<char>(value, out objValue, char.TryParse),
            not null when IsOfType<byte>(type)
                => TryParse<byte>(value, out objValue, byte.TryParse),
            not null when IsOfType<short>(type)
                => TryParse<short>(value, out objValue, short.TryParse),
            not null when IsOfType<ushort>(type)
                => TryParse<ushort>(value, out objValue, ushort.TryParse),
            not null when IsOfType<int>(type) => TryParse<int>(value, out objValue, int.TryParse),
            not null when IsOfType<uint>(type)
                => TryParse<uint>(value, out objValue, uint.TryParse),
            not null when IsOfType<long>(type)
                => TryParse<long>(value, out objValue, long.TryParse),
            not null when IsOfType<ulong>(type)
                => TryParse<ulong>(value, out objValue, ulong.TryParse),
            not null when IsOfType<bool>(type)
                => TryParse<bool>(value, out objValue, bool.TryParse),
            not null when IsOfType<double>(type) => TryParseDouble(value, out objValue),
            not null when IsOfType<DateTime>(type)
                => TryParseDateTime(value, cultureInfo, out objValue),
            not null when IsOfType<Guid>(type)
                => TryParse<Guid>(value, out objValue, Guid.TryParse),
            _ => FalseAndNull(out objValue)
        };
    }

    private delegate bool TryParseDelegate<T>(string value, out T result);

    private static bool IsOfType<T>(Type type) =>
        type == typeof(T) || Nullable.GetUnderlyingType(type) == typeof(T);

    private static bool TryParse<T>(
        string value,
        out object? objValue,
        TryParseDelegate<T> tryParse
    )
    {
        var result = tryParse(value, out T? parsedValue);
        objValue = result ? parsedValue : default;
        return result;
    }

    private static bool TryParseDateTime(
        string value,
        CultureInfo cultureInfo,
        out object? objValue
    )
    {
        var result = DateTime.TryParse(
            value,
            cultureInfo,
            DateTimeStyles.None,
            out var parsedValue
        );
        objValue = result ? parsedValue : default;
        return result;
    }

    private static bool TryParseDouble(string value, out object? objValue)
    {
        objValue = TypeDescriptor.GetConverter(typeof(double)).ConvertFromInvariantString(value);
        return objValue != null;
    }

    private static bool FalseAndNull(out object? objValue)
    {
        objValue = default;
        return false;
    }
}
