using EtlKit.Primitives;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Inherit from this class if you want to use your data object with a DBMerge,
    /// but don't want to implement <see cref="IMergeableRow" /> yourself.
    /// You still needs that you have flagged the id properties with the IdColumn attribute
    /// and the properties use to identify equal object flagged with the CompareColumn attribute.
    /// </summary>
    /// <see cref="CompareColumnAttribute"/>
    /// <see cref="IdColumnAttribute"/>
    public abstract class MergeableRow : IMergeableRow
    {
        /// <summary>
        /// Date and time when the object was considered for merging.
        /// </summary>
        public DateTime ChangeDate { get; set; }

        /// <summary>
        /// The result of a merge operation - this is either 'I' for Insertion,
        /// 'U' for Updates, 'E' for existing records (no change), and 'D' for deleted records.
        /// </summary>
        public ChangeAction? ChangeAction { get; set; }
    }
}
