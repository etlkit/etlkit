namespace EtlKit.DataFlow
{
    /// <summary>
    /// This attribute defines if the column is used to identify if the record is supposed to be deleted.
    /// If this attribute is set and the given value matches the column of the assigned property,
    /// the DbMerge will know that if the records matches (identifed by the IdColumn attribute)
    /// it should be deleted.
    /// </summary>
    /// <example>
    ///  public class MyPoco : MergeableRow
    /// {
    ///     [IdColumn]
    ///     public int Key { get; set; }
    ///     [CompareColumn]
    ///     public string Value {get;set; }
    ///     [DeleteColumn(true)]
    ///     public bool IsDeletion {get;set; }
    /// }
    /// </example>
    [AttributeUsage(AttributeTargets.Property)]
    public class DeleteColumnAttribute : Attribute
    {
        /// <summary>
        /// The value that marks a row for deletion when the decorated property equals it.
        /// </summary>
        public object DeleteOnMatchValue { get; set; }

        /// <summary>
        /// Marks the decorated property as the deletion indicator, using <paramref
        /// name="deleteOnMatchValue"/> as the value that means "delete this row".
        /// </summary>
        /// <param name="deleteOnMatchValue">The value that marks a row for deletion.</param>
        public DeleteColumnAttribute(object deleteOnMatchValue)
        {
            DeleteOnMatchValue = deleteOnMatchValue;
        }
    }
}
