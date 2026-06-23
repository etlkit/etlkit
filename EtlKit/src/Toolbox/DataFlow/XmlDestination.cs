using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

using EtlKit.Common.DataFlow;

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// A Xml destination defines a xml file where data from the flow is inserted.
    /// </summary>
    /// <see cref="XmlDestination"/>
    /// <typeparam name="TInput">Type of data input.</typeparam>
    /// <example>
    /// <code>
    /// XmlDestination&lt;MyRow&gt; dest = new XmlDestination&lt;MyRow&gt;("/path/to/file.json");
    /// dest.Wait(); //Wait for all data to arrive
    /// </code>
    /// </example>
    [PublicAPI]
    public class XmlDestination<TInput> : DataFlowStreamDestination<TInput>
    {
        /* ITask Interface */
        public override string TaskName => $"Write Xml into file {Uri ?? ""}";

        public string RootElementName { get; set; } = "Root";
        public string DynamicElementName { get; set; }
        public XmlWriter XmlWriter { get; set; }
        public XmlWriterSettings Settings { get; set; } =
            new() { Indent = true, NamespaceHandling = NamespaceHandling.OmitDuplicates };

        private XmlSerializer XmlSerializer { get; set; }
        private XmlTypeInfo TypeInfo { get; set; }
        private XmlSerializerNamespaces Ns { get; set; }

        public XmlDestination()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public XmlDestination([CanBeNull] ILogger<XmlDestination<TInput>> logger)
            : base(logger)
        {
            TypeInfo = new XmlTypeInfo(typeof(TInput));
            if (!TypeInfo.IsDynamic)
                XmlSerializer = new XmlSerializer(typeof(TInput), "");
            InitTargetAction();
        }

        public XmlDestination(string fileName)
            : this()
        {
            Uri = fileName;
        }

        public XmlDestination(string uri, ResourceType resourceType)
            : this(uri)
        {
            ResourceType = resourceType;
        }

        protected override void InitStream()
        {
            Ns = new XmlSerializerNamespaces();
            Ns.Add("", "");
            XmlWriter = XmlWriter.Create(StreamWriter, Settings);
            XmlWriter.WriteStartDocument();
            XmlWriter.WriteStartElement(RootElementName);
        }

        protected override void WriteIntoStream(TInput data)
        {
            if (data is null)
                return;
            try
            {
                if (TypeInfo.IsDynamic)
                {
                    var json = JsonConvert.SerializeObject(data);
                    XDocument doc = JsonConvert.DeserializeXNode(
                        json,
                        DynamicElementName ?? "Dynamic"
                    );
                    doc?.Root?.WriteTo(XmlWriter);
                }
                else
                {
                    XmlSerializer.Serialize(XmlWriter, data, Ns);
                }
            }
            catch (Exception e)
            {
                if (!ErrorHandler.HasErrorBuffer)
                    throw;
                ErrorHandler.Send(e, ErrorHandler.ConvertErrorData(data));
            }
            LogProgress();
        }

        protected override void CloseStream()
        {
            XmlWriter?.WriteEndElement();
            XmlWriter?.Flush();
            XmlWriter?.Close();
        }
    }

    /// <summary>
    /// A Xml destination defines a Xml file where data from the flow is inserted.
    /// The XmlDestination uses a dynamic object as input type. If you need other data types, use the generic CsvDestination instead.
    /// </summary>
    /// <see cref="XmlDestination{TInput}"/>
    [PublicAPI]
    public sealed class XmlDestination : XmlDestination<ExpandoObject>
    {
        public XmlDestination() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public XmlDestination(ILogger<XmlDestination> logger)
            : base(logger) { }

        public XmlDestination(string fileName)
            : base(fileName) { }
    }
}
