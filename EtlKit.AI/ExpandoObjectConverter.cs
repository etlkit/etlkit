using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EtlKit.AI
{
    public sealed class ExpandoObjectConverter : JsonConverter<ExpandoObject>
    {
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
                reader.Read();
                var value = ReadValue(ref reader, options);
                expando[propertyName] = value;
            }

            throw new JsonException("Expected EndObject token");
        }

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
                JsonTokenType.Number => GetNumber(reader),
                JsonTokenType.True => reader.GetBoolean(),
                JsonTokenType.False => reader.GetBoolean(),
                JsonTokenType.Null => null,
                _ => throw new JsonException(),
            };
        }

        private static object GetNumber(Utf8JsonReader reader)
        {
            if (reader.TryGetInt64(out var l))
            {
                return l;
            }
            return reader.GetDouble();
        }

        private object?[] ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            var list = new List<object?>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                var readValue = ReadValue(ref reader, options);
                list.Add(readValue);
            }

            return list.ToArray();
        }
    }
}
