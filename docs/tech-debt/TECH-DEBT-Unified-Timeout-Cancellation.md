# Tech Debt: unified timeout and cancellation approach across sources and transformations

## Context

While reviewing the Kafka delivery-error fix (MR !5, ticket RSSL-11867) a design question came up:
should `KafkaTransformation` lower the effective `MessageTimeoutMs` default (from librdkafka's
300000 ms to 30000 ms) so that an unreachable broker fails fast instead of stalling the pipeline for
five minutes?

Answering that required looking at how the rest of the library already handles "how long do we wait
on an external resource before giving up". The result: there is **no shared convention**, the three
subsystems each do something different, and the nearest thing to a convention (databases) points in
the opposite direction from a short fail-fast timeout. This note captures the inconsistency so a
single deliberate approach can be designed later, rather than each component inventing its own.

## Current state

| Subsystem | Where | Timeout mechanism | Effective default |
|-----------|-------|-------------------|-------------------|
| **Database (command)** | `DbConnectionManager.cs:130` | `cmd.CommandTimeout = 0` | **0 = infinite** — wait forever. Hardcoded, not configurable. |
| **Database (bulk copy)** | `SqlConnectionManager.cs:44` | `bulkCopy.BulkCopyTimeout = 0` | **0 = infinite** — wait forever. Hardcoded, not configurable. |
| **REST** | `SampleHttpClient.cs:9`, `RestTransformation.cs`, `RestMethodInfo.cs:30` | No timeout set on `HttpClient`; resilience is retry-based via `RetryCount` + `RetryInterval` (seconds) | `HttpClient` default (~100 s); `RetryCount`/`RetryInterval` both default to `0` |
| **Kafka** (proposed in MR !5) | `KafkaTransformation.cs` | `ProducerConfig.MessageTimeoutMs ??= 30000` (overrides librdkafka's 300000) + `MaxUnconfirmedMessages` bound | 30 s — a silent override of the client's own well-known default |

Related but distinct: `PostgresXminTailSource.PollingInterval` (`= 1 s`) and
`MongoChangeStreamSource.MaxAwaitTime` (`= 1 s`) are streaming **poll cadences**, not
failure/wait budgets — they should not be conflated with the timeouts above.

## The problem

1. **No shared default and no shared concept.** Three subsystems use three different notions —
   `CommandTimeout` (per-command budget, `0` = infinite), `MessageTimeoutMs` (delivery budget
   including retries), and retry count/interval on top of `HttpClient`'s own timeout. There is no
   `EtlKit`-wide constant, interface, or convention to align to.

2. **The DB convention is "hang forever", and it is not even configurable.** `CommandTimeout = 0`
   and `BulkCopyTimeout = 0` mean a slow or stuck database blocks a pipeline indefinitely with no
   user-facing knob to bound it. This is the same "hangs on an unreachable resource" failure mode we
   are trying to avoid for Kafka — already present, and worse (no escape hatch).

3. **Cancellation is threaded but unused.** `DataFlowSource.Execute(CancellationToken)` exists
   (`DataFlowSource.cs`), and `Execute()`/`ExecuteAsync()` pass a token through, but components
   ignore it (e.g. `MemorySource` never observes cancellation). So even where a caller wants to
   abort a stalled pipeline, there is no honored mechanism.

4. **Ad-hoc per-component decisions drift.** Because there is no policy, each new component picks its
   own behavior. The Kafka MR silently overriding the client default (30 s) is one example: it is
   inconsistent with the DB "rely on the driver / don't impose a timeout" stance, and it can turn
   recoverable transient broker blips into hard failures.

## Proposed direction

The consistent principle that already emerges from DB and REST is **not** "the same number
everywhere" — it is:

> Do not silently override the underlying client's timeout/retry defaults. Expose the relevant knob,
> keep the client's own default unless the user sets otherwise, and make "wait indefinitely"
> observable rather than accidental.

Concrete plan (deferred; should be done as one coherent pass, not piecemeal):

1. **Define the vocabulary.** Decide the small set of timeout/cancellation concepts EtlKit exposes
   and how they map onto each driver's native mechanism (DB `CommandTimeout`, Kafka
   `MessageTimeoutMs`, HTTP `HttpClient.Timeout` + retries). Do not force one numeric value across
   semantically different budgets.

2. **Stop silent overrides.** For Kafka (MR !5), do not hardcode 30 s — keep librdkafka's default and
   let the user configure `MessageTimeoutMs`. Document that an unreachable broker will wait
   `MessageTimeoutMs`, and consider a warning log when it is left unset. This matches the DB/REST
   "defer to the client" stance.

3. **Make DB timeouts configurable.** Expose `CommandTimeout` / `BulkCopyTimeout` on the DB
   components (default may stay `0` = infinite for backward compatibility) so long-running ETL
   queries still work but a caller can bound them.

4. **Honor cancellation end-to-end.** Actually observe the `CancellationToken` already flowing
   through `Execute`/`ExecuteAsync` inside sources, transformations, and blocking waits (including
   the Kafka confirm stage's delivery wait), so a caller can abort a stalled pipeline regardless of
   any per-driver timeout.

5. **Document the policy** in the data-flow docs so new components follow it instead of reinventing.

## Non-goals

- A single global "EtlKit timeout" number applied to every component — the budgets are not the same
  concept (a 30 s cap on a DB command would break legitimate long ETL queries, which is exactly why
  `CommandTimeout` is `0` today).
- Rewriting the REST retry model into a timeout model.

## Relation to MR !5 (RSSL-11867)

The Kafka fix itself (produce/confirm split so a delivery failure surfaces in `Destination.Completion`
or the error buffer) is sound and should proceed. Only the **timeout-default override** is entangled
with this broader inconsistency: per this note, prefer leaving `MessageTimeoutMs` at the client
default in MR !5 and folding any unified timeout/cancellation work into the plan above rather than
into the bug fix.
