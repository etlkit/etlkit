namespace EtlKit.DataFlow
{
    /// <summary>
    /// This attribute defines that this property is used to match with the property of the object
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
    public class MatchColumnAttribute : Attribute
    {
        /// <summary>
        /// Name of the matching property on the lookup source object.
        /// </summary>
        public string LookupSourcePropertyName { get; set; }

        /// <summary>
        /// Marks the decorated property as the match key, compared against <paramref
        /// name="lookupSourcePropertyName"/> on the lookup source object.
        /// </summary>
        /// <param name="lookupSourcePropertyName">Name of the matching property on the lookup source object.</param>
        public MatchColumnAttribute(string lookupSourcePropertyName)
        {
            LookupSourcePropertyName = lookupSourcePropertyName;
        }
    }
}
