# Tech Debt: align `KafkaSource` offset commits with the checkpoint model (at-least-once)

## Context

The streaming checkpoint model (`ICheckpointStore`, `CheckpointWriter<TInput, TPosition>`) commits a
consumer's position **only after** records have flowed through the whole pipeline and were durably
written by the destination. That yields at-least-once delivery: a crash between the destination
write and the commit replays records, never drops them. `PostgresXminTailSource` and
`MongoChangeStreamSource` follow this model, and MR !5 brought the **producer** side
(`KafkaTransformation`) in line with it — a row is re-emitted downstream only after its delivery
report confirms the write, so a downstream `CheckpointWriter` can never commit past an unconfirmed
message. The contract is pinned by `EtlKit.Kafka.Tests/KafkaTransformationCheckpointWriterTests`.

`KafkaSource` (the **consumer** side) is the remaining outlier.

## Problem

`KafkaSource.Execute` relies on the Confluent consumer's default offset management:
`enable.auto.commit` defaults to `true`, so offsets are committed on a background timer shortly
after `Consume()` returns — i.e. **at read time**, not after the pipeline has durably handled the
record. In checkpoint-model terms this is at-most-once:

- A crash after the auto-commit but before the destination write **loses** the in-flight records.
- Records diverted by conversion errors are also committed past, but that at least matches the
  explicit error-buffer opt-out semantics used elsewhere.

This is inconsistent with every other streaming source in the library, and it is silent — the user
gets weaker delivery guarantees from a Kafka pipeline than from an identical Postgres/Mongo one
without any signal in the API.

## Direction

Kafka already has a native checkpoint store — the consumer group offset — so the fix is not to bolt
`ICheckpointStore` onto `KafkaSource`, but to move the commit to the same place in the topology
where `CheckpointWriter` sits:

1. Disable auto-commit in `KafkaSource` (`EnableAutoCommit = false`,
   `EnableAutoOffsetStore = false`), unless the user explicitly configured otherwise.
2. Emit each record with its `TopicPartitionOffset` (an envelope or a required position selector,
   mirroring how `PostgresXminTailSource` exposes its `xmin` position).
3. Provide a terminal committer analogous to `CheckpointWriter` — e.g. a
   `KafkaOffsetCommitWriter` placed after the destination — that calls
   `IConsumer.StoreOffset`/`Commit` for the highest offset seen per partition, strictly forward,
   with a debounce interval like `CheckpointWriter.CommitInterval`.
4. Keep the current behaviour available as an explicit opt-in for users who prefer throughput over
   delivery guarantees, but make the at-least-once wiring the documented default path.

Open questions to resolve during design:

- Rebalance handling: on partition revocation the un-committed tail is replayed by the new assignee
  — fine for at-least-once, but the committer must not commit offsets for partitions it no longer
  owns.
- Sharing the `IConsumer` instance between the source (consume loop) and the committer (commit
  calls) across threads; librdkafka allows `Commit`/`StoreOffset` from another thread, but the
  lifecycle (dispose order) must be pinned by tests.
- Multiple partitions mean the position is per-`TopicPartition`, not a single monotone scalar, so
  `CheckpointWriter<TInput, TPosition>` itself cannot be reused as-is (`IComparable` over one
  cursor); the committer needs a per-partition max, same strictly-forward rule per key.

## References

- `EtlKit.Kafka/KafkaSource.cs` — consume loop, no explicit offset management today.
- `EtlKit.Common/DataFlow/CheckpointWriter.cs` — the commit-after-durability pattern to mirror.
- `EtlKit.Kafka.Tests/KafkaTransformationCheckpointWriterTests.cs` — producer-side contract tests;
  the consumer-side committer needs the equivalent suite.
