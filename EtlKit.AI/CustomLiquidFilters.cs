using System.Collections.Generic;
using DotLiquid;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace EtlKit.AI;

// Custom DotLiquid filters used to adjust data transformation in templates
public static class CustomLiquidFilters
{
    private static readonly object s_lock = new();
    private static bool s_registered;

    // Thread-safe one-time registration of filters
    [PublicAPI]
    public static void EnsureRegistered()
    {
        // ReSharper disable once InconsistentlySynchronizedField
        if (s_registered)
            return;
        lock (s_lock)
        {
            if (s_registered)
                return;
            // Register filters in DotLiquid globally
            Template.RegisterFilter(typeof(CustomLiquidFilters));
            s_registered = true;
        }
    }

    // Escape single quotes by doubling them
    [PublicAPI]
    public static string? EscapeSingleQuotes(string? input)
    {
        return input?.Replace("'", "''");
    }

    // Escape backslash by doubling them
    [PublicAPI]
    public static string? EscapeBackslash(string? input)
    {
        return input?.Replace("\\", "\\\\");
    }

    // Recursively escape single quotes inside nested objects/dictionaries
    [PublicAPI]
    public static object? EscapeSingleQuotesRecursive(object? input)
    {
        if (input is IDictionary<string, object?> dict)
        {
            var escapedDict = new Dictionary<string, object?>();
            foreach (var d in dict)
            {
                escapedDict[d.Key] = EscapeSingleQuotesRecursive(d.Value);
            }
            return escapedDict;
        }
        if (input is string str)
        {
            return EscapeSingleQuotes(str);
        }
        return input;
    }

    // Serialize the input object into JSON (compact form)
    [PublicAPI]
    public static string JsonArray(object input)
    {
        // Newtonsoft.Json works well with ExpandoObject
        var settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting =
                Formatting.None // compact JSON
            ,
        };

        // DotLiquid passes the object as-is; Newtonsoft will handle it
        return JsonConvert.SerializeObject(input, settings);
    }

    // Convert input object to a string (JSON for dictionaries, ToString() for scalars)
    [PublicAPI]
    public static string? AsString(object? input)
    {
        if (input is IDictionary<string, object> dict)
        {
            return JsonArray(dict);
        }

        return input?.ToString();
    }
}
