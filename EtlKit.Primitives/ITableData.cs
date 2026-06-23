using System.Collections.Generic;
using System.Data;

namespace EtlKit.Primitives
{
    public interface ITableData : IDataReader
    {
        IColumnMappingCollection GetColumnMapping();
        List<object[]> Rows { get; }
    }
}
