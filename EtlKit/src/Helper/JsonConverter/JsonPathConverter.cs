using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace EtlKit.Helper
{
    /// <summary>
    /// A JsonConverter that support json path expression in JsonProperty attributes.
    /// </summary>
    /// <example>
    /// <code>
    /// [JsonConverter(typeof(JsonPathConverter))]
    /// public class MySimpleRow
    /// {
    ///     [JsonProperty("Column1")]
    ///     public int Col1 { get; set; }
    ///     [JsonProperty("Column2.Value")]
    ///     public string Col2 { get; set; }
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// https://stackoverflow.com/questions/33088462/can-i-specify-a-path-in-an-attribute-to-map-a-property-in-my-class-to-a-child-pr
    /// </remarks>"
    public class JsonPathConverter : JsonConverter
    {
        /// <inheritdoc />
        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer
        )
        {
            var jObject = JObject.Load(reader);
            var targetObj = Activator.CreateInstance(objectType);

            foreach (
                PropertyInfo prop in objectType.GetProperties().Where(p => p.CanRead && p.CanWrite)
            )
            {
                JsonPropertyAttribute attribute = prop.GetCustomAttributes(true)
                    .OfType<JsonPropertyAttribute>()
                    .FirstOrDefault();

                var jsonPath = attribute != null ? attribute.PropertyName! : prop.Name;

                if (serializer.ContractResolver is DefaultContractResolver resolver)
                {
                    jsonPath = resolver.GetResolvedPropertyName(jsonPath);
                }

                if (!Regex.IsMatch(jsonPath, @"^[a-zA-Z0-9_.-]+$"))
                    throw new InvalidOperationException(
                        $"JProperties of JsonPathConverter can have only letters, numbers, underscores, hiffens and dots but name was ${jsonPath}."
                    ); // Array operations not permitted

                JToken token = jObject.SelectToken(jsonPath);
                if (token != null && token.Type != JTokenType.Null)
                {
                    var value = token.ToObject(prop.PropertyType, serializer);
                    prop.SetValue(targetObj, value, null);
                }
            }

            return targetObj;
        }

        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            // CanConvert is not called when [JsonConverter] attribute is used
            return objectType.GetCustomAttributes(true).OfType<JsonPathConverter>().Any();
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var properties = value
                .GetType()
                .GetRuntimeProperties()
                .Where(p => p.CanRead && p.CanWrite);
            var main = new JObject();
            foreach (PropertyInfo prop in properties)
            {
                JsonPropertyAttribute attribute = prop.GetCustomAttributes(true)
                    .OfType<JsonPropertyAttribute>()
                    .FirstOrDefault();

                var jsonPath = attribute != null ? attribute.PropertyName! : prop.Name;

                if (serializer.ContractResolver is DefaultContractResolver resolver)
                {
                    jsonPath = resolver.GetResolvedPropertyName(jsonPath);
                }

                var nesting = jsonPath.Split('.');
                JObject lastLevel = main;

                for (var i = 0; i < nesting.Length; i++)
                {
                    if (i == nesting.Length - 1)
                    {
                        lastLevel[nesting[i]] = new JValue(prop.GetValue(value));
                    }
                    else
                    {
                        lastLevel[nesting[i]] ??= new JObject();
                        lastLevel = (JObject)lastLevel[nesting[i]];
                    }
                }
            }

            serializer.Serialize(writer, main);
        }
    }
}
