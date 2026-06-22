using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EtlKit.Helper
{
    [PublicAPI]
    public class JsonProperty2JsonPath
    {
        public string JsonPropertyName { get; set; }
        public string JsonPath { get; set; }
        public string NewPropertyName
        {
            get => _newPropertyName ?? JsonPropertyName;
            set => _newPropertyName = value;
        }
        private string _newPropertyName;

        public bool Validate()
        {
            if (
                string.IsNullOrWhiteSpace(JsonPropertyName)
                || string.IsNullOrWhiteSpace(JsonPath)
                || string.IsNullOrWhiteSpace(NewPropertyName)
            )
                return false;
            return true;
        }
    }

    /// <summary>
    /// Allows to pass JsonPath string that are applied for particular property names - this will work one on the first level
    /// of the
    /// </summary>
    /// <remarks>
    /// https://github.com/JamesNK/Newtonsoft.Json/blob/master/Src/Newtonsoft.Json/Converters/ExpandoObjectConverter.cs
    /// </remarks>
    public class ExpandoJsonPathConverter : JsonConverter
    {
        private IEnumerable<JsonProperty2JsonPath> PathLookups { get; set; }

        public ExpandoJsonPathConverter(IEnumerable<JsonProperty2JsonPath> pathLookups)
        {
            PathLookups = pathLookups;
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // can write is set to false
        }

        /// <inheritdoc />
        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer
        )
        {
            return ReadValue(reader);
        }

        private object ReadValue(JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonToken.StartObject:
                    return ReadObject(reader);
                default:
                    if (IsPrimitiveToken(reader.TokenType))
                    {
                        return reader.Value;
                    }
                    reader.Skip();
                    return null;
            }
        }

        private object ReadObject(JsonReader reader)
        {
            IDictionary<string, object> expandoObject = new ExpandoObject();

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        var propertyName = reader.Value?.ToString();
                        ReadProperty(reader, expandoObject, propertyName);
                        break;
                    case JsonToken.Comment:
                        break;
                    case JsonToken.EndObject:
                        return expandoObject;
                }
            }

            throw new JsonSerializationException("Unexpected end when reading ExpandoObject.");
        }

        private void ReadProperty(
            JsonReader reader,
            IDictionary<string, object> expandoObject,
            string propertyName
        )
        {
            if (!reader.Read())
                throw new JsonSerializationException("Unexpected end when reading ExpandoObject.");

            if (IsPrimitiveToken(reader.TokenType))
            {
                expandoObject[propertyName] = reader.Value;
            }
            else
            {
                var jsonObject = JToken.Load(reader);
                foreach (
                    var pl in PathLookups.Where(
                        l => l.JsonPropertyName == propertyName && l.Validate()
                    )
                )
                {
                    expandoObject[pl.NewPropertyName] = GetValueFromJsonPath(
                        jsonObject,
                        pl.JsonPath
                    );
                }
            }
        }

        private static object GetValueFromJsonPath(JToken jsonObject, string path)
        {
            object val = null;
            var tokens = jsonObject.SelectTokens(path).ToList();
            switch (tokens.Count)
            {
                case 1:
                    {
                        JToken t = tokens[0];
                        val = ParseToken(t);
                        break;
                    }
                case > 1:
                    {
                        val = tokens.Select(ParseToken).ToList();
                        break;
                    }
            }
            return val;
        }

        private static object ParseToken(JToken t)
        {
            return t is JValue value ? value.Value : t.ToString();
        }

        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ExpandoObject);
        }

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// https://github.com/JamesNK/Newtonsoft.Json/blob/master/Src/Newtonsoft.Json/Utilities/JsonTokenUtils.cs
        private static bool IsPrimitiveToken(JsonToken token)
        {
            return token switch
            {
                JsonToken.Integer => true,
                JsonToken.Float => true,
                JsonToken.String => true,
                JsonToken.Boolean => true,
                JsonToken.Undefined => true,
                JsonToken.Null => true,
                JsonToken.Date => true,
                JsonToken.Bytes => true,
                _ => false
            };
        }
    }
}
