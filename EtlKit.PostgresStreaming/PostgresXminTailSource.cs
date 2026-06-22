using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using EtlKit.Primitives;

using EtlKit.Common.ControlFlow;
using EtlKit.Common.DataFlow;
using EtlKit.Common.DataFlow.Streaming;

using JetBrains.Annotations;

namespace EtlKit.DataFlow;

/// <summary>
/// Tail-reads a PostgreSQL table using xmin-frontier polling to avoid race conditions
/// with concurrent in-flight transactions.
/// </summary>
/// <remarks>
/// Uses <c>pg_snapshot_xmin(pg_current_snapshot())</c> as a read-safe frontier so that
/// rows inserted by transactions that committed after the snapshot boundary are never missed.
/// Set <see cref="GenericTask.ConnectionManager"/> before calling <see cref="Execute"/>.
/// </remarks>
[PublicAPI]
public class PostgresXminTailSource<TOutput> : DataFlowSource<TOutput>
{
    /// <summary>Table to poll. Must expose the system column <c>xmin</c>.</summary>
    public string TableName { get; set; } = null!;

    /// <summary>Schema that contains <see cref="TableName"/>. Defaults to <c>public</c>.</summary>
    public string Schema { get; set; } = "public";

    /// <summary>
    /// Columns used for ordering and tuple-cursor pagination.
    /// Must be monotone and uniquely identify the row's position in the stream.
    /// </summary>
    public string[] OrderByColumns { get; set; } = Array.Empty<string>();

    /// <summary>Optional extra WHERE predicate appended with AND.</summary>
    public string? AdditionalWhere { get; set; }

    /// <summary>Rows per polling round. Defaults to 500.</summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>Pause between polling rounds when no rows are found. Defaults to 1 second.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Loads the resume position across restarts (load-only — the source never commits).
    /// If <c>null</c>, the source always starts from the beginning of the table.
    /// The durable position is advanced downstream by a <c>CheckpointWriter</c> after the
    /// destination has persisted the records (at-least-once), never at emit time.
    /// </summary>
    public ICheckpointStore<long>? CheckpointStore { get; set; }

    /// <summary>
    /// Identifies this consumer's checkpoint in <see cref="CheckpointStore"/>. The same stream
    /// can be tailed by several consumers, each with its own id. Must match the
    /// <c>CheckpointId</c> of the paired <c>CheckpointWriter</c>.
    /// </summary>
    public string CheckpointId { get; set; } = null!;

    /// <summary>Maps a data record row to the output type. Required.</summary>
    public Func<IDataRecord, TOutput> RowMapper { get; set; } = null!;

    /// <inheritdoc/>
    public override void Execute(CancellationToken cancellationToken)
    {
        LogStart();
        try
        {
            RunPollingLoop(cancellationToken);
        }
        finally
        {
            Buffer.Complete();
            LogFinish();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void RunPollingLoop(CancellationToken ct)
    {
        var cursor = LoadCursor(ct);

        while (!ct.IsCancellationRequested)
        {
            var frontier = GetFrontier();
            var rowsRead = 0;
            object?[]? lastCursorValues = null;

            using var reader = ExecuteQuery(frontier, cursor);
            while (reader.Read())
            {
                ct.ThrowIfCancellationRequested();
                var output = RowMapper(reader);
                // Propagate the source's cancellation token into SendAsync so that
                // backpressure from a bounded downstream buffer doesn't trap the
                // polling loop after Cancel() — see RSSL-11703 regression test
                // Execute_CancellationDuringBlockedSendAsync_ReturnsPromptly.
                Buffer.SendAsync(output, ct).GetAwaiter().GetResult();
                lastCursorValues = ReadCursorValues(reader);
                rowsRead++;
                LogProgress();
            }

            if (rowsRead > 0 && lastCursorValues != null)
            {
                // Advance the ephemeral in-memory read cursor for the next batch. The durable
                // checkpoint is NOT written here — a downstream CheckpointWriter commits it after
                // the destination persists (at-least-once). See ICheckpointStore.
                cursor = lastCursorValues;
            }
            else
            {
                Task.Delay(PollingInterval, ct).Wait(ct);
            }
        }
    }

    private long GetFrontier()
    {
        ConnectionManager.Open();
        try
        {
            var result = ConnectionManager.ExecuteScalar(
                "SELECT pg_snapshot_xmin(pg_current_snapshot())::text::bigint"
            );
            return Convert.ToInt64(result);
        }
        finally
        {
            ConnectionManager.CloseIfAllowed();
        }
    }

    private IDataReader ExecuteQuery(long frontier, object?[]? cursor)
    {
        var sql = BuildQuery(frontier, cursor, out var parameters);
        ConnectionManager.Open();
        return ConnectionManager.ExecuteReader(sql, parameters);
    }

    private string BuildQuery(
        long frontier,
        object?[]? cursor,
        out List<IQueryParameter> parameters
    )
    {
        parameters = new List<IQueryParameter>();
        var query = new StringBuilder();

        query.Append("SELECT *, xmin::text::bigint AS _xmin_val FROM ");
        query.Append(QuotedTableRef());
        query.Append(" WHERE xmin::text::bigint < @_frontier");
        parameters.Add(new QueryParameter("_frontier", frontier));

        if (cursor != null && OrderByColumns.Length > 0)
        {
            AppendTupleCursor(query, parameters, cursor);
        }

        if (!string.IsNullOrWhiteSpace(AdditionalWhere))
        {
            query.Append(" AND (");
            query.Append(AdditionalWhere);
            query.Append(")");
        }

        if (OrderByColumns.Length > 0)
        {
            query.Append(" ORDER BY ");
            AppendQuotedColumnList(query, OrderByColumns);
        }

        query.Append(" LIMIT @_batchSize");
        parameters.Add(new QueryParameter("_batchSize", BatchSize));

        return query.ToString();
    }

    private void AppendTupleCursor(
        StringBuilder query,
        List<IQueryParameter> parameters,
        object?[] values
    )
    {
        query.Append(" AND (");
        AppendQuotedColumnList(query, OrderByColumns);
        query.Append(") > (");

        for (var i = 0; i < OrderByColumns.Length; i++)
        {
            if (i > 0)
                query.Append(", ");
            var paramName = "_cursor" + i;
            query.Append('@');
            query.Append(paramName);
            parameters.Add(new QueryParameter(paramName, values[i] ?? DBNull.Value));
        }

        query.Append(")");
    }

    // Quotes each ORDER BY / tuple-cursor identifier so mixed-case columns created as "MyCol"
    // are matched exactly. Unquoted identifiers are folded to lower case by PostgreSQL and would
    // not match a quoted mixed-case column. Bare column names are still used for IDataRecord
    // lookups (GetOrdinal is case-insensitive), so only the generated SQL is quoted here.
    private static void AppendQuotedColumnList(StringBuilder query, string[] columns)
    {
        for (var i = 0; i < columns.Length; i++)
        {
            if (i > 0)
                query.Append(", ");
            query.Append('"');
            query.Append(columns[i].Replace("\"", "\"\""));
            query.Append('"');
        }
    }

    private string QuotedTableRef()
    {
        var schema = string.IsNullOrWhiteSpace(Schema) ? "public" : Schema;
        return $"\"{schema}\".\"{TableName}\"";
    }

    private object?[] ReadCursorValues(IDataRecord reader)
    {
        var values = new object?[OrderByColumns.Length];
        for (var i = 0; i < OrderByColumns.Length; i++)
        {
            var ordinal = reader.GetOrdinal(OrderByColumns[i]);
            values[i] = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
        }
        return values;
    }

    // Cold-start resume: load the durable StreamPosition committed by the CheckpointWriter and
    // seed the single-column tuple cursor with it. The source itself never commits.
    private object?[]? LoadCursor(CancellationToken ct)
    {
        if (CheckpointStore == null)
            return null;
        var (found, position) = CheckpointStore
            .LoadAsync(CheckpointId, ct)
            .GetAwaiter()
            .GetResult();
        return found ? new object?[] { position } : null;
    }

    private sealed class QueryParameter : IQueryParameter
    {
        public string Name { get; }
        public string Type { get; } = string.Empty;
        public object Value { get; }
        public DbType DBType { get; }

        public QueryParameter(string name, object value)
        {
            Name = name;
            Value = value;
            DBType = MapDbType(value);
        }

        private static DbType MapDbType(object value) =>
            value switch
            {
                long or int or short => DbType.Int64,
                DateTime => DbType.DateTime2,
                DateTimeOffset => DbType.DateTimeOffset,
                string => DbType.String,
                Guid => DbType.Guid,
                _ => DbType.Object,
            };
    }
}
