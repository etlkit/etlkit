namespace EtlKit.DataFlow
{
    /// <summary>
    /// A rectangular range of cells in an Excel worksheet, used to bound where a source or
    /// destination reads/writes data.
    /// </summary>
    [PublicAPI]
    public class ExcelRange
    {
        /// <summary>
        /// Zero-based index of the first column in the range.
        /// </summary>
        public int StartColumn { get; set; }

        /// <summary>
        /// Zero-based index of the first row in the range.
        /// </summary>
        public int StartRow { get; set; }

        /// <summary>
        /// Zero-based index of the last column in the range, or <see langword="null"/> for unbounded.
        /// </summary>
        public int? EndColumn { get; set; }

        /// <summary>
        /// Zero-based index of the last row in the range, or <see langword="null"/> for unbounded.
        /// </summary>
        public int? EndRow { get; set; }
        internal int EndColumnIfSet => EndColumn ?? int.MaxValue;
        internal int EndRowIfSet => EndRow ?? int.MaxValue;

        /// <summary>
        /// Creates an unbounded range starting at the given column and row.
        /// </summary>
        /// <param name="startColumn">Zero-based index of the first column.</param>
        /// <param name="startRow">Zero-based index of the first row.</param>
        public ExcelRange(int startColumn, int startRow)
        {
            StartColumn = startColumn;
            StartRow = startRow;
        }

        /// <summary>
        /// Creates a bounded range between the given start and end columns/rows.
        /// </summary>
        /// <param name="startColumn">Zero-based index of the first column.</param>
        /// <param name="startRow">Zero-based index of the first row.</param>
        /// <param name="endColumn">Zero-based index of the last column.</param>
        /// <param name="endRow">Zero-based index of the last row.</param>
        public ExcelRange(int startColumn, int startRow, int endColumn, int endRow)
            : this(startColumn, startRow)
        {
            EndColumn = endColumn;
            EndRow = endRow;
        }
    }
}
