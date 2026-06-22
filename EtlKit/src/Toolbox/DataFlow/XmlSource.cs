using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using EtlKit.Primitives;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Reads data from a xml source. This can be any http resource or a file.
    /// By default, data is pulled via httpclient. Use the ResourceType property to read data from a file.
    /// </summary>
    /// <example>
    /// <code>
    /// XmlSource&lt;POCO&gt; source = new XmlSource&lt;POCO&gt;("https://jsonplaceholder.typicode.com/todos");
    /// </code>
    /// </example>
    [PublicAPI]
    public class XmlSource<TOutput> : DataFlowStreamSource<TOutput>, IDataFlowSource<TOutput>
    {
        /* ITask Interface */
        public override string TaskName => $"Read Xml from Uri: {CurrentRequestUri ?? ""}";

        /// <summary>
        /// The XmlSerializer used to deserialize the xml into the used data type.
        /// </summary>
        public XmlSerializer XmlSerializer { get; set; }
        public string ElementName { get; set; }

        /// <summary>
        /// The element name of the document that contains an item of the data to be parsed
        /// </summary>

        /* Private stuff */
        private XmlReader XmlReader { get; set; }

        private XmlTypeInfo TypeInfo { get; set; }

        public XmlSource()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public XmlSource([CanBeNull] ILogger<XmlSource<TOutput>> logger)
            : base(logger)
        {
            TypeInfo = new XmlTypeInfo(typeof(TOutput));
            if (!TypeInfo.IsDynamic)
                XmlSerializer = new XmlSerializer(typeof(TOutput));
        }

        public XmlSource(string uri)
            : this()
        {
            Uri = uri;
        }

        public XmlSource(string uri, ResourceType resourceType)
            : this(uri)
        {
            ResourceType = resourceType;
        }

        protected override void InitReader()
        {
            XmlReader = XmlReader.Create(StreamReader);
        }

        protected override void ReadAll()
        {
            XmlReader.MoveToContent();
            while (XmlReader.Read())
            {
                if (
                    XmlReader.NodeType != XmlNodeType.Element
                    || XmlReader.Name != (ElementName ?? TypeInfo.ElementName)
                    || XNode.ReadFrom(XmlReader) is not XElement el
                )
                {
                    continue;
                }

                ReadElement(el);
                LogProgress();
            }
        }

        private void ReadElement(XElement xmlElement)
        {
            try
            {
                TOutput output;
                if (TypeInfo.IsDynamic)
                {
                    var jsonText = JsonConvert.SerializeXNode(xmlElement);
                    dynamic res = JsonConvert.DeserializeObject<ExpandoObject>(jsonText);
                    output = ((IDictionary<string, object>)res)[ElementName] as dynamic;
                }
                else
                {
                    output = (TOutput)XmlSerializer.Deserialize(xmlElement.CreateReader());
                }

                Buffer.SendAsync(output).Wait();
            }
            catch (Exception e)
            {
                if (!ErrorHandler.HasErrorBuffer)
                    throw;
                ErrorHandler.Send(e, xmlElement.ToString());
            }
        }

        protected override void CloseReader()
        {
            XmlReader?.Close();
        }
    }

    /// <summary>
    /// Reads data from a xml source. While reading the data from the file, data is also asnychronously posted into the targets.
    /// XmlSource as a nongeneric type returns a dynamic object as output. If you need typed output, use
    /// the XmlSource&lt;TOutput&gt; object instead.
    /// </summary>
    /// <see cref="XmlSource{TOutput}"/>
    /// <example>
    /// <code>
    /// XmlSource source = new XmlSource("demodata.json");
    /// source.LinkTo(dest); //Link to transformation or destination
    /// source.Execute(); //Start the dataflow
    /// </code>
    /// </example>
    [PublicAPI]
    public class XmlSource : XmlSource<ExpandoObject>
    {
        public XmlSource() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public XmlSource(ILogger<XmlSource> logger)
            : base(logger) { }

        public XmlSource(string uri)
            : base(uri) { }

        public XmlSource(string uri, ResourceType resourceType)
            : base(uri, resourceType) { }
    }
}
