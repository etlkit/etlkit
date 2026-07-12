using System.Collections.Generic;
using System.Data;

namespace EtlKit.Primitives
{
    /// <summary>
    /// An in-memory batch of rows passed to <see cref="IConnectionManager.BulkInsert"/>, readable as
    /// an <see cref="IDataReader"/> for drivers that consume one (e.g. <c>SqlBulkCopy</c>).
    /// </summary>
    public interface ITableData : IDataReader
    {
        /// <summary>
        /// Maps source columns to destination columns for drivers that require an explicit mapping
        /// (e.g. <c>SqlBulkCopy</c>).
        /// </summary>
        IColumnMappingCollection GetColumnMapping();

        /// <summary>
        /// The buffered rows, each row represented as an array of column values in column order.
        /// </summary>
        List<object[]> Rows { get; }
    }
}
