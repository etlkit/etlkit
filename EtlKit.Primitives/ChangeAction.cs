namespace EtlKit.Primitives
{
    /// <summary>
    /// Identifies how a row was classified when comparing source data against a destination table
    /// during a merge operation (see <c>DBMerge</c>).
    /// </summary>
    public enum ChangeAction
    {
        /// <summary>
        /// The row is present, unchanged, in both source and destination.
        /// </summary>
        Exists = 0,

        /// <summary>
        /// The row is present only in the source and must be inserted into the destination.
        /// </summary>
        Insert = 1,

        /// <summary>
        /// The row is present in both source and destination but with different values, and must be
        /// updated in the destination.
        /// </summary>
        Update = 2,

        /// <summary>
        /// The row is present only in the destination and must be deleted, because it no longer
        /// exists in the source.
        /// </summary>
        Delete = 3,
    }
}
