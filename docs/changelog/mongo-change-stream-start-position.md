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
