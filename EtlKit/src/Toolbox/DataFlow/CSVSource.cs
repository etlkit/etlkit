using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using EtlKit.Primitives;
using Microsoft.Extensions.Logging;

using DataFlow_TypeInfo = EtlKit.Common.DataFlow.TypeInfo;
using TypeInfo = EtlKit.Common.DataFlow.TypeInfo;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Reads data from a csv source. While reading the data from the file, data is also asynchronously posted into the targets.
    /// Data is read a as string from the source and dynamically converted into the corresponding data format.
    /// </summary>
    /// <example>
    /// <code>
    /// CsvSource&lt;CSVData&gt; source = new CsvSource&lt;CSVData&gt;("Demo.csv");
    /// source.Configuration.Delimiter = ";";
    /// </code>
    /// </example>
    [PublicAPI]
    public class CsvSource<TOutput> : DataFlowStreamSource<TOutput>, IDataFlowSource<TOutput>
    {
        [SuppressMessage("Performance", "CA1822")]
        private static CultureInfo CsvDefaultCulture => CultureInfo.InvariantCulture;

        /* ITask Interface */
        public override string TaskName => $"Read Csv data from Uri: {CurrentRequestUri ?? ""}";

        /* Public properties */
        public CsvConfiguration Configuration { get; set; }
        public int SkipRows { get; set; }
        public string[] FieldHeaders { get; private set; }
        public bool IsHeaderRead => FieldHeaders != null;
        public int ReleaseGCPressureRowCount { get; set; } = 500;
        public Type ClassMapType { get; set; }

        /* Private stuff */
        private CsvReader CsvReader { get; set; }
        private DataFlow_TypeInfo TypeInfo { get; set; }

        public CsvSource()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public CsvSource([CanBeNull] ILogger<CsvSource<TOutput>> logger)
            : base(logger)
        {
            Configuration = new CsvConfiguration(CsvDefaultCulture);
            TypeInfo = new DataFlow_TypeInfo(typeof(TOutput)).GatherTypeInfo();
            ResourceType = ResourceType.File;
        }

        public CsvSource(string uri)
            : this()
        {
            Uri = uri;
        }

        protected override void InitReader()
        {
            StreamReader = new StreamReader(Uri, Configuration.Encoding ?? Encoding.UTF8, true);
            SkipFirstRows();
            CsvReader = new CsvReader(StreamReader, Configuration);
            if (ClassMapType != null)
            {
                CsvReader.Context.RegisterClassMap(ClassMapType);
            }
        }

        public override CultureInfo CurrentCulture => CsvDefaultCulture;

        private void SkipFirstRows()
        {
            for (var i = 0; i < SkipRows; i++)
                StreamReader.ReadLine();
        }

        protected override void ReadAll()
        {
            if (Configuration.HasHeaderRecord)
            {
                CsvReader.Read();
                CsvReader.ReadHeader();
                FieldHeaders = CsvReader.HeaderRecord;
            }
            while (CsvReader.Read())
            {
                ReadLineAndSendIntoBuffer();
                LogProgress();
            }
        }

        private void ReadLineAndSendIntoBuffer()
        {
            try
            {
                if (TypeInfo.IsArray)
                {
                    var line = CsvReader.Parser.Record;
                    Buffer.SendAsync((TOutput)(object)line).Wait();
                }
                else if (TypeInfo.IsDynamic)
                {
                    TOutput bufferObject = CsvReader.GetRecord<dynamic>();
                    Buffer.Post(bufferObject);
                }
                else
                {
                    TOutput bufferObject = CsvReader.GetRecord<TOutput>();
                    Buffer.SendAsync(bufferObject).Wait();
                }
            }
            catch (Exception e)
            {
                if (!ErrorHandler.HasErrorBuffer)
                    throw;
                if (e is CsvHelperException csvex)
                    ErrorHandler.Send(
                        e,
                        $"Row: {csvex.Context?.Parser.Row} -- RawRecord: {csvex.Context?.Parser.RawRecord ?? string.Empty}"
                    );
                else
                    ErrorHandler.Send(e, "N/A");
            }
        }

        protected override void CloseReader()
        {
            CsvReader?.Dispose();
        }
    }

    /// <summary>
    /// Reads data from a csv source. While reading the data from the file, data is also asnychronously posted into the targets.
    /// CsvSource as a nongeneric type uses dynamic object as output. If you need typed output, use
    /// the CsvSource&lt;TOutput&gt; object instead.
    /// </summary>
    /// <see cref="CsvSource{TOutput}"/>
    /// <example>
    /// <code>
    /// CsvSource source = new CsvSource("demodata.csv");
    /// source.LinkTo(dest); //Link to transformation or destination
    /// source.Execute(); //Start the dataflow
    /// </code>
    /// </example>
    [PublicAPI]
    public class CsvSource : CsvSource<ExpandoObject>
    {
        public CsvSource() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public CsvSource(ILogger<CsvSource> logger)
            : base(logger) { }

        public CsvSource(string fileName)
            : base(fileName) { }
    }
}
