using System.Reflection;
using System.Threading.Tasks.Dataflow;
using EtlKit.Common.DataFlow;
using EtlKit.Common.DataFlow.Streaming;
using EtlKit.DataFlow;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

// ReSharper disable AccessToDisposedClosure

namespace EtlKit.MongoDB.Tests;

[Collection("MongoDB")]
public sealed class MongoChangeStreamSourceTests : IClassFixture<MongoContainerFixture>
{
    private const string DatabaseName = "etltest";
    private readonly MongoContainerFixture _fixture;

    public MongoChangeStreamSourceTests(MongoContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private IMongoClient CreateClient() => new MongoClient(_fixture.ConnectionString);

    private static IMongoCollection<BsonDocument> GetCollection(IMongoClient client, string name)
    {
        var db = client.GetDatabase(DatabaseName);
        var collection = db.GetCollection<BsonDocument>(name);
        collection.DeleteMany(FilterDefinition<BsonDocument>.Empty);
        return collection;
    }

    private static void WaitForResults<T>(List<T> results, int expectedCount, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (results.Count < expectedCount && DateTime.UtcNow < deadline)
            Thread.Sleep(30);
    }

    [Fact]
    public async Task Execute_ReceivesInsertedDocuments_InOrder()
    {
        var client = CreateClient();
        var collection = GetCollection(client, "change_stream_basic");

        var results = new List<string>();
        var destination = new CustomDestination<string>(name => results.Add(name));

        using var tokenSource = new CancellationTokenSource();
        var source = new MongoChangeStreamSource<string>
        {
            MongoClient = client,
            Database = DatabaseName,
            Collection = "change_stream_basic",
            MaxAwaitTime = TimeSpan.FromMilliseconds(200),
            EventMapper = doc => doc.FullDocument["name"].AsString,
        };
        source.LinkTo(destination);

        // ReSharper disable once AccessToDisposedClosure
        var executeTask = Task.Run(() => source.Execute(tokenSource.Token), CancellationToken.None);

        // Allow the cursor to open before inserting
        await Task.Delay(500, CancellationToken.None).ConfigureAwait(true);

        await collection
            .InsertOneAsync(new BsonDocument { { "name", "alpha" } }, null, CancellationToken.None)
            .ConfigureAwait(true);
        await collection.InsertOneAsync(
            new BsonDocument { { "name", "beta" } },
            null,
            CancellationToken.None
        );
        await collection.InsertOneAsync(
            new BsonDocument { { "name", "gamma" } },
            null,
            CancellationToken.None
        );

        WaitForResults(results, 3, TimeSpan.FromSeconds(15));
        await tokenSource.CancelAsync();

        Assert.Throws<OperationCanceledException>(() => executeTask.GetAwaiter().GetResult());
        destination.Wait();

        Assert.Equal(3, results.Count);
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, results);
    }

    // Output carries the resume token alongside the payload — for change streams the checkpoint
    // position is the resume token (not a domain field), so the EventMapper must surface it for the
    // CheckpointWriter to commit. (Mongo wrinkle, see docs/dataflow/streaming-sources.md.)
    private readonly record struct Event(string Name, string Token) : IComparable<Event>
    {
        public int CompareTo(Event other) => string.CompareOrdinal(Token, other.Token);
    }

    [Fact]
    public async Task Execute_WithCheckpoint_ResumesAfterToken()
    {
        const string checkpointId = "mongo-resume-test";
        var client = CreateClient();
        var collection = GetCollection(client, "change_stream_checkpoint");
        var checkpointStore = new InMemoryCheckpointStore<string>();

        // First run — receive two inserts; a CheckpointWriter commits the resume token after the
        // destination.
        var firstRun = new List<string>();
        using var tokenSource1 = new CancellationTokenSource();
        var source1 = NewSource(client, checkpointId, checkpointStore);
        var record1 = new RowTransformation<Event>(e =>
        {
            firstRun.Add(e.Name);
            return e;
        });
        var writer1 = NewWriter(checkpointId, checkpointStore);
        source1.LinkTo(record1);
        record1.LinkTo(writer1);

        // ReSharper disable once AccessToDisposedClosure
        var task1 = Task.Run(() => source1.Execute(tokenSource1.Token));
        await Task.Delay(500).ConfigureAwait(true);

        await collection.InsertOneAsync(new BsonDocument { { "name", "first" } });
        await collection.InsertOneAsync(new BsonDocument { { "name", "second" } });

        WaitForResults(firstRun, 2, TimeSpan.FromSeconds(15));
        await tokenSource1.CancelAsync();

        Assert.Throws<OperationCanceledException>(() => task1.GetAwaiter().GetResult());
        writer1.Wait();

        Assert.Equal(new[] { "first", "second" }, firstRun);
        Assert.True(checkpointStore.CommitCount > 0);

        // Insert new documents after the checkpoint was committed
        await collection.InsertOneAsync(new BsonDocument { { "name", "third" } });
        await collection.InsertOneAsync(new BsonDocument { { "name", "fourth" } });

        // Second run — resume from the committed token, should receive only the new documents
        var secondRun = new List<string>();
        using var tokenSource2 = new CancellationTokenSource();
        var source2 = NewSource(client, checkpointId, checkpointStore);
        var record2 = new RowTransformation<Event>(e =>
        {
            secondRun.Add(e.Name);
            return e;
        });
        var writer2 = NewWriter(checkpointId, checkpointStore);
        source2.LinkTo(record2);
        record2.LinkTo(writer2);

        var task2 = Task.Run(() => source2.Execute(tokenSource2.Token), CancellationToken.None);

        WaitForResults(secondRun, 2, TimeSpan.FromSeconds(15));
        await tokenSource2.CancelAsync();

        Assert.Throws<OperationCanceledException>(() => task2.GetAwaiter().GetResult());
        writer2.Wait();

        Assert.Equal(new[] { "third", "fourth" }, secondRun);
    }

    private MongoChangeStreamSource<Event> NewSource(
        IMongoClient client,
        string checkpointId,
        ICheckpointStore<string> store
    ) =>
        new()
        {
            MongoClient = client,
            Database = DatabaseName,
            Collection = "change_stream_checkpoint",
            MaxAwaitTime = TimeSpan.FromMilliseconds(200),
            CheckpointStore = store,
            CheckpointId = checkpointId,
            EventMapper = doc => new Event(
                doc.FullDocument["name"].AsString,
                doc.ResumeToken.ToJson()
            ),
        };

    private static CheckpointWriter<Event, string> NewWriter(
        string checkpointId,
        ICheckpointStore<string> store
    ) =>
        new()
        {
            CheckpointStore = store,
            CheckpointId = checkpointId,
            Position = e => e.Token,
        };

    [Fact]
    public async Task Execute_CancellationDuringBlockedSendAsync_ReturnsPromptly()
    {
        // Regression: RunChangeStreamLoop calls
        //   Buffer.SendAsync(item, CancellationToken.None).Wait(CancellationToken.None)
        // — neither the SendAsync nor the Wait observes the source's cancellation
        // token. When the BufferBlock is bounded (e.g., a downstream pipeline applies
        // backpressure via BoundedCapacity propagation), SendAsync blocks indefinitely
        // on capacity, and cancelling the source has no effect.
        //
        // Force the bounded-buffer scenario by replacing the source's unbounded Buffer
        // with a BoundedCapacity=1 BufferBlock and leaving it without a consumer. The
        // source must still return after Cancel within a reasonable budget.
        var client = CreateClient();
        var collection = GetCollection(client, "change_stream_cancel_send");

        using var tokenSource = new CancellationTokenSource();
        var source = new MongoChangeStreamSource<string>
        {
            MongoClient = client,
            Database = DatabaseName,
            Collection = "change_stream_cancel_send",
            MaxAwaitTime = TimeSpan.FromMilliseconds(200),
            EventMapper = doc => doc.FullDocument["name"].AsString,
        };
        ReplaceBufferWithBounded(source, capacity: 1);

        var task = Task.Run(() => source.Execute(tokenSource.Token), CancellationToken.None);

        // Allow the change-stream cursor to open before inserting.
        await Task.Delay(500, CancellationToken.None).ConfigureAwait(true);

        // Push enough events to fill the bounded buffer (capacity 1) and block the
        // source on its second SendAsync.
        for (var i = 0; i < 5; i++)
        {
            await collection
                .InsertOneAsync(new BsonDocument { { "name", $"row{i}" } })
                .ConfigureAwait(true);
        }

        // Give the source time to enter the blocked SendAsync state.
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

    private static void ReplaceBufferWithBounded<TOutput>(
        MongoChangeStreamSource<TOutput> source,
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
        prop.SetValue(source, bounded);
    }

    [Fact]
    public async Task Execute_WithPipeline_FiltersEvents()
    {
        var client = CreateClient();
        var collection = GetCollection(client, "change_stream_pipeline");

        var filterStage = new BsonDocumentPipelineStageDefinition<
            ChangeStreamDocument<BsonDocument>,
            ChangeStreamDocument<BsonDocument>
        >(BsonDocument.Parse("{ $match: { 'fullDocument.keep': true } }"));
        var pipeline = new EmptyPipelineDefinition<
            ChangeStreamDocument<BsonDocument>
        >().AppendStage(filterStage);

        var results = new List<string>();
        var destination = new CustomDestination<string>(name => results.Add(name));

        using var tokenSource = new CancellationTokenSource();
        var source = new MongoChangeStreamSource<string>
        {
            MongoClient = client,
            Database = DatabaseName,
            Collection = "change_stream_pipeline",
            MaxAwaitTime = TimeSpan.FromMilliseconds(200),
            Pipeline = pipeline,
            EventMapper = doc => doc.FullDocument["name"].AsString,
        };
        source.LinkTo(destination);

        var executeTask = Task.Run(() => source.Execute(tokenSource.Token), CancellationToken.None);
        await Task.Delay(500, CancellationToken.None).ConfigureAwait(true);

        await collection.InsertOneAsync(
            new BsonDocument { { "name", "keep_me" }, { "keep", true } }
        );
        await collection.InsertOneAsync(
            new BsonDocument { { "name", "skip_me" }, { "keep", false } }
        );
        await collection.InsertOneAsync(
            new BsonDocument { { "name", "keep_too" }, { "keep", true } }
        );

        WaitForResults(results, 2, TimeSpan.FromSeconds(15));
        await tokenSource.CancelAsync();

        Assert.Throws<OperationCanceledException>(() => executeTask.GetAwaiter().GetResult());
        destination.Wait();

        Assert.Equal(new[] { "keep_me", "keep_too" }, results);
    }
}
