# MongoChangeStreamSource Start Position Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a `MongoChangeStreamSource<TOutput>` start from an explicitly chosen position so cold starts stop losing events, make resuming past an `invalidate` possible, and stop the source from spinning on a cursor the server has closed.

**Architecture:** Three new configuration properties on the source resolve, together with the existing checkpoint token, to exactly one of MongoDB's mutually exclusive start options (`resumeAfter` / `startAfter` / `startAtOperationTime`). A new static helper snapshots the deployment's cluster time so callers do not reach for a client clock. The change-stream read loop stops when the cursor closes instead of re-entering it. Everything is additive: with no new property set, behaviour is byte-for-byte what it is today.

**Tech Stack:** .NET (`net6.0` library, `net8.0` tests), MongoDB.Driver 3.8.0, xUnit 2.9.3, Testcontainers.MongoDb 4.11.0 (`mongo:6.0` with replica set), TPL Dataflow.

**Design spec:** [`docs/superpowers/specs/2026-08-07-mongo-change-stream-start-position-design.md`](../specs/2026-08-07-mongo-change-stream-start-position-design.md)
**Ticket:** [RSSL-11926](https://jira.rapidsoft.ru/browse/RSSL-11926)

## Global Constraints

- **Language:** all code comments, XML docs, markdown and commit messages in English.
- **Target frameworks:** `EtlKit.MongoDB` is `net6.0`; `EtlKit.MongoDB.Tests` is `net8.0`. C# language version 12.
- **Warnings are errors** (except `CS0618`, `CS1574`). A build warning fails the build.
- **XML documentation is required** on every public member of `EtlKit.MongoDB`.
- **`[PublicAPI]`** (JetBrains.Annotations) goes on every new public type.
- **Tests use xUnit `Assert.*` only.** FluentAssertions is banned in this repository.
- **Spell checking** is on (WeCantSpell.Roslyn) with the dictionary at `.directory.dic`. If the build reports a spelling diagnostic for a word this plan introduces, add that exact word to `.directory.dic` (alphabetical order, one word per line) and rebuild.
- **Commits follow Conventional Commits**, enforced by a Husky hook: `type(scope): subject`, 1–90 characters total, at least 4 characters of subject, no trailing period or whitespace. The hook appends the `Changelog:` trailer itself — do not write one.
- **Additive only.** With `StartAtOperationTime`, `StartAfter` unset and `CheckpointResumeMode` at its default, resolution must produce exactly today's `ChangeStreamOptions`.
- **Docker must be running** for every integration test in this plan. Verify with `docker version` before starting.
- **Branch:** `dev/RSSL-11926`, already created from `master`. An unrelated uncommitted edit to `Directory.Build.props` (WeCantSpell version) is present in the working tree — never `git add` it.

## File Structure

| File | Responsibility |
|---|---|
| `EtlKit.MongoDB/ChangeStreamResumeMode.cs` (create) | The `ChangeStreamResumeMode` enum — how a stored checkpoint token is applied. |
| `EtlKit.MongoDB/MongoChangeStreamPosition.cs` (create) | Snapshotting a deployment cluster time, and converting a `DateTimeOffset` to the driver's `BsonTimestamp`. The only place that knows the BSON timestamp range. |
| `EtlKit.MongoDB/MongoChangeStreamSource.cs` (modify) | The three new properties, start-position resolution, validation, and the read-loop fix. |
| `EtlKit.MongoDB/EtlKit.MongoDB.csproj` (modify) | `InternalsVisibleTo` for the test project. |
| `EtlKit.MongoDB.Tests/MongoDbCollection.cs` (create) | xUnit collection definition so all Mongo test classes share one container. |
| `EtlKit.MongoDB.Tests/MongoChangeStreamPositionTests.cs` (create) | Unit tests for the conversion, integration tests for the snapshot. |
| `EtlKit.MongoDB.Tests/MongoChangeStreamStartPositionTests.cs` (create) | Resolution order, validation, invalidate recovery, dead-cursor behaviour. |
| `EtlKit.MongoDB.Tests/MongoChangeStreamSourceTests.cs` (modify) | Existing suite: share the container fixture, drop the fixed `Task.Delay` waits. |
| `docs/dataflow/streaming-sources.md` (modify) | User documentation. |
| `docs/changelog/mongo-change-stream-start-position.md` (create) | Changelog entry per repository convention. |

---

### Task 1: Shared test infrastructure — one container, one set of helpers

The suite is about to grow from one test class to three, and two things do not survive that growth.

First, `MongoChangeStreamSourceTests` declares `[Collection("MongoDB")]` but there is no matching `[CollectionDefinition]`, so the attribute does nothing and the container comes from `IClassFixture` — one container **per class**. Left alone, this plan would start three containers, each with a 3-minute startup budget.

Second, its `DatabaseName`, `GetCollection` and `WaitForResults` members would have to be copied verbatim into every new class. Move them somewhere shared before that happens.

**Files:**
- Create: `EtlKit.MongoDB.Tests/MongoDbCollection.cs`
- Create: `EtlKit.MongoDB.Tests/MongoTestHelpers.cs`
- Modify: `EtlKit.MongoDB.Tests/MongoChangeStreamSourceTests.cs`

**Interfaces:**
- Consumes: `MongoContainerFixture` (exists, `EtlKit.MongoDB.Tests/MongoContainerFixture.cs`), exposing `string ConnectionString`.
- Produces:
  - xUnit collection named `"MongoDB"` carrying a shared `MongoContainerFixture`. Every later test class in this plan joins it with `[Collection("MongoDB")]` and takes `MongoContainerFixture` as its only constructor parameter — **without** `IClassFixture`.
  - `internal static class MongoTestHelpers` in namespace `EtlKit.MongoDB.Tests`, holding `public const string DatabaseName = "etltest"`, `public static IMongoCollection<BsonDocument> GetCollection(IMongoClient client, string name)` and `public static void WaitForResults<T>(List<T> results, int expectedCount, TimeSpan timeout)`. Every later test class in this plan reaches them through `using static EtlKit.MongoDB.Tests.MongoTestHelpers;` and keeps its own one-line `CreateClient()`, which closes over that class's own `_fixture` field.

- [ ] **Step 1: Create the collection definition**

Create `EtlKit.MongoDB.Tests/MongoDbCollection.cs`:

```csharp
using JetBrains.Annotations;
using Xunit;

namespace EtlKit.MongoDB.Tests;

// One MongoDB container for every test class in this assembly. Without this definition the
// [Collection("MongoDB")] attributes are inert and each class starts its own container.
[UsedImplicitly]
[CollectionDefinition("MongoDB")]
public sealed class MongoDbCollection : ICollectionFixture<MongoContainerFixture>;
```

- [ ] **Step 2: Take the fixture from the collection instead of the class**

In `EtlKit.MongoDB.Tests/MongoChangeStreamSourceTests.cs`, replace the class declaration:

```csharp
[Collection("MongoDB")]
public sealed class MongoChangeStreamSourceTests : IClassFixture<MongoContainerFixture>
{
```

with:

```csharp
[Collection("MongoDB")]
public sealed class MongoChangeStreamSourceTests
{
```

Leave the constructor and the `_fixture` field exactly as they are — xUnit injects the collection fixture through the same constructor parameter.

- [ ] **Step 3: Extract the shared helpers**

Create `EtlKit.MongoDB.Tests/MongoTestHelpers.cs`:

```csharp
using MongoDB.Bson;
using MongoDB.Driver;

namespace EtlKit.MongoDB.Tests;

// Shared by every Mongo test class in this assembly. Reach them with
// `using static EtlKit.MongoDB.Tests.MongoTestHelpers;` so call sites read unqualified.
internal static class MongoTestHelpers
{
    public const string DatabaseName = "etltest";

    public static IMongoCollection<BsonDocument> GetCollection(IMongoClient client, string name)
    {
        var db = client.GetDatabase(DatabaseName);
        var collection = db.GetCollection<BsonDocument>(name);
        collection.DeleteMany(FilterDefinition<BsonDocument>.Empty);
        return collection;
    }

    public static void WaitForResults<T>(List<T> results, int expectedCount, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (results.Count < expectedCount && DateTime.UtcNow < deadline)
            Thread.Sleep(30);
    }
}
```

- [ ] **Step 4: Point the existing class at the shared helpers**

In `EtlKit.MongoDB.Tests/MongoChangeStreamSourceTests.cs`, add to the using block:

```csharp
using static EtlKit.MongoDB.Tests.MongoTestHelpers;
```

then delete these three members from the class — the `using static` makes every existing call site resolve unchanged:

```csharp
    private const string DatabaseName = "etltest";
```

```csharp
    private static IMongoCollection<BsonDocument> GetCollection(IMongoClient client, string name)
    {
        var db = client.GetDatabase(DatabaseName);
        var collection = db.GetCollection<BsonDocument>(name);
        collection.DeleteMany(FilterDefinition<BsonDocument>.Empty);
        return collection;
    }
```

```csharp
    private static void WaitForResults<T>(List<T> results, int expectedCount, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (results.Count < expectedCount && DateTime.UtcNow < deadline)
            Thread.Sleep(30);
    }
```

Keep `private IMongoClient CreateClient() => new MongoClient(_fixture.ConnectionString);` where it is — it closes over the class's own `_fixture` field, so it is not shareable as a static.

- [ ] **Step 5: Run the whole existing suite**

Run: `dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj`
Expected: PASS, 4 tests. This is a refactor; a behaviour change here would show up as a failure.

- [ ] **Step 6: Commit**

```bash
git add EtlKit.MongoDB.Tests/MongoDbCollection.cs EtlKit.MongoDB.Tests/MongoTestHelpers.cs EtlKit.MongoDB.Tests/MongoChangeStreamSourceTests.cs
git commit -m "test(mongodb): share container and helpers across Mongo tests"
```

---

### Task 2: `MongoChangeStreamPosition` — snapshot and convert

**Files:**
- Create: `EtlKit.MongoDB/MongoChangeStreamPosition.cs`
- Modify: `EtlKit.MongoDB/EtlKit.MongoDB.csproj`
- Create: `EtlKit.MongoDB.Tests/MongoChangeStreamPositionTests.cs`

**Interfaces:**
- Consumes: the `"MongoDB"` collection from Task 1.
- Produces, all in namespace `EtlKit.DataFlow`:
  - `public static DateTimeOffset MongoChangeStreamPosition.Current(IMongoClient client, string database, CancellationToken cancellationToken = default)`
  - `internal static BsonTimestamp MongoChangeStreamPosition.ToBsonTimestamp(DateTimeOffset value)`
  - `internal static bool MongoChangeStreamPosition.IsRepresentable(DateTimeOffset value)`

- [ ] **Step 1: Expose internals to the test project**

In `EtlKit.MongoDB/EtlKit.MongoDB.csproj`, add a new `ItemGroup` immediately after the `ProjectReference` group (this mirrors `EtlKit.AI/EtlKit.AI.csproj`):

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="EtlKit.MongoDB.Tests" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing conversion tests**

Create `EtlKit.MongoDB.Tests/MongoChangeStreamPositionTests.cs`:

```csharp
using EtlKit.DataFlow;
using MongoDB.Driver;
using Xunit;
using static EtlKit.MongoDB.Tests.MongoTestHelpers;

namespace EtlKit.MongoDB.Tests;

public sealed class MongoChangeStreamPositionConversionTests
{
    // A BSON timestamp is (seconds since epoch, ordinal within that second). The ordinal is a
    // server-assigned operation counter, not a fraction, so a sub-second remainder has no correct
    // target and is dropped. It must be dropped DOWNWARDS: rounding up would place the start
    // position after operations that already happened and silently lose them.
    [Fact]
    public void ToBsonTimestamp_TruncatesSubSecondRemainderDownwards()
    {
        var wholeSecond = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var withRemainder = wholeSecond.AddMilliseconds(999).AddTicks(9);

        var result = MongoChangeStreamPosition.ToBsonTimestamp(withRemainder);

        Assert.Equal((int)wholeSecond.ToUnixTimeSeconds(), result.Timestamp);
        Assert.Equal(0, result.Increment);
    }

    [Fact]
    public void ToBsonTimestamp_ExactSecond_KeepsTheSecondAndZeroesTheIncrement()
    {
        var wholeSecond = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);

        var result = MongoChangeStreamPosition.ToBsonTimestamp(wholeSecond);

        Assert.Equal((int)wholeSecond.ToUnixTimeSeconds(), result.Timestamp);
        Assert.Equal(0, result.Increment);
    }

    [Fact]
    public void ToBsonTimestamp_NormalisesOffsetToUtc()
    {
        var utc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var sameInstantElsewhere = new DateTimeOffset(2026, 8, 7, 13, 0, 0, TimeSpan.FromHours(3));

        Assert.Equal(
            MongoChangeStreamPosition.ToBsonTimestamp(utc).Timestamp,
            MongoChangeStreamPosition.ToBsonTimestamp(sameInstantElsewhere).Timestamp
        );
    }

    [Fact]
    public void IsRepresentable_RejectsInstantsOutsideTheBsonTimestampRange()
    {
        // The seconds field of a BSON timestamp is a 32-bit signed integer.
        Assert.False(
            MongoChangeStreamPosition.IsRepresentable(
                new DateTimeOffset(1969, 12, 31, 23, 59, 59, TimeSpan.Zero)
            )
        );
        Assert.False(
            MongoChangeStreamPosition.IsRepresentable(
                DateTimeOffset.FromUnixTimeSeconds(int.MaxValue).AddSeconds(1)
            )
        );
        Assert.True(
            MongoChangeStreamPosition.IsRepresentable(
                new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero)
            )
        );
    }
}

[Collection("MongoDB")]
public sealed class MongoChangeStreamPositionTests
{
    private readonly MongoContainerFixture _fixture;

    public MongoChangeStreamPositionTests(MongoContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Current_ReturnsATimestampFromTheDeployment()
    {
        var client = new MongoClient(_fixture.ConnectionString);

        var snapped = MongoChangeStreamPosition.Current(client, DatabaseName);

        // Deliberately loose: this proves a real server timestamp came back, not that the
        // container's clock and the host's are synchronised.
        var delta = (DateTimeOffset.UtcNow - snapped).Duration();
        Assert.True(
            delta < TimeSpan.FromMinutes(5),
            $"Snapped cluster time {snapped:O} is {delta} away from the local clock."
        );
    }

    [Fact]
    public void Current_NeverGoesBackwards()
    {
        var client = new MongoClient(_fixture.ConnectionString);

        var first = MongoChangeStreamPosition.Current(client, DatabaseName);
        var second = MongoChangeStreamPosition.Current(client, DatabaseName);

        Assert.True(second >= first, $"Cluster time went backwards: {first:O} then {second:O}.");
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj --filter "FullyQualifiedName~MongoChangeStreamPosition"`
Expected: FAIL to compile — `The name 'MongoChangeStreamPosition' does not exist in the current context`.

- [ ] **Step 4: Implement `MongoChangeStreamPosition`**

Create `EtlKit.MongoDB/MongoChangeStreamPosition.cs`:

```csharp
using System;
using System.Threading;

using JetBrains.Annotations;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EtlKit.DataFlow;

/// <summary>
/// Produces start positions for <see cref="MongoChangeStreamSource{TOutput}"/>.
/// </summary>
[PublicAPI]
public static class MongoChangeStreamPosition
{
    /// <summary>
    /// Snapshots the deployment's current cluster time, for use as
    /// <see cref="MongoChangeStreamSource{TOutput}.StartAtOperationTime"/>.
    /// </summary>
    /// <remarks>
    /// Take the snapshot before the writes that must not be missed. Do not substitute a client
    /// clock: a client running ahead of the deployment places the start position after writes that
    /// already happened, which is the cold-start gap this is meant to close.
    /// </remarks>
    /// <param name="client">Client connected to the deployment that will be watched.</param>
    /// <param name="database">Database used to issue the command.</param>
    /// <param name="cancellationToken">Token that cancels the command.</param>
    /// <returns>The deployment's cluster time, truncated to whole seconds.</returns>
    public static DateTimeOffset Current(
        IMongoClient client,
        string database,
        CancellationToken cancellationToken = default
    )
    {
        var reply = client
            .GetDatabase(database)
            .RunCommand<BsonDocument>(
                new BsonDocumentCommand<BsonDocument>(new BsonDocument("ping", 1)),
                cancellationToken: cancellationToken
            );
        return DateTimeOffset.FromUnixTimeSeconds(
            reply["$clusterTime"]["clusterTime"].AsBsonTimestamp.Timestamp
        );
    }

    /// <summary>
    /// Converts a point in time to the BSON timestamp a change stream starts from.
    /// </summary>
    /// <remarks>
    /// A BSON timestamp is (seconds, ordinal-within-that-second). The ordinal counts operations
    /// the server performed, so a wall-clock fraction cannot be mapped onto it and is discarded
    /// downwards — starting slightly early replays events, starting late loses them.
    /// Callers must check <see cref="IsRepresentable"/> first.
    /// </remarks>
    internal static BsonTimestamp ToBsonTimestamp(DateTimeOffset value) =>
        new((int)value.ToUnixTimeSeconds(), 0);

    /// <summary>
    /// Reports whether an instant fits the 32-bit seconds field of a BSON timestamp.
    /// </summary>
    internal static bool IsRepresentable(DateTimeOffset value) =>
        value.ToUnixTimeSeconds() is >= 0 and <= int.MaxValue;
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj --filter "FullyQualifiedName~MongoChangeStreamPosition"`
Expected: PASS, 6 tests.

**If `Current_ReturnsATimestampFromTheDeployment` fails with a `KeyNotFoundException` or `MongoDB.Bson.BsonSerializationException` mentioning `$clusterTime`,** the deployment is not gossiping cluster time on this command. Replace the body of `Current` with the reply's `operationTime` field instead, then re-run:

```csharp
        var clusterTime = reply.Contains("$clusterTime")
            ? reply["$clusterTime"]["clusterTime"].AsBsonTimestamp
            : reply["operationTime"].AsBsonTimestamp;
        return DateTimeOffset.FromUnixTimeSeconds(clusterTime.Timestamp);
```

- [ ] **Step 6: Commit**

```bash
git add EtlKit.MongoDB/MongoChangeStreamPosition.cs EtlKit.MongoDB/EtlKit.MongoDB.csproj EtlKit.MongoDB.Tests/MongoChangeStreamPositionTests.cs
git commit -m "feat(mongodb): add cluster time snapshot for change stream start"
```

---

### Task 3: Start-position properties and resolution order

**Files:**
- Create: `EtlKit.MongoDB/ChangeStreamResumeMode.cs`
- Modify: `EtlKit.MongoDB/MongoChangeStreamSource.cs:84-99`
- Create: `EtlKit.MongoDB.Tests/MongoChangeStreamStartPositionTests.cs`

**Interfaces:**
- Consumes: `MongoChangeStreamPosition.Current`, `.ToBsonTimestamp`, `.IsRepresentable` from Task 2; the `"MongoDB"` collection from Task 1.
- Produces, on `MongoChangeStreamSource<TOutput>`:
  - `public DateTimeOffset? StartAtOperationTime { get; set; }`
  - `public string? StartAfter { get; set; }`
  - `public ChangeStreamResumeMode CheckpointResumeMode { get; set; }` (default `ResumeAfter`)
  - and `public enum ChangeStreamResumeMode { ResumeAfter, StartAfter }` in `EtlKit.DataFlow`.

Resolution order, exactly one applied:

| # | Condition | Applied |
|---|---|---|
| 1 | checkpoint token found | `ResumeAfter` or `StartAfter` per `CheckpointResumeMode` |
| 2 | else `StartAfter` set | `StartAfter` |
| 3 | else `StartAtOperationTime` set | `StartAtOperationTime` |
| 4 | else | nothing |

- [ ] **Step 1: Write the failing tests**

Create `EtlKit.MongoDB.Tests/MongoChangeStreamStartPositionTests.cs`:

```csharp
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
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj --filter "FullyQualifiedName~MongoChangeStreamStartPositionTests"`
Expected: FAIL to compile — `'MongoChangeStreamSource<string>' does not contain a definition for 'StartAtOperationTime'`.

- [ ] **Step 3: Create the enum**

Create `EtlKit.MongoDB/ChangeStreamResumeMode.cs`:

```csharp
using JetBrains.Annotations;

namespace EtlKit.DataFlow;

/// <summary>
/// Controls how a resume token loaded from a checkpoint store is applied when the change stream
/// is opened.
/// </summary>
[PublicAPI]
public enum ChangeStreamResumeMode
{
    /// <summary>
    /// Apply the token as <c>resumeAfter</c>. MongoDB rejects this once an <c>invalidate</c>
    /// event has been delivered for the stream.
    /// </summary>
    ResumeAfter,

    /// <summary>
    /// Apply the token as <c>startAfter</c>, which additionally resumes past an <c>invalidate</c>
    /// event — the watched collection having been dropped or renamed. Requires MongoDB 4.1.1
    /// or later.
    /// </summary>
    StartAfter,
}
```

- [ ] **Step 4: Add the properties to the source**

In `EtlKit.MongoDB/MongoChangeStreamSource.cs`, insert immediately after the `CheckpointId` property (currently ending at line 63):

```csharp
    /// <summary>
    /// Cold-start seed: starts the change stream at this point in time. Ignored when
    /// <see cref="CheckpointStore"/> yields a token. Snapshot it with
    /// <see cref="MongoChangeStreamPosition.Current"/> rather than reading a client clock — a
    /// client running ahead of the deployment would reintroduce the gap this closes.
    /// Mutually exclusive with <see cref="StartAfter"/>. Resolved to whole seconds, so the stream
    /// may replay events from within the snapshotted second (at-least-once, as documented).
    /// </summary>
    public DateTimeOffset? StartAtOperationTime { get; set; }

    /// <summary>
    /// Cold-start seed: starts the change stream strictly after this resume token, given in the
    /// same JSON form a <c>CheckpointWriter</c> commits (<c>doc.ResumeToken.ToJson()</c>).
    /// Ignored when <see cref="CheckpointStore"/> yields a token. Mutually exclusive with
    /// <see cref="StartAtOperationTime"/>.
    /// </summary>
    public string? StartAfter { get; set; }

    /// <summary>
    /// Controls how a token loaded from <see cref="CheckpointStore"/> is applied. Switch to
    /// <see cref="ChangeStreamResumeMode.StartAfter"/> to resume past an <c>invalidate</c> event,
    /// which <c>resumeAfter</c> cannot do.
    /// </summary>
    public ChangeStreamResumeMode CheckpointResumeMode { get; set; } =
        ChangeStreamResumeMode.ResumeAfter;
```

- [ ] **Step 5: Replace the start-position block in `RunChangeStreamLoop`**

In the same file, replace these lines (currently 86 and 91-99):

```csharp
        var resumeToken = LoadResumeToken(ct);

        var db = MongoClient.GetDatabase(Database);
        var collection = db.GetCollection<BsonDocument>(Collection);

        var options = new ChangeStreamOptions
        {
            FullDocument = FullDocument,
            MaxAwaitTime = MaxAwaitTime,
        };
        if (resumeToken != null)
        {
            options.ResumeAfter = resumeToken;
        }
```

with:

```csharp
        var db = MongoClient.GetDatabase(Database);
        var collection = db.GetCollection<BsonDocument>(Collection);

        var options = new ChangeStreamOptions
        {
            FullDocument = FullDocument,
            MaxAwaitTime = MaxAwaitTime,
        };
        ApplyStartPosition(options, ct);
```

and add this private method directly above `LoadResumeToken`:

```csharp
    // MongoDB treats resumeAfter, startAfter and startAtOperationTime as mutually exclusive, so
    // exactly one of them is ever set. A committed checkpoint always outranks the configured
    // seeds: on a restart the consumer must continue from real progress, not replay from a value
    // that has been sitting in configuration since the first run.
    private void ApplyStartPosition(ChangeStreamOptions options, CancellationToken ct)
    {
        var checkpointToken = LoadResumeToken(ct);
        if (checkpointToken != null)
        {
            if (CheckpointResumeMode == ChangeStreamResumeMode.StartAfter)
                options.StartAfter = checkpointToken;
            else
                options.ResumeAfter = checkpointToken;
            return;
        }

        if (StartAfter != null)
        {
            options.StartAfter = BsonDocument.Parse(StartAfter);
            return;
        }

        if (StartAtOperationTime is { } startAt)
        {
            options.StartAtOperationTime = MongoChangeStreamPosition.ToBsonTimestamp(startAt);
        }
    }
```

- [ ] **Step 6: Run the new tests to verify they pass**

Run: `dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj --filter "FullyQualifiedName~MongoChangeStreamStartPositionTests"`
Expected: PASS, 3 tests.

- [ ] **Step 7: Run the whole Mongo suite to confirm nothing regressed**

Run: `dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj`
Expected: PASS, 13 tests. The four pre-existing tests set none of the new properties, so they must still take the "nothing applied" branch.

- [ ] **Step 8: Commit**

```bash
git add EtlKit.MongoDB/ChangeStreamResumeMode.cs EtlKit.MongoDB/MongoChangeStreamSource.cs EtlKit.MongoDB.Tests/MongoChangeStreamStartPositionTests.cs
git commit -m "feat(mongodb): add explicit change stream start position"
```

---

### Task 4: Fail fast on a contradictory start position

**Files:**
- Modify: `EtlKit.MongoDB/MongoChangeStreamSource.cs:69-82` (`Execute`)
- Modify: `EtlKit.MongoDB.Tests/MongoChangeStreamStartPositionTests.cs`

**Interfaces:**
- Consumes: `MongoChangeStreamPosition.IsRepresentable` (Task 2), the three properties (Task 3).
- Produces: `Execute` throws `InvalidOperationException` for a contradictory or unrepresentable start position, before it touches `MongoClient`.

These tests need no container: `MongoClient` is left `null!`, so if validation did not run first the source would throw `NullReferenceException` instead. That is what proves the check happens before any connection work.

- [ ] **Step 1: Write the failing tests**

Append to `MongoChangeStreamStartPositionTests` in `EtlKit.MongoDB.Tests/MongoChangeStreamStartPositionTests.cs`:

```csharp
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
    public void Execute_WhenValidationFails_StillCompletesTheBuffer()
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

        Assert.True(
            destination.Completion.Wait(TimeSpan.FromSeconds(5)),
            "Buffer was not completed after a validation failure — a linked destination would hang."
        );
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj --filter "FullyQualifiedName~MongoChangeStreamStartPositionTests"`
Expected: the three new tests FAIL with `NullReferenceException` (validation does not exist yet, so the source dereferences the null `MongoClient`).

`Completion` is a `public Task` on `DataFlowDestination<TInput>` (`EtlKit.Common/DataFlow/DataFlowDestination.cs:29`), so `destination.Completion.Wait(TimeSpan)` compiles as written.

- [ ] **Step 3: Add validation to `Execute`**

In `EtlKit.MongoDB/MongoChangeStreamSource.cs`, replace the whole `Execute` method:

```csharp
    /// <inheritdoc/>
    public override void Execute(CancellationToken cancellationToken)
    {
        LogStart();
        try
        {
            RunChangeStreamLoop(cancellationToken);
        }
        finally
        {
            Buffer.Complete();
            LogFinish();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
```

with:

```csharp
    /// <inheritdoc/>
    public override void Execute(CancellationToken cancellationToken)
    {
        LogStart();
        try
        {
            // Inside the try so that a rejected configuration still completes the buffer —
            // otherwise a linked destination waits forever on a pipeline that never started.
            ValidateStartPosition();
            RunChangeStreamLoop(cancellationToken);
        }
        finally
        {
            Buffer.Complete();
            LogFinish();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void ValidateStartPosition()
    {
        if (StartAfter != null && StartAtOperationTime != null)
        {
            throw new InvalidOperationException(
                "MongoChangeStreamSource: StartAfter and StartAtOperationTime are mutually "
                    + "exclusive start positions. Set at most one of them."
            );
        }

        if (
            StartAtOperationTime is { } startAt
            && !MongoChangeStreamPosition.IsRepresentable(startAt)
        )
        {
            throw new InvalidOperationException(
                $"MongoChangeStreamSource: StartAtOperationTime ({startAt:O}) is outside the range "
                    + "a BSON timestamp can represent (1970-01-01 to 2038-01-19, UTC)."
            );
        }
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj --filter "FullyQualifiedName~MongoChangeStreamStartPositionTests"`
Expected: PASS, 6 tests.

- [ ] **Step 5: Commit**

```bash
git add EtlKit.MongoDB/MongoChangeStreamSource.cs EtlKit.MongoDB.Tests/MongoChangeStreamStartPositionTests.cs
git commit -m "feat(mongodb): reject contradictory change stream start position"
```

---

### Task 5: Stop instead of spinning on a closed cursor

`RunChangeStreamLoop` wraps `while (cursor.MoveNext(ct))` in an outer `while (!ct.IsCancellationRequested)`. `MoveNext` returning `false` means the cursor is exhausted or closed — which is what the server does after an `invalidate`. The outer loop then re-enters the same dead cursor immediately, with no delay and no reopen: a full-CPU spin until cancellation.

The outer loop exists only to perform that re-entry, so the fix is to delete it.

**Files:**
- Modify: `EtlKit.MongoDB/MongoChangeStreamSource.cs:105-124`
- Modify: `EtlKit.MongoDB.Tests/MongoChangeStreamStartPositionTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `Execute` returns normally (no `OperationCanceledException`) once the server closes the cursor, after logging a warning. Task 6 relies on this — an invalidate-recovery test cannot run if the first run never returns.

- [ ] **Step 1: Write the failing test**

Append to `MongoChangeStreamStartPositionTests` in `EtlKit.MongoDB.Tests/MongoChangeStreamStartPositionTests.cs`:

```csharp
    // Dropping the watched collection delivers an invalidate event and the server closes the
    // cursor. The source must stop rather than re-enter a dead cursor at full CPU.
    [Fact]
    public async Task Execute_WhenTheServerClosesTheCursor_ReturnsInsteadOfSpinning()
    {
        const string collectionName = "cursor_invalidated";
        var client = CreateClient();
        var collection = GetCollection(client, collectionName);
        var database = client.GetDatabase(DatabaseName);

        var startAt = MongoChangeStreamPosition.Current(client, DatabaseName);
        await collection.InsertOneAsync(new BsonDocument { { "name", "alpha" } });

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
            // Deletes and the invalidate itself carry no FullDocument.
            EventMapper = doc => doc.FullDocument?["name"]?.AsString ?? "<no-document>",
        };
        source.LinkTo(destination);

        var executeTask = Task.Run(() => source.Execute(tokenSource.Token), CancellationToken.None);
        WaitForResults(results, 1, TimeSpan.FromSeconds(15));

        await database.DropCollectionAsync(collectionName);

        // The token is never cancelled: returning at all is the assertion.
        var returned = await Task.WhenAny(executeTask, Task.Delay(TimeSpan.FromSeconds(15)));
        Assert.True(
            ReferenceEquals(returned, executeTask),
            "Execute did not return after the server closed the cursor — the outer loop is spinning."
        );
        await executeTask;
        destination.Wait();

        Assert.Contains("alpha", results);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj --filter "FullyQualifiedName~Execute_WhenTheServerClosesTheCursor_ReturnsInsteadOfSpinning"`
Expected: FAIL with the assertion message above, after roughly 15 seconds.

**If it passes unchanged,** the driver never surfaced a `false` from `MoveNext` in this scenario. Record that in the task's commit message, keep the test (it now guards the behaviour), and still apply Step 3 — re-entering an exhausted cursor is wrong regardless of whether this particular path reaches it.

- [ ] **Step 3: Delete the outer loop and log the closure**

In `EtlKit.MongoDB/MongoChangeStreamSource.cs`, replace:

```csharp
        while (!ct.IsCancellationRequested)
        {
            while (cursor.MoveNext(ct))
            {
                foreach (var doc in cursor.Current)
                {
                    ct.ThrowIfCancellationRequested();
                    var output = EventMapper(doc);
                    // Propagate the source's cancellation token into SendAsync so that
                    // backpressure from a bounded downstream buffer doesn't trap the
                    // change-stream loop after Cancel() — see RSSL-11703 regression test
                    // Execute_CancellationDuringBlockedSendAsync_ReturnsPromptly.
                    Buffer.SendAsync(output, ct).GetAwaiter().GetResult();
                    LogProgress();
                }
                // The durable resume token is NOT written here. A downstream CheckpointWriter
                // commits it after the destination persists (at-least-once); the source only
                // advances the live change-stream cursor in-memory. See ICheckpointStore.
            }
        }
```

with:

```csharp
        while (cursor.MoveNext(ct))
        {
            foreach (var doc in cursor.Current)
            {
                ct.ThrowIfCancellationRequested();
                var output = EventMapper(doc);
                // Propagate the source's cancellation token into SendAsync so that
                // backpressure from a bounded downstream buffer doesn't trap the
                // change-stream loop after Cancel() — see RSSL-11703 regression test
                // Execute_CancellationDuringBlockedSendAsync_ReturnsPromptly.
                Buffer.SendAsync(output, ct).GetAwaiter().GetResult();
                LogProgress();
            }
            // The durable resume token is NOT written here. A downstream CheckpointWriter
            // commits it after the destination persists (at-least-once); the source only
            // advances the live change-stream cursor in-memory. See ICheckpointStore.
        }

        // MoveNext returned false: the server closed the cursor, which is what happens after an
        // invalidate event (the watched collection was dropped or renamed). This used to sit
        // inside an outer while(!cancelled) loop that re-entered the dead cursor immediately,
        // burning a core until cancellation. Stop instead — resuming past an invalidate is only
        // legal via startAfter, and doing it implicitly would silently start reading a brand-new
        // collection that happens to reuse the old name. That is the caller's call, made with
        // CheckpointResumeMode.StartAfter.
        LogCursorClosed();
```

Add this private method directly below `RunChangeStreamLoop`:

```csharp
    private void LogCursorClosed()
    {
        if (DisableLogging)
            return;
        Logger.Warn(
            TaskName
                + " change stream cursor was closed by the server (e.g. after an invalidate event); the source stopped.",
            TaskType,
            "LOG",
            TaskHash,
            ControlFlow.ControlFlow.Stage,
            ControlFlow.ControlFlow.CurrentLoadProcess?.Id
        );
    }
```

Add `using EtlKit.Common.ControlFlow;` to the file's using block if the `Warn` extension does not resolve.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj --filter "FullyQualifiedName~Execute_WhenTheServerClosesTheCursor_ReturnsInsteadOfSpinning"`
Expected: PASS.

- [ ] **Step 5: Run the whole Mongo suite**

Run: `dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj`
Expected: PASS, 17 tests. `Execute_CancellationDuringBlockedSendAsync_ReturnsPromptly` in particular must still pass — cancellation now propagates out of `MoveNext` rather than out of the deleted outer loop.

- [ ] **Step 6: Commit**

```bash
git add EtlKit.MongoDB/MongoChangeStreamSource.cs EtlKit.MongoDB.Tests/MongoChangeStreamStartPositionTests.cs
git commit -m "fix(mongodb): stop change stream loop when the cursor is closed"
```

---

### Task 6: Resume past an invalidate with `CheckpointResumeMode.StartAfter`

**Files:**
- Modify: `EtlKit.MongoDB.Tests/MongoChangeStreamStartPositionTests.cs`

**Interfaces:**
- Consumes: `CheckpointResumeMode` and `ApplyStartPosition` (Task 3), the cursor-closed behaviour (Task 5), `InMemoryCheckpointStore<string>` (exists, `EtlKit.MongoDB.Tests/InMemoryCheckpointStore.cs`).
- Produces: no production code — this task proves the feature works end to end.

No production change is expected. If the test fails, the defect is in Task 3's `ApplyStartPosition`, and the fix belongs here.

- [ ] **Step 1: Write the test**

Append to `MongoChangeStreamStartPositionTests` in `EtlKit.MongoDB.Tests/MongoChangeStreamStartPositionTests.cs`:

```csharp
    // MongoDB refuses to resume with resumeAfter once an invalidate has been delivered; startAfter
    // exists for exactly this. Without CheckpointResumeMode the stored token would be unusable and
    // the consumer would be stuck permanently.
    [Fact]
    public async Task Execute_AfterInvalidate_ResumesWithCheckpointResumeModeStartAfter()
    {
        const string collectionName = "invalidate_recovery";
        const string checkpointId = "invalidate-recovery";
        var client = CreateClient();
        var collection = GetCollection(client, collectionName);
        var database = client.GetDatabase(DatabaseName);
        var store = new InMemoryCheckpointStore<string>();

        var startAt = MongoChangeStreamPosition.Current(client, DatabaseName);
        await collection.InsertOneAsync(new BsonDocument { { "name", "before_drop" } });

        // First run: read the event, commit its token, then drop the collection so the stream is
        // invalidated and the cursor closes.
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
            EventMapper = doc => (
                doc.FullDocument?["name"]?.AsString ?? "<no-document>",
                doc.ResumeToken.ToJson()
            ),
        };
        source1.LinkTo(capture);

        var run1 = Task.Run(() => source1.Execute(tokenSource1.Token), CancellationToken.None);
        WaitForResults(seen, 1, TimeSpan.FromSeconds(15));
        await store.CommitAsync(checkpointId, seen[0].Token, CancellationToken.None);

        await database.DropCollectionAsync(collectionName);
        await run1;
        capture.Wait();

        // Recreate the collection and write again.
        var recreated = database.GetCollection<BsonDocument>(collectionName);
        await recreated.InsertOneAsync(new BsonDocument { { "name", "after_drop" } });

        // Second run: the committed token is now unusable as resumeAfter. StartAfter mode makes it
        // usable again.
        var results = new List<string>();
        var destination = new CustomDestination<string>(name => results.Add(name));
        using var tokenSource2 = new CancellationTokenSource();
        var source2 = new MongoChangeStreamSource<string>
        {
            MongoClient = client,
            Database = DatabaseName,
            Collection = collectionName,
            MaxAwaitTime = TimeSpan.FromMilliseconds(200),
            CheckpointStore = store,
            CheckpointId = checkpointId,
            CheckpointResumeMode = ChangeStreamResumeMode.StartAfter,
            EventMapper = doc => doc.FullDocument?["name"]?.AsString ?? "<no-document>",
        };
        source2.LinkTo(destination);

        var run2 = Task.Run(() => source2.Execute(tokenSource2.Token), CancellationToken.None);
        WaitForResults(results, 1, TimeSpan.FromSeconds(20));
        await tokenSource2.CancelAsync();
        try
        {
            await run2;
        }
        catch (OperationCanceledException)
        {
            // Expected: the run was stopped by cancellation, not by a closed cursor.
        }
        destination.Wait();

        Assert.Contains("after_drop", results);
    }
```

- [ ] **Step 2: Run the test**

Run: `dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj --filter "FullyQualifiedName~Execute_AfterInvalidate_ResumesWithCheckpointResumeModeStartAfter"`
Expected: PASS.

If it fails with a `MongoCommandException` mentioning `resumeAfter`, `ApplyStartPosition` is not honouring `CheckpointResumeMode`; re-read Task 3 Step 5 and fix it there.

- [ ] **Step 3: Commit**

```bash
git add EtlKit.MongoDB.Tests/MongoChangeStreamStartPositionTests.cs
git commit -m "test(mongodb): cover change stream resume past an invalidate"
```

---

### Task 7: Remove the fixed delays from the existing tests

Four tests in `MongoChangeStreamSourceTests` wait `await Task.Delay(500)` for the cursor to open before writing. That is the flakiness this ticket was raised from: the delay narrows the race without closing it, and it loses on a loaded runner. Seeding from a snapshotted cluster time closes it structurally — the write is inside the cursor's scope no matter when the cursor opens.

**Files:**
- Modify: `EtlKit.MongoDB.Tests/MongoChangeStreamSourceTests.cs`

**Interfaces:**
- Consumes: `MongoChangeStreamPosition.Current` (Task 2), `StartAtOperationTime` (Task 3).
- Produces: nothing new.

Apply the same four-part edit to each of `Execute_ReceivesInsertedDocuments_InOrder`, `Execute_WithCheckpoint_ResumesAfterToken`, `Execute_CancellationDuringBlockedSendAsync_ReturnsPromptly` and `Execute_WithPipeline_FiltersEvents`:

1. After the `GetCollection(...)` call, snapshot the mark:
   ```csharp
   var startAt = MongoChangeStreamPosition.Current(client, DatabaseName);
   ```
2. Add `StartAtOperationTime = startAt,` to the source initialiser.
3. Delete the `await Task.Delay(500, CancellationToken.None).ConfigureAwait(true);` line and the `// Allow the cursor to open before inserting` comment above it.
4. Move each `Task.Delay`-free test's inserts up so they run immediately after `Task.Run(... Execute ...)`.

`Execute_WithCheckpoint_ResumesAfterToken` builds its source through the `NewSource` helper — add the seed as a parameter there:

```csharp
    private MongoChangeStreamSource<Event> NewSource(
        IMongoClient client,
        string checkpointId,
        ICheckpointStore<string> store,
        DateTimeOffset? startAt = null
    ) =>
        new()
        {
            MongoClient = client,
            Database = DatabaseName,
            Collection = "change_stream_checkpoint",
            MaxAwaitTime = TimeSpan.FromMilliseconds(200),
            CheckpointStore = store,
            CheckpointId = checkpointId,
            StartAtOperationTime = startAt,
            EventMapper = doc => new Event(
                doc.FullDocument["name"].AsString,
                doc.ResumeToken.ToJson()
            ),
        };
```

Pass `startAt` for the first run only; the second run resumes from the committed checkpoint and must not carry a seed (the checkpoint outranks it anyway, but leaving it out keeps the test's intent legible).

`Execute_CancellationDuringBlockedSendAsync_ReturnsPromptly` keeps its *second* `Task.Delay(500)` — that one waits for the source to become blocked on a full buffer, which is a different thing from cursor readiness. Only the delay under the `// Allow the change-stream cursor to open before inserting.` comment is removed.

- [ ] **Step 1: Apply the edits, using this test as the template**

`Execute_ReceivesInsertedDocuments_InOrder` goes from:

```csharp
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
```

to:

```csharp
        var client = CreateClient();
        var collection = GetCollection(client, "change_stream_basic");

        // Seed the stream from a mark taken before the inserts. The cursor may open whenever it
        // likes — the events below are inside its scope either way.
        var startAt = MongoChangeStreamPosition.Current(client, DatabaseName);

        var results = new List<string>();
        var destination = new CustomDestination<string>(name => results.Add(name));

        using var tokenSource = new CancellationTokenSource();
        var source = new MongoChangeStreamSource<string>
        {
            MongoClient = client,
            Database = DatabaseName,
            Collection = "change_stream_basic",
            MaxAwaitTime = TimeSpan.FromMilliseconds(200),
            StartAtOperationTime = startAt,
            EventMapper = doc => doc.FullDocument["name"].AsString,
        };
        source.LinkTo(destination);

        // ReSharper disable once AccessToDisposedClosure
        var executeTask = Task.Run(() => source.Execute(tokenSource.Token), CancellationToken.None);

        await collection
            .InsertOneAsync(new BsonDocument { { "name", "alpha" } }, null, CancellationToken.None)
            .ConfigureAwait(true);
```

The rest of that test is unchanged. Apply the same three edits — snapshot after `GetCollection`,
`StartAtOperationTime = startAt` in the initialiser, delete the delay and its comment — to
`Execute_WithPipeline_FiltersEvents` and `Execute_CancellationDuringBlockedSendAsync_ReturnsPromptly`,
and to `Execute_WithCheckpoint_ResumesAfterToken` via the `NewSource` change shown above.

- [ ] **Step 2: Verify no cursor-open delays remain**

Run: `grep -n "Allow the cursor\|Allow the change-stream cursor" EtlKit.MongoDB.Tests/MongoChangeStreamSourceTests.cs`
Expected: no output.

- [ ] **Step 3: Run the whole Mongo suite**

Run: `dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj`
Expected: PASS, 18 tests.

- [ ] **Step 4: Run the suite twice more to check for flakiness**

Run: `dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj && dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj`
Expected: PASS both times. These tests just lost their timing crutch; two clean consecutive runs is the minimum evidence that the seed replaced it correctly.

- [ ] **Step 5: Commit**

```bash
git add EtlKit.MongoDB.Tests/MongoChangeStreamSourceTests.cs
git commit -m "test(mongodb): replace cursor-open delays with a start position"
```

---

### Task 8: Documentation and changelog

**Files:**
- Modify: `docs/dataflow/streaming-sources.md:157-174`
- Create: `docs/changelog/mongo-change-stream-start-position.md`

**Interfaces:**
- Consumes: everything above.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Document cold start and invalidate**

In `docs/dataflow/streaming-sources.md`, insert these two sections immediately after the "Resuming after restart" section for `MongoChangeStreamSource` (after the paragraph ending "…giving at-least-once.", currently line 159) and before "### Key properties":

````markdown
### Cold start: the first run has no checkpoint

With no committed checkpoint and no start position, the cursor begins wherever `Watch()` lands, and
everything written between process start and that moment is lost — silently, with no error. The
window covers connecting to the replica set and any startup work, so it is measured in seconds.

This differs from `PostgresXminTailSource`, which reads the table from the beginning on an empty
checkpoint. The two sources share the `ICheckpointStore` contract but not their cold-start
guarantees.

Close the window by snapshotting the deployment's cluster time *before* the writes that matter and
seeding the source with it:

```csharp
var startAt = MongoChangeStreamPosition.Current(client, "mydb");

// ... anything written from here on is inside the stream's scope ...

var source = new MongoChangeStreamSource<MyEvent>
{
    MongoClient          = client,
    Database             = "mydb",
    Collection           = "orders",
    StartAtOperationTime = startAt,      // used only when no checkpoint is found
    CheckpointStore      = store,
    CheckpointId         = "orders-consumer",
    EventMapper          = doc => /* ... */,
};
```

`StartAtOperationTime` and `StartAfter` are **cold-start seeds**: a committed checkpoint always
outranks them, so a restart resumes from real progress rather than replaying from a value left in
configuration. Setting both seeds at once throws `InvalidOperationException`.

> **Use `MongoChangeStreamPosition.Current`, not a client clock.** `DateTimeOffset.UtcNow` on a host
> running ahead of the deployment places the start position *after* writes that already happened —
> reintroducing exactly the gap the seed is meant to close.

> **Second granularity.** A BSON timestamp is (seconds, ordinal-within-that-second), and the ordinal
> is a server-assigned operation counter rather than a fraction, so the sub-second part of a
> `DateTimeOffset` is discarded downwards. A stream can therefore start up to one second early and
> replay the events in that second. Under at-least-once that is the safe direction — and for a seed
> whose purpose is "begin before anything I did", the intended one.

### Resuming past an `invalidate`

When the watched collection is dropped or renamed, MongoDB delivers an `invalidate` event, closes
the cursor, and then refuses any `resumeAfter` using a token from that stream. A consumer holding a
committed token would be stuck permanently.

`CheckpointResumeMode` decides whether a stored token is applied as `resumeAfter` or `startAfter`:

```csharp
var source = new MongoChangeStreamSource<MyEvent>
{
    // ...
    CheckpointStore      = store,
    CheckpointId         = "orders-consumer",
    CheckpointResumeMode = ChangeStreamResumeMode.StartAfter,
};
```

> **Setting the mode is necessary but not sufficient.** `startAfter` widens which tokens MongoDB
> *accepts* as a starting point; it does not make a stream skip an `invalidate` it replays into.
> Recovery therefore works only if the checkpoint holds the **`invalidate` event's own token**. A
> token from before the drop replays `drop` → `invalidate`, the cursor closes again, and a consumer
> that simply restarts loops forever.
>
> Two things follow for the pipeline, and both are on the caller:
>
> - The `EventMapper` must tolerate an event with no `FullDocument`. `drop` and `invalidate` carry
>   none, so a bare `doc.FullDocument["name"]` throws. Use `doc.FullDocument?["name"]?.AsString ?? …`.
> - The mapped record must reach the downstream `CheckpointWriter`, so that token actually gets
>   committed. `MongoChangeStreamSource` never commits its own position — a pipeline that filters
>   out non-insert events will never checkpoint past the `invalidate` at all.

The source does not recover by itself: past an `invalidate`, resuming means reading a *new*
collection that reuses the old name, which is the caller's decision to make. When the cursor closes,
the source logs a warning and `Execute` returns normally.

`startAfter` requires MongoDB 4.1.1 or later. On older servers, leave `CheckpointResumeMode` at its
default.
````

- [ ] **Step 2: Add the new rows to the property table**

In the same file, in the `MongoChangeStreamSource` "Key properties" table, insert these rows after the `MaxAwaitTime` row:

```markdown
| `StartAtOperationTime` | `null` | Cold-start seed: point in time to start from. Snapshot it with `MongoChangeStreamPosition.Current`. Ignored when a checkpoint is found |
| `StartAfter` | `null` | Cold-start seed: resume token (JSON) to start strictly after. Ignored when a checkpoint is found |
| `CheckpointResumeMode` | `ResumeAfter` | How a stored token is applied. `StartAfter` also resumes past an `invalidate` (MongoDB 4.1.1+) |
```

- [ ] **Step 3: Correct two code comments that misstate the same semantics**

Both were written before the `invalidate` behaviour was pinned down by Task 6, and both now read as
if any stored token works.

In `EtlKit.MongoDB/ChangeStreamResumeMode.cs`, the XML doc on the `StartAfter` member currently says
it "additionally resumes past an `invalidate` event". Replace that summary with:

```csharp
    /// <summary>
    /// Apply the token as <c>startAfter</c>, which MongoDB also accepts when the token came from an
    /// <c>invalidate</c> event — the watched collection having been dropped or renamed. This widens
    /// which tokens are accepted as a start point; it does not skip an <c>invalidate</c> the stream
    /// replays into, so recovery requires the checkpoint to hold that <c>invalidate</c>'s own token.
    /// Requires MongoDB 4.1.1 or later.
    /// </summary>
```

In `EtlKit.MongoDB/MongoChangeStreamSource.cs`, the comment that follows the read loop ends with
"That is the caller's call, made with `CheckpointResumeMode.StartAfter`." That names no real member —
`CheckpointResumeMode` is the property, `ChangeStreamResumeMode` is the enum. Change that sentence to:

```csharp
        // That is the caller's call, made by setting CheckpointResumeMode to
        // ChangeStreamResumeMode.StartAfter.
```

- [ ] **Step 4: Write the changelog entry**

Create `docs/changelog/mongo-change-stream-start-position.md`:

```markdown
# MongoChangeStreamSource: explicit start position and dead-cursor handling

> **Status: COMPLETED** (2026-08-07) — RSSL-11926

## Problem

`MongoChangeStreamSource<TOutput>` set a start position only when a checkpoint token was loaded.
Without one, the cursor began wherever `Watch()` landed, and everything written between process
start and that moment was lost with no error and no log entry. The neighbouring
`PostgresXminTailSource` reads from the beginning on an empty checkpoint, so the two sources shared
the `ICheckpointStore` contract while offering different cold-start guarantees — undocumented.

Two further defects followed from the same code:

- Only `resumeAfter` was ever set, so a consumer could not resume past an `invalidate` event
  (the watched collection dropped or renamed), which MongoDB permits only via `startAfter`.
- `while (cursor.MoveNext(ct))` sat inside an outer `while (!ct.IsCancellationRequested)`. Once the
  server closed the cursor, the outer loop re-entered it immediately — a full-CPU spin until
  cancellation.

## Fix

Three additive properties on the source: `StartAtOperationTime` (`DateTimeOffset?`), `StartAfter`
(resume token JSON) and `CheckpointResumeMode`. The first two are cold-start seeds that a committed
checkpoint outranks; the third decides whether a stored token is applied as `resumeAfter` or
`startAfter`, which is what makes recovery from an `invalidate` possible. Contradictory
configurations throw `InvalidOperationException` before any connection is attempted.

`MongoChangeStreamPosition.Current` snapshots the deployment's cluster time so callers do not reach
for a client clock — skew there would restore the gap being closed.

The read loop's outer `while` was removed: `MoveNext` returning `false` means the cursor is
exhausted, and the source now logs a warning and stops.

Public API carries no driver types — `DateTimeOffset?`, `string` and an owned enum. `StartAfter`
uses the same JSON form the `CheckpointWriter` already commits.

## Behaviour compatibility

Fully additive. With no seed set and `CheckpointResumeMode` at its default, resolution produces
exactly the previous `ChangeStreamOptions`.

## Tests

`EtlKit.MongoDB.Tests` gained cold-start, seed-precedence, validation, invalidate-recovery and
closed-cursor coverage. The four pre-existing tests dropped their fixed `Task.Delay(500)` waits for
cursor readiness in favour of a snapshotted start position, which closes the race structurally
rather than narrowing it.
```

- [ ] **Step 5: Build the solution and run the full Mongo suite one last time**

Run: `dotnet build EtlKit.sln && dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj`
Expected: build succeeds with zero warnings; 18 tests pass.

If the build reports a WeCantSpell diagnostic (a `SP`-prefixed code) for a word introduced by this
plan, add that exact word to `.directory.dic` in alphabetical order, one word per line, and rebuild.

- [ ] **Step 6: Commit**

```bash
git add docs/dataflow/streaming-sources.md docs/changelog/mongo-change-stream-start-position.md EtlKit.MongoDB/ChangeStreamResumeMode.cs EtlKit.MongoDB/MongoChangeStreamSource.cs
git commit -m "docs(mongodb): document change stream start position and invalidate"
```

If `.directory.dic` changed, include it in the same commit.

---

## Verification checklist

Before opening the merge request:

- [ ] `dotnet build EtlKit.sln` — zero warnings.
- [ ] `dotnet test EtlKit.MongoDB.Tests/EtlKit.MongoDB.Tests.csproj` — 18 tests pass, twice in a row.
- [ ] `git diff master --stat` shows no change to `Directory.Build.props`.
- [ ] `git log master..HEAD --oneline` shows eight commits, each a Conventional Commit.
- [ ] Report back to RSSL-11926: the outcome of Task 5 Step 2 (whether the dead-cursor spin reproduced), and that ticket item 3 (readiness signal) was dropped with the reasoning recorded in the design spec.
