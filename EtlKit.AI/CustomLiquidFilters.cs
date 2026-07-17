using System.Collections.Generic;
using DotLiquid;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace EtlKit.AI;

/// <summary>
/// Custom <a href="https://github.com/dotliquid/dotliquid">DotLiquid</a> filters, registered globally
/// via <see cref="EnsureRegistered"/>, for escaping and serializing values inside AI prompt templates.
/// </summary>
public static class CustomLiquidFilters
{
    private static readonly object s_lock = new();
    private static bool s_registered;

    /// <summary>
    /// Registers this class's filters with DotLiquid, if not already done. Safe to call more than
    /// once or concurrently.
    /// </summary>
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

    /// <summary>
    /// Escapes single quotes by doubling them (<c>'</c> becomes <c>''</c>), for embedding a value in a
    /// single-quoted SQL string literal.
    /// </summary>
    /// <param name="input">The string to escape, or <see langword="null"/>.</param>
    [PublicAPI]
    public static string? EscapeSingleQuotes(string? input)
    {
        return input?.Replace("'", "''");
    }

    /// <summary>
    /// Escapes backslashes by doubling them (<c>\</c> becomes <c>\\</c>).
    /// </summary>
    /// <param name="input">The string to escape, or <see langword="null"/>.</param>
    [PublicAPI]
    public static string? EscapeBackslash(string? input)
    {
        return input?.Replace("\\", "\\\\");
    }

    /// <summary>
    /// Applies <see cref="EscapeSingleQuotes"/> to every string value, recursing into nested
    /// dictionaries. Non-string, non-dictionary values are returned unchanged.
    /// </summary>
    /// <param name="input">The value to escape: a string, an <see
    /// cref="IDictionary{TKey,TValue}"/> of strings/objects, or any other value.</param>
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

    /// <summary>
    /// Serializes <paramref name="input"/> to compact JSON (no indentation, nulls omitted). Works with
    /// <see cref="System.Dynamic.ExpandoObject"/> since it uses Newtonsoft.Json.
    /// </summary>
    /// <param name="input">The value to serialize.</param>
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

    /// <summary>
    /// Converts <paramref name="input"/> to a string: dictionaries are rendered as compact JSON via
    /// <see cref="JsonArray"/>, everything else uses <see cref="object.ToString"/>.
    /// </summary>
    /// <param name="input">The value to convert, or <see langword="null"/>.</param>
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
