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
        public object DeleteOnMatchValue { get; set; }

        public DeleteColumnAttribute(object deleteOnMatchValue)
        {
            DeleteOnMatchValue = deleteOnMatchValue;
        }
    }
}
