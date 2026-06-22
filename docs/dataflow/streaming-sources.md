# Streaming sources

Streaming sources continuously tail an external system and emit records into the data flow until
cancelled. They are designed for long-running, event-driven pipelines — unlike batch sources, they
never signal completion on their own and must be stopped via a `CancellationToken`.

Both sources described here support **resumable, at-least-once processing** through the
`ICheckpointStore<TPosition>` abstraction plus a terminal `CheckpointWriter<TInput, TPosition>`.

## Checkpointing: at-least-once by design

The checkpoint has **two cursors**:

- an **ephemeral read cursor** inside the source — advances as rows are emitted, never persisted;
- a **durable commit cursor** in the `ICheckpointStore` — advanced **only after** the destination
  has persisted the records.

The source **loads** the commit cursor on start but **never commits** it. A `CheckpointWriter`,
placed at the very end of the pipeline (after the real destination), commits the position once a
record has flowed all the way through. A crash between the destination write and the commit replays
the record (a duplicate) on restart — never drops it. That is **at-least-once**; downstream
consumers must be idempotent. (If the destination and the checkpoint share one transaction, commit
inside it for effective exactly-once — see "Co-located commit" below.)

### ICheckpointStore&lt;TPosition&gt;

Defined in `EtlKit.Common`. `TPosition` is the single monotone stream position (e.g. `long` for a
sequence column, a resume-token `string`), hence `IComparable<TPosition>` — commits advance strictly
forward and never regress.

```csharp
public interface ICheckpointStore<TPosition> where TPosition : IComparable<TPosition>
{
    Task<(bool Found, TPosition Position)> LoadAsync(string checkpointId, CancellationToken ct);
    Task CommitAsync(string checkpointId, TPosition position, CancellationToken ct);
}
```

`checkpointId` identifies a single **consumer's** progress, not the stream. The same stream can be
tailed by several consumers, each with its own `checkpointId` and therefore its own cursor (the
Kafka consumer-group model); one store can hold many checkpoints. Source and `CheckpointWriter` must
be configured with the **same** `CheckpointId`.

`DbCheckpointStore<TPosition>` is a ready-made implementation over an EtlKit `IConnectionManager`
(`TableName`, `KeyColumn`, `PositionColumn`); positions are stored natively (`bigint` for `long`,
`text` for `string`).

### CheckpointWriter&lt;TInput, TPosition&gt;

Terminal destination that extracts the position from each (already durably written) record via a
`Position` selector and commits it:

```csharp
source.LinkTo(destinationTransform);          // your destination, modelled as a pass-through transform
destinationTransform.LinkTo(new CheckpointWriter<MyEvent, long>
{
    CheckpointStore = store,
    CheckpointId    = "my-consumer",
    Position        = e => e.StreamPosition,   // the position must be a field on the DTO
    // CommitInterval = TimeSpan.FromSeconds(1) // optional: debounce commits
});
```

> **Position must travel in the DTO.** The `Position` selector reads a field of the flowing record,
> so transforms must carry the position field through (don't drop or fan-in/aggregate it away).
> For PostgreSQL this is naturally the `StreamPosition` column. For MongoDB the position is the
> **resume token**, which is not a domain field — the `EventMapper` must surface it, e.g.
> `EventMapper = doc => new MyEvent(..., doc.ResumeToken.ToJson())`.

### Co-located commit (exactly-once)

When the destination and the checkpoint live in the same database, skip the `CheckpointWriter` and
call `ICheckpointStore.CommitAsync` inside the destination's own transaction. Commit and write then
succeed or fail atomically — effective exactly-once for that consumer.

## MongoChangeStreamSource (EtlKit.MongoDB)

`MongoChangeStreamSource<TOutput>` watches a MongoDB
[change stream](https://www.mongodb.com/docs/manual/changeStreams/) and emits one record per change
event.

### Requirements

- The MongoDB deployment must run in **replica set mode** (a single-node replica set is sufficient
  for development).
- NuGet package: `EtlKit.MongoDB`

### Basic usage

```csharp
using var tokenSource = new CancellationTokenSource();

var source = new MongoChangeStreamSource<MyEvent>
{
    MongoClient = new MongoClient("mongodb://localhost:27017/?replicaSet=rs0"),
    Database = "mydb",
    Collection = "orders",
    EventMapper = doc => new MyEvent
    {
        Id    = doc.FullDocument["_id"].AsObjectId.ToString(),
        Total = doc.FullDocument["total"].AsDouble,
    },
};

source.LinkTo(destination);
source.Execute(tokenSource.Token);
```

### Filtering with a pipeline

Pass a MongoDB aggregation pipeline to receive only the events you care about:

```csharp
var filterStage = new BsonDocumentPipelineStageDefinition<
    ChangeStreamDocument<BsonDocument>,
    ChangeStreamDocument<BsonDocument>
>(BsonDocument.Parse("{ $match: { 'fullDocument.status': 'shipped' } }"));

var pipeline = new EmptyPipelineDefinition<ChangeStreamDocument<BsonDocument>>()
    .AppendStage(filterStage);

var source = new MongoChangeStreamSource<MyEvent>
{
    MongoClient  = client,
    Database     = "mydb",
    Collection   = "orders",
    Pipeline     = pipeline,
    EventMapper  = doc => /* ... */,
};
```

### Resuming after restart

Set `CheckpointStore` + `CheckpointId` to resume, and surface the resume token in the output so a
`CheckpointWriter` can commit it (see [Checkpointing](#checkpointing-at-least-once-by-design)):

```csharp
var source = new MongoChangeStreamSource<MyEvent>
{
    MongoClient     = client,
    Database        = "mydb",
    Collection      = "orders",
    CheckpointStore = store,            // ICheckpointStore<string>
    CheckpointId    = "orders-consumer",
    EventMapper     = doc => new MyEvent(/* ... */, token: doc.ResumeToken.ToJson()),
};

source.LinkTo(destinationTransform);
destinationTransform.LinkTo(new CheckpointWriter<MyEvent, string>
{
    CheckpointStore = store,
    CheckpointId    = "orders-consumer",
    Position        = e => e.Token,
});
```

On restart the source loads the committed token and passes it to `Watch()` as `ResumeAfter`. The
source itself never commits — the `CheckpointWriter` does, after the destination, giving
at-least-once.

### Key properties

| Property | Default | Description |
|---|---|---|
| `MongoClient` | required | MongoDB client instance (caller manages lifetime) |
| `Database` | required | Database name |
| `Collection` | required | Collection name |
| `EventMapper` | required | Maps `ChangeStreamDocument<BsonDocument>` to `TOutput` (surface the resume token here for checkpointing) |
| `Pipeline` | `null` (all events) | Aggregation pipeline for server-side filtering |
| `FullDocument` | `UpdateLookup` | Controls which document version is returned on updates |
| `MaxAwaitTime` | 1 second | Max time the server waits for new events per batch |
| `CheckpointStore` | `null` (start from now) | `ICheckpointStore<string>` for resume tokens (load-only) |
| `CheckpointId` | required if `CheckpointStore` set | This consumer's checkpoint key |

---

## PostgresXminTailSource (EtlKit.PostgresStreaming)

`PostgresXminTailSource<TOutput>` continuously polls a PostgreSQL table for new rows using the
`xmin` system column as a visibility fence. It uses `pg_snapshot_xmin(pg_current_snapshot())` as a
read-safe frontier, which guarantees that rows from in-flight transactions are never emitted
prematurely — they appear only after their transaction commits.

### Requirements

- PostgreSQL 13 or later (uses `pg_current_snapshot()`).
- NuGet package: `EtlKit.PostgresStreaming`

### Basic usage

```csharp
using var tokenSource = new CancellationTokenSource();
using var cm = new PostgresConnectionManager("Host=localhost;Database=mydb;Username=etl;Password=secret")
{
    LeaveOpen = true,
};

var source = new PostgresXminTailSource<MyRow>
{
    ConnectionManager = cm,
    TableName         = "orders",
    Schema            = "public",
    OrderByColumns    = new[] { "id" },
    RowMapper         = r => new MyRow { Id = (long)r["id"], Name = (string)r["name"] },
};

source.LinkTo(destination);
source.Execute(tokenSource.Token);
```

### Resuming after restart

Set `CheckpointStore` (`ICheckpointStore<long>`) + `CheckpointId`, and commit downstream with a
`CheckpointWriter` (see [Checkpointing](#checkpointing-at-least-once-by-design)). The cursor column
must be a single monotone `bigint` that is exposed on the mapped row:

```csharp
var source = new PostgresXminTailSource<MyRow>
{
    ConnectionManager = cm,
    TableName         = "orders",
    Schema            = "public",
    OrderByColumns    = new[] { "stream_position" }, // monotone bigint; see note below
    CheckpointStore   = store,                       // ICheckpointStore<long>
    CheckpointId      = "orders-consumer",
    RowMapper         = r => new MyRow { Position = (long)r["stream_position"], /* ... */ },
};

source.LinkTo(destinationTransform);
destinationTransform.LinkTo(new CheckpointWriter<MyRow, long>
{
    CheckpointStore = store,
    CheckpointId    = "orders-consumer",
    Position        = r => r.Position,
});
```

On restart the source loads the committed position and resumes with `WHERE <cursor> > @position`.
The source never commits — the `CheckpointWriter` does, after the destination, giving at-least-once.

> **The cursor column must be write-stamped to catch UPDATEs.** A `xmin`-frontier cursor over an
> *immutable* key (a business key, or an insert-only id) is INSERT-only: an in-place UPDATE won't
> re-emit because the cursor already passed the row. To stream updates, point `OrderByColumns` at a
> column that is re-stamped with a monotone value on every write (e.g. a `bigint` filled by a
> server-side sequence on INSERT and re-stamped in the UPSERT's `DO UPDATE`). Use a **server-side**
> sequence, not an app-generated value: the cursor's order must match the database's transaction
> (`xid`) order, or the frontier can silently drop events under concurrent (multi-writer) ingest.

### Filtering rows

Use `AdditionalWhere` to apply a server-side predicate on top of the xmin fence:

```csharp
var source = new PostgresXminTailSource<MyRow>
{
    // ...
    AdditionalWhere = "status = 'pending'",
};
```

> **Security:** `AdditionalWhere` is interpolated verbatim into the polling SQL.
> Treat it like a hard-coded code fragment — never concatenate values that arrive
> from external input (HTTP requests, configuration files, untrusted message
> payloads), or you will introduce SQL injection. Use literal predicates only.

### Caveat: xmin wraparound

PostgreSQL's `xid`/`xmin` is a 32-bit transaction counter that wraps around every
~2 billion transactions. The xmin **frontier** is correct in the steady state
(it always reflects the current epoch), but a frontier-only resume across a
wraparound boundary is unsafe — old rows from a previous epoch may compare as
"newer than" the new frontier.

The source mitigates this by using `OrderByColumns` + `CheckpointStore`: the
checkpoint cursor is the durable progress marker, while the xmin frontier only
serves to exclude rows from currently-open transactions. If you need long-term
durability across potential wraparounds (e.g., very high-throughput databases
running for years without `VACUUM FREEZE`), make sure your `OrderByColumns` are
strictly monotonic application-level identifiers (BIGINT identity, timestamp,
ULID) and not derived from xmin itself.

### Key properties

| Property | Default | Description |
|---|---|---|
| `ConnectionManager` | required | Postgres connection manager |
| `TableName` | required | Table to poll (must expose the `xmin` system column) |
| `Schema` | `"public"` | Schema that contains the table |
| `OrderByColumns` | required | Columns used for ordering and cursor pagination |
| `RowMapper` | required | Maps `IDataRecord` to `TOutput` |
| `BatchSize` | 500 | Rows per polling round |
| `PollingInterval` | 1 second | Pause between rounds when no rows are found |
| `AdditionalWhere` | `null` | Extra SQL predicate appended with AND |
| `CheckpointStore` | `null` (start from beginning) | `ICheckpointStore<long>` for the cursor (load-only) |
| `CheckpointId` | required if `CheckpointStore` set | This consumer's checkpoint key |
