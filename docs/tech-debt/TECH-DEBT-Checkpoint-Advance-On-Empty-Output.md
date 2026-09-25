# Tech Debt: advance the checkpoint when a batch produces no output rows

## Context

In the streaming checkpoint model (`ICheckpointStore`, `CheckpointWriter<TInput, TPosition>`) a
source keeps an ephemeral in-memory cursor and never writes the durable position itself. The commit
happens in a terminal `CheckpointWriter`, after the destination has persisted the row. That is what
makes the model at-least-once, and `PostgresXminTailSource` says so at the point where the cursor
advances:

```csharp
// Advance the ephemeral in-memory read cursor for the next batch. The durable
// checkpoint is NOT written here — a downstream CheckpointWriter commits it after
// the destination persists (at-least-once). See ICheckpointStore.
cursor = lastCursorValues;
```

The durable position is therefore derived from rows that reached the end of the pipeline.

## Problem

A record that produces **no output rows** never moves the durable position, because nothing carrying
its position ever reaches the writer. Two ordinary pipeline shapes do this:

- a row-multiplying step returns an empty set for that record — `SqlQueryTransformation` whose query
  matches nothing, `RowMultiplication` whose function yields no items;
- a filter downstream of the source drops the row.

Those records are re-read on every subsequent run until a later record does produce output and drags
the position past them. Nothing fails and nothing is lost, so the only symptom is repeated work:
while the tail of the stream consists of such records, each run re-reads the same span.

The effect is bounded in a resident consumer, which keeps its in-memory cursor and only pays the
price after a restart. It is unbounded in a scheduled bounded run (`StopWhenEmpty`), where the
in-memory cursor dies with the run and the next run starts again from the last durable position.

### Where it surfaced

A scheduled package exports contact events into ClickHouse, expanding each event into one row per
factor with `jsonb_array_elements_text`. An event without factors yields no rows, so the position
does not move, and the next run re-reads it. With `StopWhenEmpty` the source still reaches the end
of the stream within a run, so the first event that does have factors commits a position past all of
them — the pipeline does not stall, it just repeats the read. See BEREKECXD-1071.

## Direction

After the dataflow completes successfully, let the terminal `CheckpointWriter` commit the position
the **source** has read up to, if that position is ahead of the last one committed from a row.

The commit belongs in the writer, not in the source. A source finishes posting rows long before the
destination has persisted them, so a source-side commit would report progress for records still in
flight and break the at-least-once guarantee. The writer completes only once everything downstream
of it has completed, so at that moment "read" and "persisted" coincide.

Shape of the change:

1. Streaming sources expose the position they reached — a property, or a small interface implemented
   by `PostgresXminTailSource` and `MongoChangeStreamSource`. The source publishes it **only when it
   left the polling loop because the stream was drained** (`StopWhenEmpty`), and leaves it unset on
   cancellation. That is the signal the writer acts on; see "Why the source has to say it" below.
2. `CheckpointWriter` gains an opt-in property that turns this on, deserialized from XML the same
   way `CheckpointStore` already is.
3. The commit is forward-only and never moves the position backwards. A faulted run commits nothing
   beyond what its rows delivered: a fault reaches the writer through `CheckCompleteAction` →
   `TargetBlock.Fault`, and `CleanUp` runs in `finally` either way, so
   `TargetAction.Completion .IsFaulted` is enough to tell.
4. It stays opt-in: in a pipeline that branches below the source, "no rows at this writer" does not
   mean "nothing left to do", and silently committing past those records would be wrong.

### Why the source has to say it

The writer cannot tell a cancelled run from a successful one by its own completion. Both sources
complete the buffer in a `finally` and only then rethrow:

```csharp
try { RunPollingLoop(cancellationToken); }
finally { Buffer.Complete(); LogFinish(); }
cancellationToken.ThrowIfCancellationRequested();
```

So on cancellation everything below the source completes normally, exactly as it does on a clean
run, and the writer would happily commit a position the flow never finished processing. Hence the
condition is not "the flow completed" but "the source reached the end of the stream", and only the
source knows that.

### The two positions must live in the same space

The source's cursor is a tuple over `OrderByColumns` (`object?[]`), while the checkpoint store is
typed `ICheckpointStore<long>` and `LoadCursor` seeds a **single-column** tuple from it. The
writer's position comes from a row field via `Position` / `PositionColumn` — a value that has
already been through the transformations between source and writer.

These coincide only when `OrderByColumns` holds exactly one column and the writer reads its
`PositionColumn` from that same column. Otherwise the writer would store a value from a different
space, and the next run could resume ahead of data it never processed. The contract has to state
this, and the implementation has to check it rather than assume it — today nothing does, and even
plain resume is already implicitly single-column.

### Scope and tests

Scoping the first iteration to `StopWhenEmpty` keeps the semantics simple — the source drained the
stream, so everything it emitted has landed. A resident consumer needs safe commit points while the
flow is still running (buffer drained, nothing in flight), which is a harder problem and can follow.

Tests to pin the contract: a record that yields no output rows leaves the checkpoint past it once
the run drains the stream; a faulted run and a cancelled run do not advance it; a repeated run never
moves it backwards; a configuration whose `OrderByColumns` and `PositionColumn` disagree is
rejected.

## References

- `EtlKit.PostgresStreaming/PostgresXminTailSource.cs` — in-memory cursor, no durable commit.
- `EtlKit.Common/DataFlow/CheckpointWriter.cs` — where the commit happens today.
- `EtlKit.Common/DataFlow/Streaming/DbCheckpointStore.cs` — the store the position is written to.
- `docs/tech-debt/TECH-DEBT-KafkaSource-Offset-Commit-Alignment.md` — the neighbouring gap on the
  Kafka consumer side; both are about where in the topology a position may safely be committed.
