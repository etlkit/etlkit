# MongoChangeStreamSource: explicit start position and dead-cursor handling

- **Ticket:** [RSSL-11926](https://jira.rapidsoft.ru/browse/RSSL-11926)
- **Date:** 2026-08-07
- **Status:** design approved, ready for an implementation plan

## Problem

`MongoChangeStreamSource<TOutput>` (`EtlKit.MongoDB/MongoChangeStreamSource.cs`) has three defects
in how it opens and maintains its change-stream cursor.

### 1. Cold start drops events silently

`RunChangeStreamLoop` sets a start position only when a checkpoint token was loaded:

```csharp
var options = new ChangeStreamOptions { FullDocument = ..., MaxAwaitTime = ... };
if (resumeToken != null) { options.ResumeAfter = resumeToken; }
using var cursor = collection.Watch(pipeline, options, ct);
```

With no checkpoint, the cursor begins at the moment `Watch()` returns. Everything written between
process start and that moment is lost — no exception, no log entry. The window is not theoretical:
it spans connecting to the replica set and creating indexes, so it is measured in seconds.

The neighbouring `PostgresXminTailSource<TOutput>` has no such hole — on an empty checkpoint it reads
the table from the beginning. Two sources share the `ICheckpointStore` contract while offering
different cold-start guarantees, and the difference is documented nowhere.

### 2. No way to resume past an invalidate

MongoDB forbids resuming with `resumeAfter` once an `invalidate` event has been delivered (the
watched collection was dropped or renamed); `startAfter` exists precisely for that case. The source
only ever sets `ResumeAfter`, so a consumer holding a committed token is stuck permanently.

### 3. Spin on a closed cursor (suspected)

```csharp
while (!ct.IsCancellationRequested)
{
    while (cursor.MoveNext(ct)) { /* ... */ }
}
```

If `MoveNext` returns `false` the outer loop re-enters immediately, on the same cursor, with no
delay and no reopen. `MoveNext` returning `false` means the cursor is exhausted or closed — which is
what the server does after an invalidate, making this the same scenario as defect 2. Unconfirmed;
reproduction is part of the work.

## Scope

**In scope:** defects 1, 2 and 3 above.

**Out of scope — cursor readiness signal (ticket item 3).** The ticket proposed exposing a
"cursor is open" signal to replace fixed `Task.Delay` calls in tests and to serve health checks. It
is dropped, because it does not solve the problem it was proposed for:

- In production the writer is an external system that cannot observe our signal, so readiness cannot
  close the cold-start window there. Defect 1's fix can, and does — it works regardless of who
  writes.
- In tests the same fix removes the race *structurally* rather than narrowing it: a cursor seeded
  from a timestamp taken before the write covers that write no matter when the cursor physically
  opens. No delay, no handshake, no readiness signal.
- What would remain is a readiness probe and in-process startup ordering. Neither has a caller
  asking for it today.

`PostgresXminTailSource` is not touched at all: it has no cold-start race to signal around.

## Design

### A. Start-position properties

Three additions to `MongoChangeStreamSource<TOutput>`:

```csharp
/// Cold-start seed: start the change stream at this point in time. Ignored when a checkpoint
/// is found. Snap it with MongoChangeStreamPosition.Current — a client clock running ahead of
/// the deployment would reintroduce the very gap this closes.
public DateTimeOffset? StartAtOperationTime { get; set; }

/// Cold-start seed: start strictly after this resume token, in the same JSON form the
/// CheckpointWriter commits (doc.ResumeToken.ToJson()). Ignored when a checkpoint is found.
public string? StartAfter { get; set; }

/// How a token loaded from CheckpointStore is applied. StartAfter also resumes past an invalidate.
public ChangeStreamResumeMode CheckpointResumeMode { get; set; } = ChangeStreamResumeMode.ResumeAfter;
```

and a new enum in the same project:

```csharp
public enum ChangeStreamResumeMode { ResumeAfter, StartAfter }
```

**No driver types on the public surface.** `DateTimeOffset?`, `string?` and an owned enum — not
`BsonTimestamp` or `BsonDocument`. Conversion to driver types stays inside `RunChangeStreamLoop`,
next to the existing `BsonDocument.Parse`. `string` is also the type the rest of the resume path
already uses: the checkpoint is `ICheckpointStore<string>`, `LoadResumeToken` parses JSON, and the
documentation tells callers to surface `doc.ResumeToken.ToJson()`. Recovering from an invalidate is
therefore a copy of the stored token with no conversion.

This does not make the class serializable overall — `IMongoClient`, `PipelineDefinition<>` and the
`EventMapper` delegate remain. It only avoids adding leakage where none is needed.

The name `StartAtOperationTime` is kept despite the changed type: it maps directly onto the MongoDB
option `startAtOperationTime`, which is what callers will search for.

### B. Start-position resolution

Exactly one position is ever set on `ChangeStreamOptions` — the three server-side options are
mutually exclusive.

| # | Condition | Applied to `ChangeStreamOptions` |
|---|---|---|
| 1 | checkpoint token found | `ResumeAfter` or `StartAfter`, per `CheckpointResumeMode` |
| 2 | else `StartAfter` set | `StartAfter` |
| 3 | else `StartAtOperationTime` set | `StartAtOperationTime` |
| 4 | else | nothing — current behaviour, starts at cursor open |

The asymmetry is deliberate and is what makes both defects closeable with one change:

- `StartAtOperationTime` and `StartAfter` are **cold-start seeds**. A checkpoint outranks them, so a
  restart resumes from committed progress instead of replaying from a stale configured value.
- The **invalidate recovery** is `CheckpointResumeMode`, not a property that outranks the checkpoint.
  The caller flips one setting and the already-stored token resumes past the invalidate; the token
  never has to be lifted out into configuration.

### C. Validation

Both checks run at the top of `Execute`, before any connection work, and throw
`InvalidOperationException` naming the offending properties:

- `StartAfter` and `StartAtOperationTime` both set. The server would otherwise reject the `Watch`
  with a message that does not identify which EtlKit properties conflict.
- `StartAtOperationTime` outside the range a BSON timestamp can represent. The BSON timestamp packs
  seconds into a 32-bit field, so values before the Unix epoch or beyond 2038 cannot round-trip and
  must fail loudly rather than wrap.

A `StartAfter` string that is not valid JSON propagates `BsonDocument.Parse`'s `FormatException`
unchanged — it is already unambiguous.

### D. Conversion to `BsonTimestamp`

A BSON timestamp is `BsonTimestamp(int timestamp, int increment)`: seconds since the Unix epoch, and
an ordinal counting operations *within* that second. The increment is **not** a fraction of a
second — it is a counter the server assigns as operations occur.

So the conversion is `new BsonTimestamp((int)value.ToUnixTimeSeconds(), 0)`, and the sub-second part
of the `DateTimeOffset` is discarded. Not because `DateTimeOffset` lacks the precision — it holds
100-nanosecond ticks — but because there is no correct target for it. Deriving an increment from
milliseconds would assert "start after the *n*th operation of that second" on the strength of a wall
clock, which can skip operations that really happened: the exact defect being fixed.

**The seconds conversion must truncate downwards, never round to nearest.** Rounding up would place
the start after operations that already occurred and drop them.
`DateTimeOffset.ToUnixTimeSeconds()` floors, which is the required behaviour — the implementation
depends on it deliberately, and a test pins it.

The residual effect is that a stream can start up to one second before the snapped mark and replay
the events in that second. Under the library's documented at-least-once contract — duplicates
expected, losses not — erring early is the safe direction, and for a cold-start seed whose whole
purpose is "begin before anything I did" it is also the intended one. It must still be stated
explicitly in the user documentation.

### E. Cluster-time helper

```csharp
public static class MongoChangeStreamPosition
{
    /// Snapshots the deployment's current cluster time for use as StartAtOperationTime.
    public static DateTimeOffset Current(IMongoClient client, string database, CancellationToken ct = default);
}
```

This is the one addition beyond the ticket's letter. Without it the seed is a trap: the driver
exposes no direct API for snapping a cluster time, so callers would reach for
`DateTimeOffset.UtcNow`, and a client clock running ahead of the deployment restores exactly the
cold-start gap the seed is meant to close. Rounding to whole seconds does not mitigate skew. The
library's own tests need the helper regardless, so the only real question is whether it is public —
and it is the piece a consumer is most likely to get wrong.

The mechanism (a session's `OperationTime` after a command, or `operationTime` read from a command
reply) is settled by a round-trip test against a real replica set during implementation, not chosen
from memory.

### F. Dead-cursor handling

`MoveNext() == false` means the cursor is exhausted; re-entering it cannot help. The loop stops:

```csharp
while (cursor.MoveNext(ct)) { /* ... */ }
// The server closed the cursor (e.g. after an invalidate event — collection dropped or
// renamed). Re-entering would spin on a dead cursor. Stop and let the caller resume with
// CheckpointResumeMode.StartAfter.
```

`Execute`'s existing `finally` then completes the buffer and logs the end, and `Execute` returns
normally — the token was not cancelled, so no `OperationCanceledException`. Downstream observes
ordinary completion. Before breaking, the source logs at warning level through `Logger`, using the
same argument shape the `DataFlowTask` log helpers use (task name, task type, action, hash, stage,
load-process id), so a stream ending on its own is never silent.

The source does **not** reopen the cursor. Past an invalidate, resumption is only legal via
`startAfter`, and doing it implicitly would silently start reading a *new* collection that happens
to carry the old name. That is the caller's decision, expressed through `CheckpointResumeMode`.

The fix is correct whether or not reproduction succeeds; only the regression test depends on it.

## Testing

Integration tests against the existing `MongoContainerFixture`.

New:

- **Cold start from a snapped mark.** Snap cluster time, write documents, *then* start the source
  with `StartAtOperationTime` — assert the pre-start writes arrive. This is the test the ticket asks
  for, and it fails against today's code.
- **`StartAfter` seed** skips the event at the given token and delivers the following ones.
- **Checkpoint outranks the seeds:** with a committed checkpoint *and* a stale
  `StartAtOperationTime` set, the run resumes from the checkpoint.
- **Invalidate recovery:** run, commit a token, drop the collection, restart with
  `CheckpointResumeMode.StartAfter` — assert events after the drop arrive. Confirms the default
  `ResumeAfter` mode fails here first, so the test proves the mode does the work.
- **Conflicting seeds** throw `InvalidOperationException` before any connection is attempted.
- **Seconds conversion truncates downwards.** A deterministic unit test over the conversion — no
  container — asserting that a `DateTimeOffset` carrying a sub-second remainder maps to the *lower*
  whole second with increment 0. Rounding up here would silently drop events, and the integration
  tests only catch it when a write happens to land in the same second as the snap.
- **Closed cursor does not spin:** drop the watched collection and assert `Execute` returns within a
  budget instead of burning CPU until cancellation.

Changed: the four existing tests in `MongoChangeStreamSourceTests` drop their
`await Task.Delay(500)` in favour of a snapped `StartAtOperationTime`. The delays are the flakiness
the ticket reports upstream, and removing them is a direct demonstration that the fix works.

## Documentation

`docs/dataflow/streaming-sources.md`:

- A cold-start section for `MongoChangeStreamSource` covering the gap, the seed, and the helper —
  including the guarantee difference against `PostgresXminTailSource`, which the ticket flags as
  undocumented today.
- An invalidate section: what MongoDB forbids, and the `CheckpointResumeMode` recovery.
- The second-granularity replay note from section D.
- New rows in the key-properties table.

A changelog document under `docs/changelog/` per the repository's convention.

## Notes and risks

- The change is purely additive. Defaults reproduce today's behaviour exactly: no seeds set and
  `CheckpointResumeMode.ResumeAfter` give the current start-position logic unchanged.
- `startAfter` requires MongoDB 4.1.1 or later, while the driver supports 4.0. Callers on 4.0 that
  set `CheckpointResumeMode.StartAfter` receive a server-side error. This is why the mode is opt-in
  rather than the default, and it belongs in the documentation.
- Defect 3's reproduction may prove impossible if the driver's internal retry never surfaces a
  `false` from `MoveNext`. The fix ships regardless; the finding is reported back to the ticket.
