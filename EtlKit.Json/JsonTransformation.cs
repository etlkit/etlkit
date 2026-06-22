using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

// ReSharper disable once CheckNamespace
namespace EtlKit.Common.DataFlow;

/// <summary>
/// Transforms fields represented as JSON strings into a destination object using JSONPath
/// </summary>
[PublicAPI]
public sealed class JsonTransformation : RowTransformation<ExpandoObject>
{
    /// <summary>
    /// Collection of mappings from the source object to the destination object
    /// </summary>
    public Dictionary<string, Mapping> Mappings { get; set; } = new();

    /// <summary>
    /// When <c>true</c>, all input fields are copied to the output before applying
    /// <see cref="Mappings"/>. Mappings may add new fields or override copied ones.
    /// When <c>false</c> (default), only the fields listed in <see cref="Mappings"/> appear
    /// in the output.
    /// </summary>
    public bool PassThrough { get; set; }

    /// <summary>
    /// Maps JSON properties to a destination object
    /// </summary>
    [PublicAPI]
    public sealed record Mapping
    {
        /// <summary>
        /// Default constructor for deserialization
        /// </summary>
        public Mapping()
            : this(string.Empty, string.Empty) { }

        /// <summary>
        /// Maps JSON properties to a destination object
        /// </summary>
        public Mapping(string Name, string Path)
        {
            this.Name = Name;
            this.Path = Path;
        }

        /// <summary>
        /// Name of the JSON field in the source object
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// JSONPath expression selecting the value from the JSON field.
        /// Use <c>"$"</c> to select the entire JSON object as a native
        /// <see cref="System.Dynamic.ExpandoObject"/>. Leave <c>null</c> or empty to copy
        /// the raw field value without parsing.
        /// </summary>
        public string? Path { get; set; }
    }

    /// <inheritdoc cref="JsonTransformation(ILogger{JsonTransformation}?)"/>
    public JsonTransformation()
        : this(logger: null) { }

    /// <summary>
    /// Creates a new instance with an injected logger.
    /// </summary>
    public JsonTransformation(ILogger<JsonTransformation>? logger)
        : base(logger)
    {
        TransformationFunc = TransformWithJsonPath;
    }

    private ExpandoObject TransformWithJsonPath(ExpandoObject source)
    {
        var res = new ExpandoObject();
        var access = (IDictionary<string, object?>)res;

        if (PassThrough)
        {
            foreach (var pair in (IDictionary<string, object?>)source)
                access[pair.Key] = pair.Value;
        }

        IReadOnlyDictionary<string, JObject> parsedFields = ParseJsonFields(source);

        foreach (var key in Mappings.Keys)
            access[key] = GetValue(source, parsedFields, Mappings[key]);

        return res;
    }

    private IReadOnlyDictionary<string, JObject> ParseJsonFields(
        IDictionary<string, object?> source
    ) =>
        Mappings
            .Values.Where(m => !string.IsNullOrEmpty(m.Path))
            .Select(x => x.Name)
            .Distinct()
            .Where(key => key != null && source.ContainsKey(key))
            .ToDictionary(key => key, key => JObject.Parse(source[key]!.ToString()));

    /// <summary>
    /// Parses a JSON object string and returns an <see cref="ExpandoObject"/> with native .NET values.
    /// JSON strings become <c>string</c>, integers become <c>int</c>, floats become <c>double</c>,
    /// booleans become <c>bool</c>, ISO-8601 date strings become <c>DateTime</c>,
    /// nulls become <c>null</c>, nested objects become nested <see cref="ExpandoObject"/> instances,
    /// and arrays become <c>List&lt;object?&gt;</c>.
    /// </summary>
    /// <param name="json">A valid JSON object string.</param>
    public static ExpandoObject ParseNative(string json) => ConvertJObject(JObject.Parse(json));

    private static ExpandoObject ConvertJObject(JObject obj)
    {
        var result = (IDictionary<string, object?>)new ExpandoObject();
        foreach (var prop in obj.Properties())
            result[prop.Name] = ConvertToken(prop.Value);
        return (ExpandoObject)result;
    }

    private static object? ConvertToken(JToken? token) =>
        token == null
            ? null
            : token.Type switch
            {
                JTokenType.Null => null,
                JTokenType.Boolean => token.ToObject<bool>(),
                JTokenType.Date => token.ToObject<DateTime>(),
                JTokenType.Integer => token.ToObject<int>(),
                JTokenType.Float => token.ToObject<double>(),
                JTokenType.Object => ConvertJObject((JObject)token),
                JTokenType.Array => ((JArray)token).Select(ConvertToken).ToList(),
                _ => token.ToObject<string>(),
            };

    private static object? GetValue(
        IDictionary<string, object?> sourceObject,
        IReadOnlyDictionary<string, JObject> parsedJsonFields,
        Mapping mapping
    )
    {
        // No path: copy the raw field value without JSON parsing
        if (string.IsNullOrEmpty(mapping.Path))
            return sourceObject[mapping.Name];

        // Field is not present or is not valid JSON
        if (!parsedJsonFields.TryGetValue(mapping.Name, out JObject? jsonObj))
            return string.Empty;

        // Use JSONPath to retrieve the value and convert to a native .NET type.
        // Path="$" selects the root object and returns a native ExpandoObject.
        JToken value = jsonObj.SelectToken(mapping.Path!)!;
        return ConvertToken(value);
    }
}
