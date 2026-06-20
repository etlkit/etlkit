using EtlKit.Primitives;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Reads data from a json source. This can be any http resource or a file.
    /// By default, data is pulled via httpclient. Use the ResourceType property to read data from a file.
    /// </summary>
    /// <example>
    /// <code>
    /// JsonSource&lt;POCO&gt; source = new JsonSource&lt;POCO&gt;("https://jsonplaceholder.typicode.com/todos");
    /// </code>
    /// </example>
    [PublicAPI]
    public class JsonSource<TOutput> : DataFlowStreamSource<TOutput>, IDataFlowSource<TOutput>
    {
        /* ITask Interface */
        public override string TaskName => $"Read Json from Uri: {CurrentRequestUri ?? ""}";

        /* Public properties */
        /// <summary>
        /// The Newtonsoft.Json.JsonSerializer used to deserialize the json into the used data type.
        /// </summary>
        public JsonSerializer JsonSerializer { get; set; }

        /* Private stuff */
        private JsonTextReader JsonTextReader { get; set; }

        public JsonSource()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public JsonSource([CanBeNull] ILogger<JsonSource<TOutput>> logger)
            : base(logger)
        {
            JsonSerializer = new JsonSerializer();
        }

        public JsonSource(string uri)
            : this()
        {
            Uri = uri;
        }

        public JsonSource(string uri, ResourceType resourceType)
            : this(uri)
        {
            ResourceType = resourceType;
        }

        protected override void InitReader()
        {
            JsonTextReader = new JsonTextReader(StreamReader);
        }

        protected override void ReadAll()
        {
            SkipToStartOfArray();

            var skipRecord = false;

            if (ErrorHandler.HasErrorBuffer)
            {
                JsonSerializer.Error += (object _, ErrorEventArgs args) =>
                {
                    ErrorHandler.Send(args.ErrorContext.Error, args.ErrorContext.Error.Message);
                    args.ErrorContext.Handled = true;
                    skipRecord = true;
                };
            }

            while (JsonTextReader.Read())
            {
                if (IsEndingToken(JsonTextReader.TokenType))
                    continue;
                TOutput record = JsonSerializer.Deserialize<TOutput>(JsonTextReader);
                if (skipRecord)
                {
                    if (JsonTextReader.TokenType == JsonToken.EndObject)
                        skipRecord = false;
                    continue;
                }
                Buffer.SendAsync(record).Wait();
                LogProgress();
            }
        }

        private void SkipToStartOfArray()
        {
            do
            {
                // Nothing
            } while (JsonTextReader.Read() && JsonTextReader.TokenType != JsonToken.StartArray);
        }

        private static bool IsEndingToken(JsonToken tokenType)
        {
            return tokenType is JsonToken.EndArray or JsonToken.EndObject;
        }

        protected override void CloseReader()
        {
            JsonTextReader?.Close();
        }
    }

    /// <summary>
    /// Reads data from a json source. While reading the data from the file, data is also asnychronously posted into the targets.
    /// JsonSource as a nongeneric type returns a dynamic object as output. If you need typed output, use
    /// the JsonSource&lt;TOutput&gt; object instead.
    /// </summary>
    /// <see cref="JsonSource{TOutput}"/>
    /// <example>
    /// <code>
    /// JsonSource source = new JsonSource("demodata.json");
    /// source.LinkTo(dest); //Link to transformation or destination
    /// source.Execute(); //Start the dataflow
    /// </code>
    /// </example>
    [PublicAPI]
    public sealed class JsonSource : JsonSource<ExpandoObject>
    {
        public JsonSource() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public JsonSource(ILogger<JsonSource> logger)
            : base(logger) { }

        public JsonSource(string uri)
            : base(uri) { }

        public JsonSource(string uri, ResourceType resourceType)
            : base(uri, resourceType) { }
    }
}
