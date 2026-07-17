using System.Data.Common;
using System.Linq;
using EtlKit.Common;
using EtlKit.Primitives;

namespace EtlKit
{
    /// <summary>
    /// Non-generic <see cref="TableData{T}"/> for rows represented as plain <c>object[]</c> arrays.
    /// </summary>
    [PublicAPI]
    public sealed class TableData : TableData<object[]>
    {
        /// <summary>
        /// Creates an instance for <paramref name="definition"/> with no pre-sized row buffer.
        /// </summary>
        /// <param name="definition">The destination table's structure, used for column mapping.</param>
        public TableData(TableDefinition definition)
            : base(definition) { }

        /// <summary>
        /// Creates an instance for <paramref name="definition"/>, pre-sizing the row buffer for
        /// <paramref name="estimatedBatchSize"/> rows.
        /// </summary>
        /// <param name="definition">The destination table's structure, used for column mapping.</param>
        /// <param name="estimatedBatchSize">Expected number of rows, used to pre-size the internal buffer.</param>
        public TableData(TableDefinition definition, int estimatedBatchSize)
            : base(definition, estimatedBatchSize) { }
    }

    /// <summary>
    /// An in-memory batch of rows of type <typeparamref name="T"/>, exposed as an <see
    /// cref="IDataReader"/> for bulk-insert drivers that consume one (e.g. <c>SqlBulkCopy</c>). See
    /// <see cref="EtlKit.Primitives.ITableData"/> for the shared contract.
    /// </summary>
    /// <typeparam name="T">The row type: <c>object[]</c>, a POCO, or <see cref="System.Dynamic.ExpandoObject"/>.</typeparam>
    [PublicAPI]
    public class TableData<T> : ITableData
    {
        /// <inheritdoc />
        /// <exception cref="EtlKitException">No <see cref="TableDefinition"/> was provided at construction.</exception>
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
                (true, false) => Definition.Columns.Where(c =>
                    !c.IsIdentity && DynamicColumnNames.ContainsKey(c.Name)
                ),
                (_, _) => Definition.Columns.Where(c =>
                    !c.IsIdentity && TypeInfo.HasPropertyOrColumnMapping(c.Name)
                ),
            };
            var mapping = new DataColumnMappingCollection();
            mapping.AddRange(
                columns
                    .Select(col => new DataColumnMapping(col.SourceColumn, col.DataSetColumn))
                    .ToArray()
            );
            return mapping;
        }

        /// <inheritdoc cref="EtlKit.Primitives.ITableData.Rows" />
        public List<object[]> Rows { get; private set; }

        /// <summary>
        /// The row last returned by <see cref="Read"/>, or <see langword="null"/> before the first
        /// <see cref="Read"/> call.
        /// </summary>
        public object[] CurrentRow { get; private set; }

        /// <summary>
        /// Maps column name to column position, used to resolve <see cref="GetOrdinal"/> when <c>T</c>
        /// is a dynamic type.
        /// </summary>
        public Dictionary<string, int> DynamicColumnNames { get; set; } = new();
        private int ReadIndex { get; set; }
        private TableDefinition Definition { get; set; }
        private bool HasDefinition => Definition != null;
        private DataFlow.DBTypeInfo TypeInfo { get; set; }
        private int? IDColumnIndex { get; set; }
        private bool HasIDColumnIndex => IDColumnIndex != null;

        /// <summary>
        /// Creates an instance for <paramref name="definition"/> with no pre-sized row buffer.
        /// </summary>
        /// <param name="definition">The destination table's structure, used for column mapping.</param>
        public TableData(TableDefinition definition)
        {
            InitObjects(definition);
        }

        /// <summary>
        /// Creates an instance for <paramref name="definition"/>, pre-sizing the row buffer for
        /// <paramref name="estimatedBatchSize"/> rows.
        /// </summary>
        /// <param name="definition">The destination table's structure, used for column mapping.</param>
        /// <param name="estimatedBatchSize">Expected number of rows, used to pre-size the internal buffer.</param>
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

        /// <summary>Returns the row at the position <see cref="GetOrdinal"/> resolves <paramref name="name"/> to. Not part of <see cref="IDataReader"/>.</summary>
        /// <param name="name">Column name, resolved via <see cref="GetOrdinal"/>.</param>
        public object this[string name] => Rows[GetOrdinal(name)];

        /// <summary>Returns the row at index <paramref name="i"/> in <see cref="Rows"/>. Not part of <see cref="IDataReader"/>.</summary>
        /// <param name="i">Zero-based row index.</param>
        public object this[int i] => Rows[i];

        /// <inheritdoc />
        /// <remarks>Always <c>0</c>; this reader does not support nested result sets.</remarks>
        public int Depth => 0;

        /// <inheritdoc />
        /// <remarks>Returns <see cref="Rows"/>'s count rather than the destination column count.</remarks>
        public int FieldCount => Rows.Count;

        /// <inheritdoc />
        public bool IsClosed => Rows.Count == 0;

        /// <inheritdoc />
        public int RecordsAffected => Rows.Count;

        /// <inheritdoc />
        public bool GetBoolean(int i) => Convert.ToBoolean(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        /// <inheritdoc />
        public byte GetByte(int i) => Convert.ToByte(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        /// <inheritdoc />
        /// <remarks>Not implemented for streamed reads; always returns <c>0</c> without copying into <paramref name="buffer"/>.</remarks>
        public long GetBytes(
            int i,
            long fieldOffset,
            byte[] buffer,
            int bufferoffset,
            int length
        ) => 0;

        /// <inheritdoc />
        public char GetChar(int i) => Convert.ToChar(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        /// <inheritdoc />
        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            var value = Convert.ToString(CurrentRow[ShiftIndexAroundIDColumn(i)]);
            buffer = value.Substring(bufferoffset, length).ToCharArray();
            return buffer.Length;
        }

        /// <inheritdoc />
        public DateTime GetDateTime(int i) =>
            Convert.ToDateTime(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        /// <inheritdoc />
        /// <exception cref="NotImplementedException">Always thrown; nested data readers are not supported.</exception>
        public IDataReader GetData(int i) => throw new NotImplementedException();

        /// <inheritdoc />
        public decimal GetDecimal(int i) =>
            Convert.ToDecimal(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        /// <inheritdoc />
        public double GetDouble(int i) => Convert.ToDouble(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        /// <inheritdoc />
        public float GetFloat(int i) =>
            float.Parse(Convert.ToString(CurrentRow[ShiftIndexAroundIDColumn(i)]));

        /// <inheritdoc />
        public Guid GetGuid(int i) =>
            Guid.Parse(Convert.ToString(CurrentRow[ShiftIndexAroundIDColumn(i)]));

        /// <inheritdoc />
        public short GetInt16(int i) => Convert.ToInt16(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        /// <inheritdoc />
        public int GetInt32(int i) => Convert.ToInt32(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        /// <inheritdoc />
        public long GetInt64(int i) => Convert.ToInt64(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        /// <inheritdoc />
        /// <exception cref="NotImplementedException">Always thrown; column names are not tracked by position.</exception>
        public string GetName(int i) => throw new NotImplementedException();

        /// <inheritdoc />
        /// <exception cref="NotImplementedException">Always thrown; column type names are not tracked by position.</exception>
        public string GetDataTypeName(int i) => throw new NotImplementedException();

        /// <inheritdoc />
        /// <exception cref="NotImplementedException">Always thrown; column CLR types are not tracked by position.</exception>
        public Type GetFieldType(int i) => throw new NotImplementedException();

        /// <inheritdoc />
        public int GetOrdinal(string name) => FindOrdinalInObject(name);

        private int FindOrdinalInObject(string name)
        {
            return TypeInfo?.GetTypeInfoGroup() switch
            {
                Common.DataFlow.TypeInfo.TypeInfoGroup.Array or null =>
                    Definition.Columns.FindIndex(col => col.Name == name),
                Common.DataFlow.TypeInfo.TypeInfoGroup.Dynamic => IncrementIfAfterIdColumn(
                    DynamicColumnNames[name]
                ),
                _ => IncrementIfAfterIdColumn(
                    TypeInfo!.GetIndexByPropertyNameOrColumnMapping(name)
                ),
            };

            int IncrementIfAfterIdColumn(int ix)
            {
                if (HasIDColumnIndex && ix >= IDColumnIndex)
                    ix++;
                return ix;
            }
        }

        /// <inheritdoc />
        /// <exception cref="NotImplementedException">Always thrown; schema metadata is not tracked.</exception>
        public DataTable GetSchemaTable()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public string GetString(int i) => Convert.ToString(CurrentRow[ShiftIndexAroundIDColumn(i)]);

        /// <inheritdoc />
        public object GetValue(int i) =>
            CurrentRow.Length > ShiftIndexAroundIDColumn(i)
                ? CurrentRow[ShiftIndexAroundIDColumn(i)]
                : null;

        private int ShiftIndexAroundIDColumn(int i) =>
            HasIDColumnIndex switch
            {
                false => i,
                _ => i > IDColumnIndex ? i - 1 : i,
            };

        /// <inheritdoc />
        public int GetValues(object[] values)
        {
            values = CurrentRow;
            return values.Length;
        }

        /// <inheritdoc />
        public bool IsDBNull(int i)
        {
            return CurrentRow.Length <= ShiftIndexAroundIDColumn(i)
                || CurrentRow[ShiftIndexAroundIDColumn(i)] == null;
        }

        /// <inheritdoc />
        /// <remarks>Always reports whether at least one more row remains; this reader has a single result set.</remarks>
        public bool NextResult()
        {
            return ReadIndex + 1 <= Rows?.Count;
        }

        /// <inheritdoc />
        /// <remarks>Advances through <see cref="Rows"/> in order, setting <see cref="CurrentRow"/>.</remarks>
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

        /// <summary>
        /// Resets the read position and clears <see cref="Rows"/> and <see cref="CurrentRow"/>, for
        /// reusing this instance with new data.
        /// </summary>
        public void ClearData()
        {
            ReadIndex = 0;
            CurrentRow = null;
            Rows.Clear();
        }

        #region IDisposable Support
        private bool _disposedValue;

        /// <summary>
        /// Clears <see cref="Rows"/> when <paramref name="disposing"/> is <see langword="true"/>; safe
        /// to call more than once.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/> rather than a finalizer.</param>
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

        /// <summary>
        /// Clears <see cref="Rows"/> via <see cref="Dispose(bool)"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public void Close()
        {
            Dispose();
        }
        #endregion
    }
}
