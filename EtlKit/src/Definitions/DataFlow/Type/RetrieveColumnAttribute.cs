namespace EtlKit.DataFlow
{
    /// <summary>
    /// This attribute defines that this property is used to store the lookup value of the property from the object
    /// used in the Source for a Lookup identified by the given lookupSourcePropertyName.
    /// </summary>
    /// <example>
    /// <code>
    /// public class MyLookupData
    /// {
    ///     public string Id { get; set; }
    ///     public string Value { get; set; }
    /// }
    ///
    /// public class MyDataRow
    /// {
    ///     [MatchColumn("Id")]
    ///     public string MyProperty { get; set; }
    ///     [RetrieveColumn("Value")]
    ///     public string MyProperty { get; set; }
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Property)]
    public class RetrieveColumnAttribute : Attribute
    {
        /// <summary>
        /// Name of the value property on the lookup source object to copy from.
        /// </summary>
        public string LookupSourcePropertyName { get; set; }

        /// <summary>
        /// Marks the decorated property to receive the value of <paramref
        /// name="lookupSourcePropertyName"/> from the matched lookup source object.
        /// </summary>
        /// <param name="lookupSourcePropertyName">Name of the value property on the lookup source object to copy from.</param>
        public RetrieveColumnAttribute(string lookupSourcePropertyName)
        {
            LookupSourcePropertyName = lookupSourcePropertyName;
        }
    }
}
