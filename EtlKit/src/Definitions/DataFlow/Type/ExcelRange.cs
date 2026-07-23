namespace EtlKit.DataFlow
{
    /// <summary>
    /// A rectangular range of cells in an Excel worksheet, used to bound where a source or
    /// destination reads/writes data.
    /// </summary>
    /// <remarks>
    /// Row and start-column bounds are one-based: <c>StartRow = 1</c> selects the first worksheet
    /// row and <c>StartColumn = 1</c> the first column. <see cref="EndColumn"/> is the exception —
    /// it is compared against the zero-based column index (see its documentation).
    /// </remarks>
    [PublicAPI]
    public class ExcelRange
    {
        /// <summary>
        /// One-based position of the first column in the range (<c>1</c> selects the first column).
        /// </summary>
        public int StartColumn { get; set; }

        /// <summary>
        /// One-based number of the first row in the range (<c>1</c> selects the first row).
        /// </summary>
        public int StartRow { get; set; }

        /// <summary>
        /// Zero-based index of the last column in the range (inclusive), or <see langword="null"/>
        /// for unbounded. Note the asymmetry with <see cref="StartColumn"/>: this bound is compared
        /// against the zero-based column index, so <c>EndColumn = 2</c> includes the first three
        /// columns.
        /// </summary>
        public int? EndColumn { get; set; }

        /// <summary>
        /// One-based number of the last row in the range (inclusive), or <see langword="null"/> for
        /// unbounded.
        /// </summary>
        public int? EndRow { get; set; }
        internal int EndColumnIfSet => EndColumn ?? int.MaxValue;
        internal int EndRowIfSet => EndRow ?? int.MaxValue;

        /// <summary>
        /// Creates an unbounded range starting at the given column and row.
        /// </summary>
        /// <param name="startColumn">One-based position of the first column.</param>
        /// <param name="startRow">One-based number of the first row.</param>
        public ExcelRange(int startColumn, int startRow)
        {
            StartColumn = startColumn;
            StartRow = startRow;
        }

        /// <summary>
        /// Creates a bounded range between the given start and end columns/rows.
        /// </summary>
        /// <param name="startColumn">One-based position of the first column.</param>
        /// <param name="startRow">One-based number of the first row.</param>
        /// <param name="endColumn">Zero-based index of the last column (inclusive).</param>
        /// <param name="endRow">One-based number of the last row (inclusive).</param>
        public ExcelRange(int startColumn, int startRow, int endColumn, int endRow)
            : this(startColumn, startRow)
        {
            EndColumn = endColumn;
            EndRow = endRow;
        }
    }
}
