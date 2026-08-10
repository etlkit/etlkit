# MongoChangeStreamSource: explicit start position and dead-cursor handling

> **Status: COMPLETED** (2026-08-08) — RSSL-11926

## Problem

`MongoChangeStreamSource<TOutput>` set a change stream start position only when a checkpoint token
was loaded:

```csharp
var options = new ChangeStreamOptions { FullDocument = ..., MaxAwaitTime = ... };
if (resumeToken != null) { options.ResumeAfter = resumeToken; }
using var cursor = collection.Watch(pipeline, options, ct);
```

Without a checkpoint the cursor began wherever `Watch()` landed, and everything written between
process start and that moment was lost — no exception, no log entry. The window covers connecting to
the replica set and any startup work, so it is measured in seconds, not milliseconds.

The neighbouring `PostgresXminTailSource` has no such hole: on an empty checkpoint it reads the table
from the beginning. The two sources shared the `ICheckpointStore` contract while offering different
cold-start guarantees, and the difference was documented nowhere.

Two further defects followed from the same code:

- Only `resumeAfter` was ever set, so a consumer could not resume past an `invalidate` event (the
  watched collection dropped or renamed), which MongoDB permits only via `startAfter`.
- `while (cursor.MoveNext(ct))` sat inside an outer `while (!ct.IsCancellationRequested)`. Once the
  server closed the cursor, the outer loop re-entered it immediately, with no delay and no reopen — a
  full-CPU spin until cancellation.

The spin was listed in the ticket as a suspicion, not a confirmed defect, on the grounds that the
driver retries resumable errors internally and the branch might be unreachable. **It reproduced.**
Before the fix, the new regression test failed after 15 s with `Execute did not return after the
server closed the cursor — the outer loop is spinning.` The driver does surface `MoveNext == false`
after an invalidate.

## Fix

Three additive properties on `MongoChangeStreamSource<TOutput>`:

| Property | Type | Default | Role |
|---|---|---|---|
| `StartAtOperationTime` | `DateTimeOffset?` | `null` | Cold-start seed: point in time to start from |
| `StartAfter` | `string?` | `null` | Cold-start seed: resume token (JSON) to start strictly after |
| `CheckpointResumeMode` | `ChangeStreamResumeMode` | `ResumeAfter` | How a stored token is applied |

MongoDB treats `resumeAfter`, `startAfter` and `startAtOperationTime` as mutually exclusive, so
exactly one ever reaches `ChangeStreamOptions`:

1. checkpoint token found → `ResumeAfter` or `StartAfter`, per `CheckpointResumeMode`
2. else `StartAfter` set → `StartAfter`
3. else `StartAtOperationTime` set → `StartAtOperationTime`
4. else nothing (previous behaviour)

Contradictory configurations throw `InvalidOperationException` before any connection is attempted —
both seeds set at once, or a `StartAtOperationTime` outside the range a BSON timestamp's 32-bit
seconds field can represent. The check runs *inside* `Execute`'s `try`, so the `finally` still
completes the buffer; otherwise a linked destination would wait forever on a pipeline that never
started.

`MongoChangeStreamPosition.Current` snapshots the deployment's cluster time so callers do not reach
for a client clock — skew there would restore the gap being closed.

The read loop's outer `while` was removed: `MoveNext` returning `false` means the cursor is
exhausted, so the source logs a warning and stops.

## Design decisions

**The seeds are cold-start only; the checkpoint outranks them.** A restart must resume from real
progress rather than replaying from a value left behind in configuration. That rules out letting a
configured seed win. But invalidate recovery needs the *stored* token applied differently, which is
why it goes through `CheckpointResumeMode` instead of a fourth property — the token never has to be
lifted out of the store and into config.

**No driver types on the new public surface.** `DateTimeOffset?`, `string` and an owned enum, not
`BsonTimestamp`/`BsonDocument`. Conversion stays internal. `string` is also what the rest of the
resume path already uses: the checkpoint is `ICheckpointStore<string>`, the loader parses JSON, and
the documentation tells callers to surface `doc.ResumeToken.ToJson()`. Recovering from an invalidate
is therefore a copy of the stored token with no conversion. (This does not make the class
serializable overall — `IMongoClient`, `PipelineDefinition<>` and the `EventMapper` delegate remain.
It avoids adding leakage where none is needed.)

**Second granularity is deliberate, and truncates downwards.** A BSON timestamp is (seconds,
ordinal-within-that-second); the ordinal is a server-assigned operation counter, not a fraction, so
a `DateTimeOffset`'s sub-second remainder has no correct target. Deriving an ordinal from
milliseconds would assert "start after the *n*th operation of that second" on the strength of a wall
clock and could skip operations that really happened — the exact defect being fixed. The remainder is
therefore discarded downwards: a stream may start up to a second early and replay those events, which
under the documented at-least-once contract is the safe direction, and for a seed meaning "begin
before anything I did" also the intended one. A dedicated unit test pins the truncation direction,
because rounding up would silently drop events and the integration tests only catch it when a write
lands in the snapshotted second.

**The source does not reopen the cursor after an invalidate.** Resuming there means reading a *new*
collection that reuses the old name. That is the caller's decision, expressed through
`CheckpointResumeMode`.

## What `startAfter` does and does not do

Established empirically while writing the recovery test, and the most surprising thing about this
feature:

`startAfter` widens which tokens MongoDB **accepts** as a starting point. It does **not** make a
stream skip an `invalidate` it replays into. Recovery therefore works only if the checkpoint holds
the `invalidate` event's **own** token — a token from before the drop replays `drop` → `invalidate`,
the cursor closes again, and a consumer that simply restarts loops forever.

Two consequences land on the caller, both documented in
[`docs/dataflow/streaming-sources.md`](../dataflow/streaming-sources.md):

- The `EventMapper` must tolerate an event with no `FullDocument` — `drop` and `invalidate` carry
  none, so a bare `doc.FullDocument["name"]` throws.
- That mapped record must reach the downstream `CheckpointWriter`. The source never commits its own
  position, so a pipeline filtering out non-insert events will never checkpoint past the invalidate.

## Not done: a cursor-readiness signal

The ticket also proposed exposing a "cursor is open" signal, to replace fixed `Task.Delay` waits in
tests and to serve health checks. It was dropped, because it does not solve the problem it was
proposed for:

- In production the writer is an external system that cannot observe our signal, so readiness cannot
  close the cold-start window there. `StartAtOperationTime` can, and does, regardless of who writes.
- In tests the same seed removes the race *structurally* rather than narrowing it: a cursor started
  from a mark taken before the write covers that write no matter when the cursor opens.
- What would remain is a readiness probe and in-process startup ordering, with no caller asking for
  either today.

`PostgresXminTailSource` was left untouched for the same reason — it has no cold-start race to signal
around.

## Behaviour compatibility

Fully additive. With no seed set and `CheckpointResumeMode` at its default, resolution produces
exactly the previous `ChangeStreamOptions`.

`startAfter` requires MongoDB 4.1.1 or later while the driver supports 4.0, which is why the mode is
opt-in rather than the default.

## Tests

`EtlKit.MongoDB.Tests` went from 4 tests to 18: cold start from a snapshotted mark, `StartAfter`
seed, checkpoint-outranks-seed precedence, both validation failures plus buffer completion on a
rejected configuration, the truncation-direction unit test, closed-cursor behaviour, and invalidate
recovery.

The four pre-existing tests dropped their fixed `await Task.Delay(500)` cursor-readiness waits in
favour of a snapshotted start position. Those delays were the flakiness this ticket was raised from.

A `[CollectionDefinition("MongoDB")]` was added: the existing `[Collection("MongoDB")]` attribute had
no matching definition and was inert, so every test class was starting its own container.
