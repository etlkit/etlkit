using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// <see cref="System.Text.Json"/> converter that reads/writes <see cref="ExpandoObject"/> — the
    /// built-in <c>System.Text.Json</c> serializer does not support <see cref="ExpandoObject"/> directly.
    /// </summary>
    public class ExpandoObjectConverter : JsonConverter<ExpandoObject>
    {
        /// <summary>
        /// Reads a JSON object into an <see cref="ExpandoObject"/>, recursing into nested objects and
        /// arrays. Applies <paramref name="options"/>'s <see
        /// cref="JsonSerializerOptions.PropertyNamingPolicy"/> to property names, if set.
        /// </summary>
        /// <param name="reader">The reader positioned at a JSON object start.</param>
        /// <param name="typeToConvert">Ignored; always converts to <see cref="ExpandoObject"/>.</param>
        /// <param name="options">Serializer options, used for the property naming policy.</param>
        /// <exception cref="JsonException">The reader is not positioned at a JSON object, or the JSON is malformed.</exception>
        public override ExpandoObject Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject token");
            }

            IDictionary<string, object?> expando = new ExpandoObject();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return (ExpandoObject)expando;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected PropertyName token");
                }

                var propertyName = reader.GetString()!;
                if (options.PropertyNamingPolicy != null)
                    propertyName = options.PropertyNamingPolicy.ConvertName(propertyName);
                reader.Read();
                var value = ReadValue(ref reader, options);
                expando[propertyName] = value;
            }

            throw new JsonException("Expected EndObject token");
        }

        /// <summary>
        /// Writes <paramref name="value"/> by re-serializing it through the default serializer (which
        /// handles <see cref="ExpandoObject"/>'s underlying <see cref="IDictionary{TKey,TValue}"/> shape).
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The object to write.</param>
        /// <param name="options">Serializer options passed through to the underlying serialization.</param>
        public override void Write(
            Utf8JsonWriter writer,
            ExpandoObject value,
            JsonSerializerOptions options
        )
        {
            JsonSerializer.Serialize(writer, value, options);
        }

        private object? ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.StartObject => Read(ref reader, typeof(ExpandoObject), options),
                JsonTokenType.StartArray => ReadArray(ref reader, options),
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.TryGetInt64(out var l) ? l : reader.GetDouble(),
                JsonTokenType.True => reader.GetBoolean(),
                JsonTokenType.False => reader.GetBoolean(),
                JsonTokenType.Null => null,
                _ => throw new JsonException(),
            };
        }

        private object ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            var list = new List<object>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                var readValue = ReadValue(ref reader, options);
                if (readValue != null)
                    list.Add(readValue);
            }

            return list.ToArray();
        }
    }
}
