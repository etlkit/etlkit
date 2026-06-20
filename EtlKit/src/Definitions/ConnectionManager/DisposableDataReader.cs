namespace EtlKit.ConnectionManager;

/// <summary>
/// Wrapper for IDbCommand and IDataReader to use combined Dispose
/// </summary>
public sealed class DisposableDataReader : IDataReader
{
    private readonly IDbCommand _command;
    private readonly IDataReader _reader;

    public DisposableDataReader(Func<IDbCommand> createCommand, CommandBehavior? commandBehavior)
    {
        _command = createCommand();
        _reader = commandBehavior.HasValue
            ? _command.ExecuteReader(commandBehavior.Value)
            : _command.ExecuteReader();
    }

    public void Close()
    {
        _reader.Close();
    }

    public DataTable GetSchemaTable()
    {
        return _reader.GetSchemaTable();
    }

    public bool NextResult()
    {
        return _reader.NextResult();
    }

    public bool Read() => _reader.Read();

    public int Depth
    {
        get => _reader.Depth;
    }

    public bool IsClosed
    {
        get => _reader.IsClosed;
    }

    public int RecordsAffected
    {
        get => _reader.RecordsAffected;
    }

    public string GetName(int i)
    {
        return _reader.GetName(i);
    }

    public string GetDataTypeName(int i) => _reader.GetDataTypeName(i);

    public Type GetFieldType(int i) => _reader.GetFieldType(i);

    public object GetValue(int i) => _reader.GetValue(i);

    public int GetValues(object[] values) => _reader.GetValues(values);

    public int GetOrdinal(string name) => _reader.GetOrdinal(name);

    public bool GetBoolean(int i) => _reader.GetBoolean(i);

    public byte GetByte(int i) => _reader.GetByte(i);

    public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) =>
        _reader.GetBytes(i, fieldOffset, buffer, bufferoffset, length);

    public char GetChar(int i) => _reader.GetChar(i);

    public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) =>
        _reader.GetChars(i, fieldoffset, buffer, bufferoffset, length);

    public Guid GetGuid(int i) => _reader.GetGuid(i);

    public short GetInt16(int i) => _reader.GetInt16(i);

    public int GetInt32(int i) => _reader.GetInt32(i);

    public long GetInt64(int i) => _reader.GetInt64(i);

    public float GetFloat(int i) => _reader.GetFloat(i);

    public double GetDouble(int i) => _reader.GetDouble(i);

    public string GetString(int i) => _reader.GetString(i);

    public decimal GetDecimal(int i) => _reader.GetDecimal(i);

    public DateTime GetDateTime(int i) => _reader.GetDateTime(i);

    public IDataReader GetData(int i) => ((IDataRecord)_reader).GetData(i);

    public bool IsDBNull(int i) => _reader.IsDBNull(i);

    public int FieldCount => _reader.FieldCount;

    public object this[int i] => _reader[i];
    public object this[string name]
    {
        get => _reader[name];
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _command?.Dispose();
    }
}
