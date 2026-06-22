namespace EtlKit.DataFlow
{
    [PublicAPI]
    public class ExcelRange
    {
        public int StartColumn { get; set; }
        public int StartRow { get; set; }
        public int? EndColumn { get; set; }
        public int? EndRow { get; set; }
        internal int EndColumnIfSet => EndColumn ?? int.MaxValue;
        internal int EndRowIfSet => EndRow ?? int.MaxValue;

        public ExcelRange(int startColumn, int startRow)
        {
            StartColumn = startColumn;
            StartRow = startRow;
        }

        public ExcelRange(int startColumn, int startRow, int endColumn, int endRow)
            : this(startColumn, startRow)
        {
            EndColumn = endColumn;
            EndRow = endRow;
        }
    }
}
