namespace EtlKit.DataFlow
{
    /// <summary>
    /// This attribute defines either which column index is mapped to the property or the
    /// header name that identifies the column
    /// By default, when reading from an excel file, a header column is expected in the first row.
    /// The name of the header is used to match with the property names of the object.
    /// With this attribute, you can define the column index of the excel column for the property or
    /// a different header name for a property.
    /// The index starts at 0.
    /// </summary>
    /// <example>
    ///  public class MyPoco
    /// {
    ///     [ExcelColumn("HeaderName")]
    ///     public string ColumnByHeaderName { get; set; }
    ///     [ExcelColumn(2)]
    ///     public string ThirdColumnInExcel { get; set; }
    /// }
    /// </example>
    [AttributeUsage(AttributeTargets.Property)]
    public class ExcelColumnAttribute : Attribute
    {
        public int? Index { get; set; }
        public string ColumnName { get; set; }

        public ExcelColumnAttribute(int columnIndex)
        {
            Index = columnIndex;
        }

        public ExcelColumnAttribute(string columnName)
        {
            ColumnName = columnName;
        }
    }
}
