namespace EtlKit.DataFlow
{
    /// <summary>
    /// This attribute defines if the column is used as an Id for the DBMerge. It it supposed
    /// to use with an object that either inherits from MergeableRow or implements the IMergeable interface.
    /// If you do not provide this attribute, you need to override the UniqueId property
    /// if you inherit from MergeableRow.
    /// </summary>
    /// <example>
    ///  public class MyPoco : MergeableRow
    /// {
    ///     [IdColumn]
    ///     public int Key { get; set; }
    ///     public string Value {get;set; }
    /// }
    /// </example>
    [AttributeUsage(AttributeTargets.Property)]
    public class IdColumnAttribute : Attribute { }
}
