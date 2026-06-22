#nullable enable
using System.Threading;
using EtlKit.Common.DataFlow;
using EtlKit.Common.DataFlow.Streaming;
using EtlKit.ConnectionManager;
using EtlKit.DataFlow;
using Xunit;

namespace EtlKit.Common.Tests;

/// <summary>
/// Tests for <see cref="DbCheckpointStore{TPosition}"/> using an in-memory SQLite database.
/// Each test class instance gets a fresh database; SQLiteConnectionManager.LeaveOpen keeps
/// the single connection alive so the in-memory database persists between store calls.
/// </summary>
public sealed class DbCheckpointStoreTests : IDisposable
{
    private const string DefaultTable = "Checkpoints";

    private readonly SQLiteConnectionManager _cm;

    public DbCheckpointStoreTests()
    {
        _cm = new SQLiteConnectionManager("Data Source=:memory:") { LeaveOpen = true };
        _cm.Open();
        CreateTable(DefaultTable, "CheckpointId", "Position");
    }

    public void Dispose() => _cm.Dispose();

    // ── helpers ──────────────────────────────────────────────────────────────

    private void CreateTable(string table, string keyCol, string valueCol) =>
        _cm.ExecuteNonQuery(
            $"""
            CREATE TABLE "{table}" (
                "{keyCol}" TEXT NOT NULL PRIMARY KEY,
                "{valueCol}" TEXT
            )
            """
        );

    private DbCheckpointStore<long> LongStore(
        string? table = null,
        string? keyCol = null,
        string? valueCol = null
    )
    {
        var store = new DbCheckpointStore<long>(_cm, table ?? DefaultTable);
        if (keyCol != null)
            store.KeyColumn = keyCol;
        if (valueCol != null)
            store.PositionColumn = valueCol;
        return store;
    }

    private DbCheckpointStore<string> StringStore() => new(_cm, DefaultTable);

    private long RowCount(string table = DefaultTable) =>
        (long)_cm.ExecuteScalar($"SELECT COUNT(*) FROM \"{table}\"")!;

    // ── LoadAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_WhenTableIsEmpty_ReturnsFalseAndDefault()
    {
        var (found, pos) = await LongStore().LoadAsync("cp1", CancellationToken.None);

        Assert.False(found);
        Assert.Equal(0L, pos);
    }

    [Fact]
    public async Task LoadAsync_AfterCommit_ReturnsTrueAndStoredPosition()
    {
        var store = LongStore();
        await store.CommitAsync("cp1", 42L, CancellationToken.None);

        var (found, pos) = await store.LoadAsync("cp1", CancellationToken.None);

        Assert.True(found);
        Assert.Equal(42L, pos);
    }

    [Fact]
    public async Task LoadAsync_UnknownCheckpointId_ReturnsFalse()
    {
        var store = LongStore();
        await store.CommitAsync("other", 99L, CancellationToken.None);

        var (found, _) = await store.LoadAsync("cp1", CancellationToken.None);

        Assert.False(found);
    }

    // ── CommitAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CommitAsync_FirstWrite_InsertsOneRow()
    {
        await LongStore().CommitAsync("cp1", 1L, CancellationToken.None);

        Assert.Equal(1L, RowCount());
    }

    [Fact]
    public async Task CommitAsync_SecondWrite_UpdatesInPlaceWithoutAddingRows()
    {
        var store = LongStore();
        await store.CommitAsync("cp1", 1L, CancellationToken.None);
        await store.CommitAsync("cp1", 2L, CancellationToken.None);

        Assert.Equal(1L, RowCount()); // still exactly one row — no duplicate insert
        var (_, pos) = await store.LoadAsync("cp1", CancellationToken.None);
        Assert.Equal(2L, pos);
    }

    [Fact]
    public async Task CommitAsync_MultipleWrites_LastPositionWins()
    {
        var store = LongStore();
        await store.CommitAsync("cp1", 10L, CancellationToken.None);
        await store.CommitAsync("cp1", 20L, CancellationToken.None);
        await store.CommitAsync("cp1", 30L, CancellationToken.None);

        var (found, pos) = await store.LoadAsync("cp1", CancellationToken.None);
        Assert.True(found);
        Assert.Equal(30L, pos);
    }

    // ── Multiple checkpoint IDs ──────────────────────────────────────────────

    [Fact]
    public async Task CommitAsync_TwoCheckpointIds_StoredIndependently()
    {
        var store = LongStore();
        await store.CommitAsync("consumer-a", 100L, CancellationToken.None);
        await store.CommitAsync("consumer-b", 200L, CancellationToken.None);

        var (_, posA) = await store.LoadAsync("consumer-a", CancellationToken.None);
        var (_, posB) = await store.LoadAsync("consumer-b", CancellationToken.None);

        Assert.Equal(100L, posA);
        Assert.Equal(200L, posB);
        Assert.Equal(2L, RowCount());
    }

    // ── Position types ───────────────────────────────────────────────────────

    [Fact]
    public async Task WithStringPosition_CommitAndLoad_RoundTrips()
    {
        var store = StringStore();
        await store.CommitAsync("cp1", "resume-token-xyz", CancellationToken.None);

        var (found, pos) = await store.LoadAsync("cp1", CancellationToken.None);

        Assert.True(found);
        Assert.Equal("resume-token-xyz", pos);
    }

    [Fact]
    public async Task WithStringPosition_SecondWrite_UpdatesPosition()
    {
        var store = StringStore();
        await store.CommitAsync("cp1", "token-1", CancellationToken.None);
        await store.CommitAsync("cp1", "token-2", CancellationToken.None);

        var (_, pos) = await store.LoadAsync("cp1", CancellationToken.None);

        Assert.Equal("token-2", pos);
        Assert.Equal(1L, RowCount());
    }

    // ── Custom columns ───────────────────────────────────────────────────────

    [Fact]
    public async Task WithCustomColumns_CommitAndLoad_Works()
    {
        const string customTable = "CustomCheckpoints";
        CreateTable(customTable, "StreamKey", "Offset");

        var store = LongStore(table: customTable, keyCol: "StreamKey", valueCol: "Offset");
        await store.CommitAsync("stream1", 55L, CancellationToken.None);

        var (found, pos) = await store.LoadAsync("stream1", CancellationToken.None);

        Assert.True(found);
        Assert.Equal(55L, pos);
    }

    // ── CheckpointWriter integration ─────────────────────────────────────────

    [Fact]
    public async Task CheckpointWriter_WithDbStore_CommitsHighestPositionToDatabase()
    {
        var store = LongStore();
        var writer = new CheckpointWriter<(long Id, string Name), long>
        {
            CheckpointStore = store,
            CheckpointId = "pipeline-1",
            Position = r => r.Id,
        };
        var source = new MemorySource<(long Id, string Name)>
        {
            Data = [(1L, "alpha"), (2L, "beta"), (3L, "gamma")],
        };
        source.LinkTo(writer);
        await source.ExecuteAsync();
        await writer.Completion.ConfigureAwait(true);

        var (found, pos) = await store.LoadAsync("pipeline-1", CancellationToken.None);

        Assert.True(found);
        Assert.Equal(3L, pos);
    }
}
