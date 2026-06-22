using CsvHelper;
using CsvHelper.Configuration;

using EtlKit.Common.DataFlow;

using Microsoft.Extensions.Logging;

using DataFlow_TypeInfo = EtlKit.Common.DataFlow.TypeInfo;
using TypeInfo = EtlKit.Common.DataFlow.TypeInfo;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// A Csv destination defines a csv file where data from the flow is inserted.
    /// </summary>
    /// <see cref="DbDestination"/>
    /// <typeparam name="TInput">Type of data input.</typeparam>
    /// <example>
    /// <code>
    /// CsvDestination&lt;MyRow&gt; dest = new CsvDestination&lt;MyRow&gt;("/path/to/file.csv");
    /// dest.Wait(); //Wait for all data to arrive
    /// </code>
    /// </example>
    [PublicAPI]
    public class CsvDestination<TInput> : DataFlowStreamDestination<TInput>
    {
        /* ITask Interface */
        public override string TaskName => $"Write Csv data into file {Uri ?? ""}";
        public CsvConfiguration Configuration { get; set; }

        private CsvWriter CsvWriter { get; set; }
        private DataFlow_TypeInfo TypeInfo { get; set; }

        public CsvDestination()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public CsvDestination([CanBeNull] ILogger<CsvDestination<TInput>> logger)
            : base(logger)
        {
            Configuration = new CsvConfiguration(CultureInfo.InvariantCulture);

            TypeInfo = new DataFlow_TypeInfo(typeof(TInput)).GatherTypeInfo();
            ResourceType = ResourceType.File;
            InitTargetAction();
        }

        public CsvDestination(string uri)
            : this()
        {
            Uri = uri;
        }

        protected override void InitStream()
        {
            CsvWriter = new CsvWriter(StreamWriter, Configuration, true);
            CsvWriter.Context.TypeConverterOptionsCache.GetOptions<DateTime>().Formats = new[]
            {
                "yyyy-MM-dd HH:mm:ss.fff",
            };
        }

        public override CultureInfo CurrentCulture => CultureInfo.InvariantCulture;

        protected override void WriteIntoStream(TInput data)
        {
            WriteHeaderIfRequired(data);
            if (TypeInfo.IsArray)
                WriteArray(data);
            else
                WriteObject(data);

            LogProgress();
        }

        private void WriteHeaderIfRequired(TInput tInput)
        {
            if (
                TypeInfo.IsArray
                || !Configuration.HasHeaderRecord
                || CsvWriter.HeaderRecord != null
            )
            {
                return;
            }

            if (TypeInfo.IsDynamic)
            {
                CsvWriter.WriteDynamicHeader(tInput as ExpandoObject);
            }
            else
            {
                CsvWriter.WriteHeader<TInput>();
            }

            CsvWriter.NextRecord();
        }

        private void WriteArray(TInput data)
        {
            if (data is null)
                return;
            var recordAsArray = data as object[];
            try
            {
                foreach (var field in recordAsArray!)
                {
                    CsvWriter.WriteField(field);
                }
            }
            catch (Exception e)
            {
                if (!ErrorHandler.HasErrorBuffer)
                    throw;
                ErrorHandler.Send(e, ErrorHandler.ConvertErrorData(data));
            }

            CsvWriter.NextRecord();
        }

        private void WriteObject(TInput data)
        {
            if (data is null)
                return;
            try
            {
                CsvWriter.WriteRecord(data);
            }
            catch (Exception e)
            {
                if (!ErrorHandler.HasErrorBuffer)
                    throw;
                ErrorHandler.Send(e, ErrorHandler.ConvertErrorData(data));
            }

            CsvWriter.NextRecord();
        }

        protected override void CloseStream()
        {
            CsvWriter?.Flush();
            CsvWriter?.Dispose();
        }
    }

    /// <summary>
    /// A Csv destination defines a csv file where data from the flow is inserted.
    /// The CsvDestination uses a dynamic object as input type. If you need other data types, use the generic CsvDestination instead.
    /// </summary>
    /// <see cref="CsvDestination{TInput}"/>
    /// <example>
    /// <code>
    /// //Non generic CsvDestination works with dynamic object as input
    /// //use CsvDestination&lt;TInput&gt; for generic usage!
    /// CsvDestination dest = new CsvDestination("/path/to/file.csv");
    /// dest.Wait(); //Wait for all data to arrive
    /// </code>
    /// </example>
    [PublicAPI]
    public class CsvDestination : CsvDestination<ExpandoObject>
    {
        public CsvDestination() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public CsvDestination(ILogger<CsvDestination> logger)
            : base(logger) { }

        public CsvDestination(string fileName)
            : base(fileName) { }
    }
}
