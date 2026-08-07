using EtlKit.Common.DataFlow;
using EtlKit.Common.DataFlow.Streaming;
using EtlKit.DataFlow;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static EtlKit.MongoDB.Tests.MongoTestHelpers;

namespace EtlKit.MongoDB.Tests;

[Collection("MongoDB")]
public sealed class MongoChangeStreamStartPositionTests
{
    private readonly MongoContainerFixture _fixture;

    public MongoChangeStreamStartPositionTests(MongoContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private IMongoClient CreateClient() => new MongoClient(_fixture.ConnectionString);

    // The headline defect: with no start position the cursor begins wherever Watch() lands, so
    // everything written before that moment is lost silently. Here the write happens while the
    // source is not running at all and its cursor does not exist.
    [Fact]
    public async Task Execute_WithStartAtOperationTime_ReceivesWritesMadeBeforeTheSourceStarted()
    {
        const string collectionName = "cold_start_seed";
        var client = CreateClient();
        var collection = GetCollection(client, collectionName);

        var startAt = MongoChangeStreamPosition.Current(client, DatabaseName);
        await collection.InsertOneAsync(new BsonDocument { { "name", "before_start" } });

        var results = new List<string>();
        var destination = new CustomDestination<string>(name => results.Add(name));

        using var tokenSource = new CancellationTokenSource();
        var source = new MongoChangeStreamSource<string>
        {
            MongoClient = client,
            Database = DatabaseName,
            Collection = collectionName,
            MaxAwaitTime = TimeSpan.FromMilliseconds(200),
            StartAtOperationTime = startAt,
            EventMapper = doc => doc.FullDocument["name"].AsString,
        };
        source.LinkTo(destination);

        var executeTask = Task.Run(() => source.Execute(tokenSource.Token), CancellationToken.None);

        WaitForResults(results, 1, TimeSpan.FromSeconds(15));
        await tokenSource.CancelAsync();

        Assert.Throws<OperationCanceledException>(() => executeTask.GetAwaiter().GetResult());
        destination.Wait();

        Assert.Equal(new[] { "before_start" }, results);
    }

    [Fact]
    public async Task Execute_WithStartAfterSeed_SkipsTheEventAtThatToken()
    {
        const string collectionName = "start_after_seed";
        var client = CreateClient();
        var collection = GetCollection(client, collectionName);

        var startAt = MongoChangeStreamPosition.Current(client, DatabaseName);
        await collection.InsertOneAsync(new BsonDocument { { "name", "first" } });
        await collection.InsertOneAsync(new BsonDocument { { "name", "second" } });

        // First pass: read both events from the snapped mark so we hold a real resume token.
        var seen = new List<(string Name, string Token)>();
        var capture = new CustomDestination<(string Name, string Token)>(e => seen.Add(e));
        using var tokenSource1 = new CancellationTokenSource();
        var source1 = new MongoChangeStreamSource<(string Name, string Token)>
        {
            MongoClient = client,
            Database = DatabaseName,
            Collection = collectionName,
            MaxAwaitTime = TimeSpan.FromMilliseconds(200),
            StartAtOperationTime = startAt,
            EventMapper = doc => (doc.FullDocument["name"].AsString, doc.ResumeToken.ToJson()),
        };
        source1.LinkTo(capture);

        var run1 = Task.Run(() => source1.Execute(tokenSource1.Token), CancellationToken.None);
        WaitForResults(seen, 2, TimeSpan.FromSeconds(15));
        await tokenSource1.CancelAsync();
        Assert.Throws<OperationCanceledException>(() => run1.GetAwaiter().GetResult());
        capture.Wait();
        Assert.Equal(2, seen.Count);

        // Second pass: seeded strictly after the FIRST event, so only "second" may arrive.
        var results = new List<string>();
        var destination = new CustomDestination<string>(name => results.Add(name));
        using var tokenSource2 = new CancellationTokenSource();
        var source2 = new MongoChangeStreamSource<string>
        {
            MongoClient = client,
            Database = DatabaseName,
            Collection = collectionName,
            MaxAwaitTime = TimeSpan.FromMilliseconds(200),
            StartAfter = seen[0].Token,
            EventMapper = doc => doc.FullDocument["name"].AsString,
        };
        source2.LinkTo(destination);

        var run2 = Task.Run(() => source2.Execute(tokenSource2.Token), CancellationToken.None);
        WaitForResults(results, 1, TimeSpan.FromSeconds(15));
        await tokenSource2.CancelAsync();
        Assert.Throws<OperationCanceledException>(() => run2.GetAwaiter().GetResult());
        destination.Wait();

        Assert.Equal(new[] { "second" }, results);
    }

    // The seeds are cold-start only. A restart must resume from committed progress, not replay
    // from a value left behind in configuration.
    [Fact]
    public async Task Execute_WithCheckpointAndStaleSeed_ResumesFromTheCheckpoint()
    {
        const string collectionName = "checkpoint_outranks_seed";
        const string checkpointId = "checkpoint-outranks-seed";
        var client = CreateClient();
        var collection = GetCollection(client, collectionName);
        var store = new InMemoryCheckpointStore<string>();

        var startAt = MongoChangeStreamPosition.Current(client, DatabaseName);
        await collection.InsertOneAsync(new BsonDocument { { "name", "first" } });
        await collection.InsertOneAsync(new BsonDocument { { "name", "second" } });

        // Read "first" only, and commit its token as the checkpoint.
        var seen = new List<(string Name, string Token)>();
        var capture = new CustomDestination<(string Name, string Token)>(e => seen.Add(e));
        using var tokenSource1 = new CancellationTokenSource();
        var source1 = new MongoChangeStreamSource<(string Name, string Token)>
        {
            MongoClient = client,
            Database = DatabaseName,
            Collection = collectionName,
            MaxAwaitTime = TimeSpan.FromMilliseconds(200),
            StartAtOperationTime = startAt,
            EventMapper = doc => (doc.FullDocument["name"].AsString, doc.ResumeToken.ToJson()),
        };
        source1.LinkTo(capture);

        var run1 = Task.Run(() => source1.Execute(tokenSource1.Token), CancellationToken.None);
        WaitForResults(seen, 1, TimeSpan.FromSeconds(15));
        await tokenSource1.CancelAsync();
        Assert.Throws<OperationCanceledException>(() => run1.GetAwaiter().GetResult());
        capture.Wait();
        await store.CommitAsync(checkpointId, seen[0].Token, CancellationToken.None);

        // Restart with BOTH the checkpoint and the now-stale seed. The checkpoint must win: if
        // the seed won, "first" would be replayed.
        var results = new List<string>();
        var destination = new CustomDestination<string>(name => results.Add(name));
        using var tokenSource2 = new CancellationTokenSource();
        var source2 = new MongoChangeStreamSource<string>
        {
            MongoClient = client,
            Database = DatabaseName,
            Collection = collectionName,
            MaxAwaitTime = TimeSpan.FromMilliseconds(200),
            StartAtOperationTime = startAt,
            CheckpointStore = store,
            CheckpointId = checkpointId,
            EventMapper = doc => doc.FullDocument["name"].AsString,
        };
        source2.LinkTo(destination);

        var run2 = Task.Run(() => source2.Execute(tokenSource2.Token), CancellationToken.None);
        WaitForResults(results, 1, TimeSpan.FromSeconds(15));
        await tokenSource2.CancelAsync();
        Assert.Throws<OperationCanceledException>(() => run2.GetAwaiter().GetResult());
        destination.Wait();

        Assert.Equal(new[] { "second" }, results);
    }

    // MongoClient is deliberately left null: reaching the driver at all would throw
    // NullReferenceException, so an InvalidOperationException proves validation ran first.
    [Fact]
    public void Execute_WithBothSeedsSet_ThrowsBeforeTouchingTheDriver()
    {
        var source = new MongoChangeStreamSource<string>
        {
            Database = DatabaseName,
            Collection = "never_watched",
            StartAtOperationTime = DateTimeOffset.UtcNow,
            StartAfter = "{ \"_data\": \"abc\" }",
            EventMapper = doc => doc.FullDocument["name"].AsString,
        };

        var error = Assert.Throws<InvalidOperationException>(
            () => source.Execute(CancellationToken.None)
        );
        Assert.Contains("StartAfter", error.Message, StringComparison.Ordinal);
        Assert.Contains("StartAtOperationTime", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_WithStartAtOperationTimeOutOfRange_ThrowsBeforeTouchingTheDriver()
    {
        var source = new MongoChangeStreamSource<string>
        {
            Database = DatabaseName,
            Collection = "never_watched",
            StartAtOperationTime = DateTimeOffset.FromUnixTimeSeconds(int.MaxValue).AddSeconds(1),
            EventMapper = doc => doc.FullDocument["name"].AsString,
        };

        var error = Assert.Throws<InvalidOperationException>(
            () => source.Execute(CancellationToken.None)
        );
        Assert.Contains("StartAtOperationTime", error.Message, StringComparison.Ordinal);
    }

    // A validation failure must still complete the buffer, or a linked destination's Wait()
    // would hang forever on a pipeline that never started.
    [Fact]
    public async Task Execute_WhenValidationFails_StillCompletesTheBuffer()
    {
        var destination = new CustomDestination<string>(_ => { });
        var source = new MongoChangeStreamSource<string>
        {
            Database = DatabaseName,
            Collection = "never_watched",
            StartAtOperationTime = DateTimeOffset.UtcNow,
            StartAfter = "{ \"_data\": \"abc\" }",
            EventMapper = doc => doc.FullDocument["name"].AsString,
        };
        source.LinkTo(destination);

        Assert.Throws<InvalidOperationException>(() => source.Execute(CancellationToken.None));

        var completedInTime =
            await Task.WhenAny(destination.Completion, Task.Delay(TimeSpan.FromSeconds(5)))
            == destination.Completion;
        Assert.True(
            completedInTime,
            "Buffer was not completed after a validation failure — a linked destination would hang."
        );
    }
}
