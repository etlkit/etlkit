using System.Data.Common;
using System.Linq;

using EtlKit.Common;
using EtlKit.Primitives;

namespace EtlKit
{
    [PublicAPI]
    public sealed class TableData : TableData<object[]>
    {
        public TableData(TableDefinition definition)
            : base(definition) { }

        public TableData(TableDefinition definition, int estimatedBatchSize)
            : base(definition, estimatedBatchSize) { }
    }

    [PublicAPI]
    public class TableData<T> : ITableData
    {
        public IColumnMappingCollection GetColumnMapping()
        {
            if (HasDefinition)
                return GetColumnMappingFromDefinition();
            throw new EtlKitException(
                "No table definition found. For Bulk insert a TableDefinition is always needed."
            );
        }

        private DataColumnMappingCollection GetColumnMappingFromDefinition()
        {
            IEnumerable<TableColumn> columns = (TypeInfo?.IsDynamic, TypeInfo?.IsArray) switch
            {
                (_, true) => Definition.Columns.Where(c => !c.IsIdentity),
                (true, false)
                    => Definition.Columns.Where(
                        c => !c.IsIdentity && DynamicColumnNames.ContainsKey(c.Name)
                    ),
                (_, _)
                    => Definition.Columns.Where(
                        c => !c.IsIdentity && TypeInfo.HasPropertyOrColumnMapping(c.Name)
                    )
            };
            var mapping = new DataColumnMappingCollection();
            mapping.AddRange(
                columns
                    .Select(col => new DataColumnMapping(col.SourceColumn, col.DataSetColumn))
                    .ToArray()
            );
            return mapping;
        }

        public List<object[]> Rows { get; private set; }
        public object[] CurrentRow { get; private set; }
        public Dictionary<string, int> DynamicColumnNames { get; set; } = new();
        private int ReadIndex { get; set; }
        private TableDefinition Definition { get; set; }
        private bool HasDefinition => Definition != null;
        private DataFlow.DBTypeInfo TypeInfo { get; set; }
        private int? IDColumnIndex { get; set; }
        private bool HasIDColumnIndex => IDColumnIndex != null;

        public TableData(TableDefinition definition)
        {
            InitObjects(definition);
        }

        public TableData(TableDefinition definition, int estimatedBatchSize)
        {
            InitObjects(definition, estimatedBatchSize);
        }

        private void InitObjects(TableDefinition definition, int estimatedBatchSize = 0)
        {
            Definition = definition;
            IDColumnIndex = Definition.IDColumnIndex;
            Rows = new List<object[]>(estimatedBatchSize);
            TypeInfo = new DataFlow.DBTypeInfo(typeof(T));
        }

        public object this[string name] => Rows[GetOrdinal(name)];
        public object this[int i] => Rows[i];
        public int Depth => 0;
        public int FieldCount => Rows.Count;
        public bool IsClosed => Rows.Count == 0;
        public int RecordsAffected => Rows.Count;

        public bool GetBoolean(int i) => Convert.ToBoolean(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        public byte GetByte(int i) => Convert.ToByte(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        public long GetBytes(
            int i,
            long fieldOffset,
            byte[] buffer,
            int bufferoffset,
            int length
        ) => 0;

        public char GetChar(int i) => Convert.ToChar(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            var value = Convert.ToString(CurrentRow[ShiftIndexAroundIDColumn(i)]);
            buffer = value.Substring(bufferoffset, length).ToCharArray();
            return buffer.Length;
        }

        public DateTime GetDateTime(int i) =>
            Convert.ToDateTime(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        public IDataReader GetData(int i) => throw new NotImplementedException();

        public decimal GetDecimal(int i) =>
            Convert.ToDecimal(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        public double GetDouble(int i) => Convert.ToDouble(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        public float GetFloat(int i) =>
            float.Parse(Convert.ToString(CurrentRow[ShiftIndexAroundIDColumn(i)]));

        public Guid GetGuid(int i) =>
            Guid.Parse(Convert.ToString(CurrentRow[ShiftIndexAroundIDColumn(i)]));

        public short GetInt16(int i) => Convert.ToInt16(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        public int GetInt32(int i) => Convert.ToInt32(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        public long GetInt64(int i) => Convert.ToInt64(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        public string GetName(int i) => throw new NotImplementedException();

        public string GetDataTypeName(int i) => throw new NotImplementedException();

        public Type GetFieldType(int i) => throw new NotImplementedException();

        public int GetOrdinal(string name) => FindOrdinalInObject(name);

        private int FindOrdinalInObject(string name)
        {
            return TypeInfo?.GetTypeInfoGroup() switch
            {
                Common.DataFlow.TypeInfo.TypeInfoGroup.Array
                or null
                    => Definition.Columns.FindIndex(col => col.Name == name),
                Common.DataFlow.TypeInfo.TypeInfoGroup.Dynamic
                    => IncrementIfAfterIdColumn(DynamicColumnNames[name]),
                _ => IncrementIfAfterIdColumn(TypeInfo!.GetIndexByPropertyNameOrColumnMapping(name))
            };

            int IncrementIfAfterIdColumn(int ix)
            {
                if (HasIDColumnIndex && ix >= IDColumnIndex)
                    ix++;
                return ix;
            }
        }

        public DataTable GetSchemaTable()
        {
            throw new NotImplementedException();
        }

        public string GetString(int i) => Convert.ToString(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        public object GetValue(int i) =>
            CurrentRow.Length > ShiftIndexAroundIDColumn(i)
                ? CurrentRow[ShiftIndexAroundIDColumn(i)]
                : null;

        private int ShiftIndexAroundIDColumn(int i) =>
            HasIDColumnIndex switch
            {
                false => i,
                _ => i > IDColumnIndex ? i - 1 : i
            };

        public int GetValues(object[] values)
        {
            values = CurrentRow;
            return values.Length;
        }

        public bool IsDBNull(int i)
        {
            return CurrentRow.Length <= ShiftIndexAroundIDColumn(i)
                || CurrentRow[ShiftIndexAroundIDColumn(i)] == null;
        }

        public bool NextResult()
        {
            return ReadIndex + 1 <= Rows?.Count;
        }

        public bool Read()
        {
            if (!(Rows?.Count > ReadIndex))
            {
                return false;
            }

            CurrentRow = Rows[ReadIndex];
            ReadIndex++;
            return true;
        }

        public void ClearData()
        {
            ReadIndex = 0;
            CurrentRow = null;
            Rows.Clear();
        }

        #region IDisposable Support
        private bool _disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue)
            {
                return;
            }

            if (disposing)
            {
                Rows.Clear();
                Rows = null;
            }

            _disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Close()
        {
            Dispose();
        }
        #endregion
    }
}
