namespace EtlKit.DataFlow
{
    /// <summary>
    /// This attribute defines a column name to which the value of the property is mapped when writing or reading
    /// from a database.
    /// By default, when reading from a database source the mapping which property stores data of which column
    /// is resolved by the property names. Using this attribute, you can specify the column name that maps to a propety.
    /// </summary>
    /// <example>
    ///  public class MyPoco
    /// {
    ///     [ColumnMap("Column1")]
    ///     public string MyProperty { get; set; }
    /// }
    /// </example>
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnMapAttribute : Attribute
    {
        public string ColumnName { get; set; }

        public ColumnMapAttribute(string columnName)
        {
            ColumnName = columnName;
        }
    }
}
