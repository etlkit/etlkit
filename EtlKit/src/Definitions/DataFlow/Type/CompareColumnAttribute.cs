namespace EtlKit.DataFlow
{
    /// <summary>
    /// This attribute defines if the column is included in the comparison to identify
    /// object that exists and needs to be updated. It it supposed
    /// to use with an object that either inherits from MergeableRow.
    /// If you implement the IMergeable interface, you need to override the Equals-method instead.
    /// </summary>
    /// <example>
    ///  public class MyPoco : MergeableRow
    /// {
    ///     [IdColumn]
    ///     public int Key { get; set; }
    ///     [CompareColumn]
    ///     public string Value {get;set; }
    /// }
    /// </example>
    [AttributeUsage(AttributeTargets.Property)]
    public class CompareColumnAttribute : Attribute { }
}
