#nullable enable
using System;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using EtlKit.Primitives;
using JetBrains.Annotations;

namespace EtlKit.Common.DataFlow.Streaming;

/// <summary>
/// Database-backed <see cref="ICheckpointStore{TPosition}"/>. Stores one row per
/// <c>checkpointId</c> in a table of your choosing via an ETLBox <see cref="IConnectionManager"/>.
/// </summary>
/// <remarks>
/// <para>
/// The table needs a key column (defaults to <c>CheckpointId</c>, text) and a position column
/// (defaults to <c>Position</c>, of a type matching <typeparamref name="TPosition"/> — e.g.
/// <c>bigint</c> for <see cref="long"/>, <c>text</c> for <see cref="string"/>); the key column
/// should be unique. The position is stored natively for primitive types — no string serialization
/// in the contract. Identifiers are quoted with the connection manager's dialect quotes;
/// <see cref="TableName"/> is emitted verbatim, so pass a pre-qualified/quoted name if it lives in a
/// non-default schema.
/// </para>
/// <para>
/// Writes use a portable update-then-insert rather than a dialect-specific UPSERT. This is safe
/// because commits for a single <c>checkpointId</c> are serialized by a single committer (one
/// <c>CheckpointWriter</c> per checkpoint).
/// </para>
/// <para>
/// Calls execute synchronously on the supplied connection manager (ETLBox is sync-first) and are
/// returned as completed tasks; each call opens and closes the connection.
/// </para>
/// </remarks>
[PublicAPI]
public class DbCheckpointStore<TPosition> : ICheckpointStore<TPosition>
    where TPosition : IComparable<TPosition>
{
    /// <summary>Connection manager used to reach the checkpoint table.</summary>
    public IConnectionManager ConnectionManager { get; set; } = null!;

    /// <summary>Table that holds the checkpoints. Emitted verbatim (pre-qualify if needed).</summary>
    public string TableName { get; set; } = null!;

    /// <summary>Column holding the checkpoint id (key). Defaults to <c>CheckpointId</c>.</summary>
    public string KeyColumn { get; set; } = "CheckpointId";

    /// <summary>Column holding the committed position (value). Defaults to <c>Position</c>.</summary>
    public string PositionColumn { get; set; } = "Position";

    /// <summary>Creates an unconfigured store; set the properties before use.</summary>
    public DbCheckpointStore() { }

    /// <summary>Creates a store bound to a connection manager and checkpoint table.</summary>
    /// <param name="connectionManager">Connection manager used to reach the table.</param>
    /// <param name="tableName">Table that holds the checkpoints (emitted verbatim).</param>
    public DbCheckpointStore(IConnectionManager connectionManager, string tableName)
    {
        ConnectionManager = connectionManager;
        TableName = tableName;
    }

    private string Q(string identifier) => ConnectionManager.QB + identifier + ConnectionManager.QE;

    /// <inheritdoc/>
    public Task<(bool Found, TPosition Position)> LoadAsync(
        string checkpointId,
        CancellationToken ct
    )
    {
        var sql =
            $"SELECT {Q(PositionColumn)} FROM {TableName} WHERE {Q(KeyColumn)} = @_checkpointId";
        ConnectionManager.Open();
        try
        {
            var result = ConnectionManager.ExecuteScalar(sql, new[] { KeyParam(checkpointId) });
            if (result is null or DBNull)
                return Task.FromResult((false, default(TPosition)!));
            var position = (TPosition)
                Convert.ChangeType(result, typeof(TPosition), CultureInfo.InvariantCulture);
            return Task.FromResult((true, position));
        }
        finally
        {
            ConnectionManager.CloseIfAllowed();
        }
    }

    /// <inheritdoc/>
    public Task CommitAsync(string checkpointId, TPosition position, CancellationToken ct)
    {
        var parameters = new[] { KeyParam(checkpointId), PositionParam(position) };

        ConnectionManager.Open();
        try
        {
            var updated = ConnectionManager.ExecuteNonQuery(
                $"UPDATE {TableName} SET {Q(PositionColumn)} = @_position "
                    + $"WHERE {Q(KeyColumn)} = @_checkpointId",
                parameters
            );
            if (updated == 0)
            {
                ConnectionManager.ExecuteNonQuery(
                    $"INSERT INTO {TableName} ({Q(KeyColumn)}, {Q(PositionColumn)}) "
                        + "VALUES (@_checkpointId, @_position)",
                    parameters
                );
            }
            return Task.CompletedTask;
        }
        finally
        {
            ConnectionManager.CloseIfAllowed();
        }
    }

    private static IQueryParameter KeyParam(string checkpointId) =>
        new Parameter("_checkpointId", DbType.String, checkpointId);

    private static IQueryParameter PositionParam(TPosition position) =>
        new Parameter("_position", PositionDbType, position!);

    private static DbType PositionDbType
    {
        get
        {
            if (typeof(TPosition) == typeof(Guid))
                return DbType.Guid;
            return Type.GetTypeCode(typeof(TPosition)) switch
            {
                TypeCode.Int64 => DbType.Int64,
                TypeCode.Int32 => DbType.Int32,
                TypeCode.Int16 => DbType.Int16,
                TypeCode.String => DbType.String,
                TypeCode.DateTime => DbType.DateTime2,
                _ => DbType.Object,
            };
        }
    }

    private sealed class Parameter : IQueryParameter
    {
        public Parameter(string name, DbType dbType, object value)
        {
            Name = name;
            DBType = dbType;
            Value = value;
        }

        public string Name { get; }
        public string Type => DBType.ToString();
        public object Value { get; }
        public DbType DBType { get; }
    }
}
