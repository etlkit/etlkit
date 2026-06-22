using EtlKit.Helper;
using EtlKit.Primitives;
using ExcelDataReader;
using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Reads data from a excel source. While reading the data from the file, data is also asnychronously posted into the targets.
    /// You can define a sheet name and a range - only the data in the specified sheet and range is read. Otherwise, all data
    /// in all sheets will be processed.
    /// </summary>
    /// <example>
    /// <code>
    /// ExcelSource&lt;ExcelData&gt; source = new ExcelSource&lt;ExcelData&gt;("src/DataFlow/ExcelDataFile.xlsx") {
    ///         Range = new ExcelRange(2, 4, 5, 9),
    ///         SheetName = "Sheet2"
    ///  };
    /// </code>
    /// </example>
    [PublicAPI]
    public class ExcelSource<TOutput> : DataFlowStreamSource<TOutput>, IDataFlowSource<TOutput>
    {
        /* ITask Interface */
        public override string TaskName => $"Read excel data from Uri: {CurrentRequestUri ?? ""}";

        /* Public properties */
        public string ExcelFilePassword { get; set; }
        public ExcelRange Range { get; set; }
        public string SheetName { get; set; }
        public bool IgnoreBlankRows { get; set; }
        public bool HasNoHeader { get; set; }

        /* Private stuff */
        private readonly List<string> _headerColumns = new();
        private bool HasHeaderData => !HasNoHeader && _headerColumns?.Count > 0;
        private bool HasRange => Range != null;
        private bool HasSheetName => !string.IsNullOrWhiteSpace(SheetName);
        private bool IsHeaderRead { get; set; }
        private IExcelDataReader ExcelDataReader { get; set; }
        private ExcelTypeInfo TypeInfo { get; set; }

        public ExcelSource()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public ExcelSource([CanBeNull] ILogger<ExcelSource<TOutput>> logger)
            : base(logger)
        {
            TypeInfo = new ExcelTypeInfo(typeof(TOutput));
            ResourceType = ResourceType.File;
        }

        public ExcelSource(string uri)
            : this()
        {
            Uri = uri;
        }

        protected override void ReadAll()
        {
            do
            {
                var rowNr = 0;

                while (ExcelDataReader.Read())
                {
                    if (!ReadLine(ref rowNr))
                    {
                        break;
                    }
                }
            } while (ExcelDataReader.NextResult());
        }

        /// <summary>
        /// Read one single line
        /// </summary>
        /// <param name="rowNr">Counter of data rows read</param>
        /// <returns>true if needs to skip to the end of sheet</returns>
        private bool ReadLine(ref int rowNr)
        {
            if (CheckSkipLine(ref rowNr, out var skipToEnd))
                return skipToEnd;
            return ParseHeaderOrDataRow(rowNr);
        }

        private bool ParseHeaderOrDataRow(int rowNr)
        {
            try
            {
                if (!HasNoHeader && !IsHeaderRead)
                    ParseHeader();
                else
                {
                    TOutput row = ParseDataRow();
                    if (row is not null)
                    {
                        Buffer.SendAsync(row).Wait();
                        LogProgress();
                    }
                    else if (!IgnoreBlankRows)
                        return false;
                }
            }
            catch (Exception e)
            {
                if (!ErrorHandler.HasErrorBuffer)
                    throw;
                ErrorHandler.Send(e, $"File: {Uri} -- Sheet: {SheetName ?? ""} -- Row: {rowNr}");
            }

            return true;
        }

        /// <summary>
        /// Check if line needs to be read
        /// </summary>
        /// <param name="rowNr">Data row counter</param>
        /// <param name="skipToEnd">Calling party shall skip to the end of sheet</param>
        /// <returns>true if line shall be skipped, false if line is to be read</returns>
        private bool CheckSkipLine(ref int rowNr, out bool skipToEnd)
        {
            if (ExcelDataReader.VisibleState != "visible")
            {
                skipToEnd = true;
                return true;
            }

            if (HasSheetName && ExcelDataReader.Name != SheetName)
            {
                skipToEnd = true;
                return true;
            }

            rowNr++;
            if (HasRange && rowNr > Range.EndRowIfSet)
            {
                skipToEnd = false;
                return true;
            }

            if (HasRange && rowNr < Range.StartRow)
            {
                skipToEnd = true;
                return true;
            }

            skipToEnd = false;
            return false;
        }

        private void ParseHeader()
        {
            for (var col = 0; col < ExcelDataReader.FieldCount; col++)
            {
                if (HasRange && col > Range.EndColumnIfSet)
                    break;
                if (HasRange && col + 1 < Range.StartColumn)
                    continue;
                var value = Convert.ToString(ExcelDataReader.GetValue(col));
                _headerColumns.Add(value);
            }
            IsHeaderRead = true;
        }

        private TOutput ParseDataRow()
        {
            TOutput row = GetNewOutputInstance();
            var emptyRow = true;
            var colNrInRange = -1;
            for (var col = 0; col < ExcelDataReader.FieldCount; col++)
            {
                if (HasRange && col > Range.EndColumnIfSet)
                    break;
                if (HasRange && col + 1 < Range.StartColumn)
                    continue;
                emptyRow &= ExcelDataReader.IsDBNull(col);
                var value = ExcelDataReader.GetValue(col);
                colNrInRange++;
                SetOutputValue(row, colNrInRange, value);
            }

            return emptyRow ? default : row;
        }

        private void SetOutputValue(TOutput row, int colNrInRange, object value)
        {
            if (TypeInfo.IsArray)
                SetValueInArray(row as Array, colNrInRange, value);
            else if (TypeInfo.IsDynamic)
                SetValueInDynamic(row as dynamic, colNrInRange, value);
            else
                SetValueInObject(row, colNrInRange, value);
        }

        private TOutput GetNewOutputInstance() =>
            TypeInfo.IsArray
                ? (TOutput)Activator.CreateInstance(typeof(TOutput), ExcelDataReader.FieldCount)
                : (TOutput)Activator.CreateInstance(typeof(TOutput));

        private static void SetValueInArray(Array row, int colNrInRange, object value)
        {
            var con = Convert.ChangeType(value, typeof(TOutput).GetElementType()!);
            row.SetValue(con, colNrInRange);
        }

        private void SetValueInDynamic(
            IDictionary<string, object> row,
            int colNrInRange,
            object value
        )
        {
            if (HasHeaderData)
                row.Add(_headerColumns[colNrInRange], value);
            else
                row.Add("Column" + (colNrInRange + 1), value);
        }

        private void SetValueInObject(TOutput row, int colNrInRange, object value)
        {
            PropertyInfo propInfo = null;
            if (
                HasHeaderData
                    && TypeInfo.ExcelColumnName2PropertyIndex.TryGetValue(
                        _headerColumns[colNrInRange],
                        out var propertyIndex
                    )
                || TypeInfo.ExcelIndex2PropertyIndex.TryGetValue(colNrInRange, out propertyIndex)
            )
                propInfo = TypeInfo.Properties[propertyIndex];
            propInfo?.TrySetValue(
                row,
                ExcelTypeInfo.CastPropertyValue(propInfo, value?.ToString())
            );
        }

        protected override void InitReader()
        {
            ExcelDataReader = ExcelReaderFactory.CreateReader(
                StreamReader.BaseStream,
                new ExcelReaderConfiguration { Password = ExcelFilePassword }
            );
        }

        protected override void CloseReader()
        {
            ExcelDataReader?.Close();
        }
    }

    /// <summary>
    /// Reads data from a excel source. While reading the data from the file, data is also asnychronously posted into the targets.
    /// You can define a sheet name and a range - only the data in the specified sheet and range is read. Otherwise, all data
    /// in all sheets will be processed.
    /// The non generic class uses a dynamic object for storing the data.
    /// </summary>
    /// <see cref="ExcelSource{TOutput}"/>
    /// <example>
    /// <code>
    /// ExcelSourcesource = new ExcelSource("src/DataFlow/ExcelDataFile.xlsx") {
    ///         Range = new ExcelRange(2, 4, 5, 9),
    ///         SheetName = "Sheet2"
    ///  };
    /// </code>
    /// </example>
    [PublicAPI]
    public class ExcelSource : ExcelSource<ExpandoObject>
    {
        public ExcelSource() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public ExcelSource(ILogger<ExcelSource> logger)
            : base(logger) { }

        public ExcelSource(string uri)
            : base(uri) { }
    }
}
