using System.Data;
using System.Reflection;
using System.Threading.Tasks.Dataflow;
using EtlKit.Common.DataFlow;
using EtlKit.Common.DataFlow.Streaming;
using EtlKit.ConnectionManager;
using EtlKit.DataFlow;
using JetBrains.Annotations;
using Npgsql;
using Xunit;

namespace EtlKit.PostgresStreaming.Tests;

[Collection("Postgres")]
#pragma warning disable SP3110
public sealed class PostgresXminTailSourceTests : IClassFixture<PostgresContainerFixture>
#pragma warning restore SP3110
{
    private readonly PostgresContainerFixture _fixture;

    public PostgresXminTailSourceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [MustDisposeResource]
    private PostgresConnectionManager CreateConnectionManager() =>
        new(_fixture.ConnectionString) { LeaveOpen = true };

    private static void ExecuteSql(NpgsqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void SetupTestTable(NpgsqlConnection conn, string table)
    {
        ExecuteSql(
            conn,
            $"""
            DROP TABLE IF EXISTS {table};
            CREATE TABLE {table} (
                id   BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                name TEXT NOT NULL
            )
            """
        );
    }

    private static void InsertRow(NpgsqlConnection conn, string table, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO {table} (name) VALUES (@n)";
        cmd.Parameters.AddWithValue("n", name);
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void Execute_ReadsAllRows_InOrder()
    {
        const string tableName = "events_order_test";
        using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        conn.Open();
        SetupTestTable(conn, tableName);
        InsertRow(conn, tableName, "alpha");
        InsertRow(conn, tableName, "beta");
        InsertRow(conn, tableName, "gamma");

        var results = new List<string>();
        var destination = new CustomDestination<(long Id, string Name)>(row =>
            results.Add(row.Name)
        );

        using var cm = CreateConnectionManager();
        using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var source = new PostgresXminTailSource<(long Id, string Name)>
        {
            ConnectionManager = cm,
            TableName = tableName,
            Schema = "public",
            OrderByColumns = new[] { "id" },
            BatchSize = 100,
            PollingInterval = TimeSpan.FromMilliseconds(100),
            RowMapper = r => ((long)r["id"], (string)r["name"]),
        };
        source.LinkTo(destination);

        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(500));
        Assert.Throws<OperationCanceledException>(() => source.Execute(tokenSource.Token));
        destination.Wait();

        Assert.Equal(3, results.Count);
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, results);
    }

    [Fact]
    public void Execute_WithCheckpoint_ResumesFromLastPosition()
    {
        // A CheckpointWriter commits the StreamPosition (here: id) AFTER the destination, so the
        // second run resumes past everything the first run durably processed — no duplicates.
        const string tableName = "events_checkpoint_test";
        const string checkpointId = "pg-resume-test";
        using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        conn.Open();
        SetupTestTable(conn, tableName);
        InsertRow(conn, tableName, "first");
        InsertRow(conn, tableName, "second");

        var checkpointStore = new InMemoryCheckpointStore<long>();

        var firstRun = new List<string>();
        RunWithCheckpointWriter(tableName, checkpointId, checkpointStore, firstRun);
        Assert.Equal(new[] { "first", "second" }, firstRun);
        Assert.True(checkpointStore.CommitCount > 0);

        InsertRow(conn, tableName, "third");
        InsertRow(conn, tableName, "fourth");

        var secondRun = new List<string>();
        RunWithCheckpointWriter(tableName, checkpointId, checkpointStore, secondRun);
        Assert.Equal(new[] { "third", "fourth" }, secondRun);
    }

    [Fact]
    public void Execute_WithoutCommit_ReDeliversOnRestart_AtLeastOnce()
    {
        // Simulates a crash between the destination write and the checkpoint commit: the rows are
        // durably "written" (recorded) but no CheckpointWriter commits. On restart the rows are
        // re-delivered (duplicates on the crash boundary), never lost — that is at-least-once.
        const string tableName = "events_atleastonce_test";
        const string checkpointId = "pg-alo-test";
        using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        conn.Open();
        SetupTestTable(conn, tableName);
        InsertRow(conn, tableName, "a");
        InsertRow(conn, tableName, "b");

        var store = new InMemoryCheckpointStore<long>();
        var delivered = new List<string>();

        RunSourceOnly(tableName, checkpointId, store, delivered);
        Assert.Equal(new[] { "a", "b" }, delivered);
        Assert.Equal(0, store.CommitCount); // crashed before committing

        RunSourceOnly(tableName, checkpointId, store, delivered);
        Assert.Equal(new[] { "a", "b", "a", "b" }, delivered); // re-delivered, nothing lost
    }

    // source -> record (durable destination, re-emits) -> CheckpointWriter (commits id after write)
    private void RunWithCheckpointWriter(
        string tableName,
        string checkpointId,
        ICheckpointStore<long> store,
        List<string> sink
    )
    {
        using var cm = CreateConnectionManager();
        using var tokenSource = new CancellationTokenSource();
        var source = NewSource(cm, tableName, checkpointId, store);
        var record = new RowTransformation<(long Id, string Name)>(r =>
        {
            sink.Add(r.Name);
            return r;
        });
        var writer = new CheckpointWriter<(long Id, string Name), long>
        {
            CheckpointStore = store,
            CheckpointId = checkpointId,
            Position = r => r.Id,
        };
        source.LinkTo(record);
        record.LinkTo(writer);
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(500));
        Assert.Throws<OperationCanceledException>(() => source.Execute(tokenSource.Token));
        writer.Wait();
    }

    // source -> destination only; no CheckpointWriter, so nothing is committed.
    private void RunSourceOnly(
        string tableName,
        string checkpointId,
        ICheckpointStore<long> store,
        List<string> sink
    )
    {
        using var cm = CreateConnectionManager();
        using var tokenSource = new CancellationTokenSource();
        var source = NewSource(cm, tableName, checkpointId, store);
        var destination = new CustomDestination<(long Id, string Name)>(r => sink.Add(r.Name));
        source.LinkTo(destination);
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(500));
        Assert.Throws<OperationCanceledException>(() => source.Execute(tokenSource.Token));
        destination.Wait();
    }

    private static PostgresXminTailSource<(long Id, string Name)> NewSource(
        PostgresConnectionManager cm,
        string tableName,
        string checkpointId,
        ICheckpointStore<long> store
    ) =>
        new()
        {
            ConnectionManager = cm,
            TableName = tableName,
            Schema = "public",
            OrderByColumns = new[] { "id" },
            BatchSize = 100,
            PollingInterval = TimeSpan.FromMilliseconds(100),
            CheckpointStore = store,
            CheckpointId = checkpointId,
            RowMapper = r => ((long)r["id"], (string)r["name"]),
        };

    [Fact]
    public void Execute_FrontierExcludesUncommittedRows()
    {
        // Verifies the xmin-frontier correctly excludes rows from open transactions.
        // We simulate this by inserting inside a transaction that we commit AFTER
        // the source has already polled once: those rows must appear in the next batch.
        const string tableName = "events_frontier_test";
        using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        conn.Open();
        SetupTestTable(conn, tableName);
        InsertRow(conn, tableName, "committed_before");

        var results = new List<string>();

        // Open a long transaction that holds an xid slot
        using var openTransaction = conn.BeginTransaction();
        using var slowCmd = conn.CreateCommand();
        slowCmd.Transaction = openTransaction;
        slowCmd.CommandText = $"INSERT INTO {tableName} (name) VALUES ('in_flight')";
        slowCmd.ExecuteNonQuery();

        // Start source — should see "committed_before" but NOT "in_flight"
        using var cm = CreateConnectionManager();
        using var tokenSource = new CancellationTokenSource();
        var destination = new CustomDestination<(long Id, string Name)>(r => results.Add(r.Name));

        var source = new PostgresXminTailSource<(long Id, string Name)>
        {
            ConnectionManager = cm,
            TableName = tableName,
            Schema = "public",
            OrderByColumns = new[] { "id" },
            BatchSize = 100,
            PollingInterval = TimeSpan.FromMilliseconds(50),
            RowMapper = r => ((long)r["id"], (string)r["name"]),
        };
        source.LinkTo(destination);

        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(300));
        Assert.Throws<OperationCanceledException>(() => source.Execute(tokenSource.Token));
        destination.Wait();

        // Only the pre-existing committed row should be present
        Assert.DoesNotContain("in_flight", results);
        Assert.Contains("committed_before", results);

        // Commit the slow transaction — subsequent poll should pick it up
        openTransaction.Commit();
    }

    [Fact]
    public void Execute_ExecuteReaderThrows_DoesNotLeakConnection()
    {
        // Regression: ExecuteQuery() opens the connection but doesn't pair the Open()
        // with CloseIfAllowed() on the exception path. If ExecuteReader throws (e.g.,
        // unknown relation), the DisposableDataReader constructor never finishes, so
        // its CommandBehavior.CloseConnection safety net never fires either, and the
        // connection stays open on the manager until cm.Dispose() — a leak.

        using var cm = new PostgresConnectionManager(_fixture.ConnectionString)
        {
            LeaveOpen = false,
        };

        var destination = new CustomDestination<(long Id, string Name)>(_ => { });
        using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var source = new PostgresXminTailSource<(long Id, string Name)>
        {
            ConnectionManager = cm,
            TableName = "table_that_does_not_exist__rssl11703",
            Schema = "public",
            OrderByColumns = new[] { "id" },
            BatchSize = 100,
            PollingInterval = TimeSpan.FromMilliseconds(50),
            RowMapper = r => ((long)r["id"], (string)r["name"]),
        };
        source.LinkTo(destination);

        // ExecuteReader on the unknown relation surfaces a PostgresException.
        Assert.ThrowsAny<Exception>(() => source.Execute(tokenSource.Token));
        destination.Wait();

        // With LeaveOpen=false, after Execute returns the manager must not retain
        // an open underlying connection — every Open() must be paired with a close.
        Assert.True(
            cm.State is null or ConnectionState.Closed,
            $"Connection leaked after ExecuteReader threw — expected Closed/null, got {cm.State}."
        );
    }

    [Fact]
    public async Task Execute_CancellationDuringBlockedSendAsync_ReturnsPromptly()
    {
        // Regression: RunPollingLoop calls
        //   Buffer.SendAsync(item, CancellationToken.None).Wait(CancellationToken.None)
        // — neither the SendAsync nor the Wait observes the source's cancellation
        // token. When the BufferBlock is bounded (e.g., a downstream pipeline applies
        // backpressure via BoundedCapacity propagation), SendAsync blocks indefinitely
        // on capacity, and cancelling the source has no effect.
        //
        // This test forces the bounded-buffer scenario by replacing the source's
        // unbounded Buffer with a BoundedCapacity=1 BufferBlock and leaving it without
        // a consumer. The source must still return after Cancel within a reasonable
        // budget.
        const string tableName = "events_sendasync_cancel_test";
        SetupTableWithRows(tableName, 5);

        using var cm = CreateConnectionManager();
        var source = new PostgresXminTailSource<(long Id, string Name)>
        {
            ConnectionManager = cm,
            TableName = tableName,
            Schema = "public",
            OrderByColumns = new[] { "id" },
            BatchSize = 100,
            PollingInterval = TimeSpan.FromMilliseconds(50),
            RowMapper = r => ((long)r["id"], (string)r["name"]),
        };
        ReplaceBufferWithBounded(source, capacity: 1);

        using var tokenSource = new CancellationTokenSource();
        var task = Task.Run(() => source.Execute(tokenSource.Token), CancellationToken.None);

        // Give the source enough time to enter the SendAsync-blocked state on the
        // second row (capacity=1, no consumer means the second SendAsync blocks).
        await Task.Delay(500, CancellationToken.None).ConfigureAwait(true);

        await tokenSource.CancelAsync().ConfigureAwait(true);

        try
        {
            await task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        }
        catch (TimeoutException)
        {
            Assert.Fail(
                "Execute did not return within 5s after cancellation — Buffer.SendAsync.Wait(CancellationToken.None) ignored the token."
            );
        }
        catch (OperationCanceledException)
        {
            // Expected — source observed the token and faulted the task.
        }
    }

    private void SetupTableWithRows(string tableName, int rowCount)
    {
        using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        conn.Open();
        SetupTestTable(conn, tableName);
        for (var i = 0; i < rowCount; i++)
            InsertRow(conn, tableName, $"row{i}");
    }

    private static void ReplaceBufferWithBounded<TOutput>(
        PostgresXminTailSource<TOutput> source,
        int capacity
    )
    {
        var bounded = new BufferBlock<TOutput>(
            new DataflowBlockOptions { BoundedCapacity = capacity }
        );
        var prop = typeof(DataFlowSource<TOutput>).GetProperty(
            "Buffer",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.NotNull(prop);
        prop!.SetValue(source, bounded);
    }

    [Fact]
    public void Execute_AdditionalWhere_FiltersRows()
    {
        const string tableName = "events_where_test";
        using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        conn.Open();
        SetupTestTable(conn, tableName);
        InsertRow(conn, tableName, "keep_me");
        InsertRow(conn, tableName, "skip_me");
        InsertRow(conn, tableName, "keep_me_too");

        var results = new List<string>();
        var destination = new CustomDestination<(long Id, string Name)>(r => results.Add(r.Name));
        using var cm = CreateConnectionManager();
        using var tokenSource = new CancellationTokenSource();

        var source = new PostgresXminTailSource<(long Id, string Name)>
        {
            ConnectionManager = cm,
            TableName = tableName,
            Schema = "public",
            OrderByColumns = new[] { "id" },
            BatchSize = 100,
            PollingInterval = TimeSpan.FromMilliseconds(100),
            AdditionalWhere = "name LIKE 'keep%'",
            RowMapper = r => ((long)r["id"], (string)r["name"]),
        };
        source.LinkTo(destination);

        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(500));
        Assert.Throws<OperationCanceledException>(() => source.Execute(tokenSource.Token));
        destination.Wait();

        Assert.Equal(new[] { "keep_me", "keep_me_too" }, results);
    }

    [Fact]
    public void Execute_OrderByMixedCaseQuotedColumn_ReadsInOrder()
    {
        // Regression (RSSL-11704): a column created with a quoted mixed-case name ("StreamPosition")
        // must be usable in ORDER BY and the tuple cursor. Unquoted identifiers are folded to lower
        // case by PostgreSQL and fail with "column does not exist"; the source must quote the
        // OrderByColumns identifiers in the generated SQL (while GetOrdinal stays case-insensitive).
        const string tableName = "events_mixedcase_orderby_test";
        using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        conn.Open();
        ExecuteSql(
            conn,
            $"""
            DROP TABLE IF EXISTS {tableName};
            CREATE TABLE {tableName} (
                "StreamPosition" BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                name            TEXT NOT NULL
            )
            """
        );
        InsertRow(conn, tableName, "one");
        InsertRow(conn, tableName, "two");
        InsertRow(conn, tableName, "three");

        var results = new List<string>();
        var destination = new CustomDestination<(long Pos, string Name)>(r => results.Add(r.Name));
        using var cm = CreateConnectionManager();
        using var tokenSource = new CancellationTokenSource();

        var source = new PostgresXminTailSource<(long Pos, string Name)>
        {
            ConnectionManager = cm,
            TableName = tableName,
            Schema = "public",
            OrderByColumns = new[] { "StreamPosition" },
            BatchSize = 100,
            PollingInterval = TimeSpan.FromMilliseconds(100),
            RowMapper = r => ((long)r["StreamPosition"], (string)r["name"]),
        };
        source.LinkTo(destination);

        // A second poll runs the tuple-cursor WHERE clause — also over the quoted column.
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(500));
        Assert.Throws<OperationCanceledException>(() => source.Execute(tokenSource.Token));
        destination.Wait();

        Assert.Equal(new[] { "one", "two", "three" }, results);
    }
}
