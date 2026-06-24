# Tech Debt: `PostgresLogicalReplicationSource<T>` — WAL/CDC streaming source

## Context

`EtlKit.PostgresStreaming.PostgresXminTailSource<T>` reads the tail of a table by **polling**
with an `xmin`-frontier (to fence in-flight transactions) plus a tuple cursor over caller-supplied
`OrderByColumns`. This is correct and cheap, but it is fundamentally a **snapshot-on-poll** model:
it emits the *current* visible version of each row whose cursor value advanced past the checkpoint.

Two consequences follow from the polling model:

1. **Intermediate versions are coalesced.** If a row is updated several times between two polls,
   the source sees only the latest version. For the originating use case (БДК `ContactsEvents`,
   MLRSSL-1509 / RSSL-11703) this is desirable — the trigger engine wants the latest state, not
   every keystroke. But it means the source cannot reconstruct a full change history.
2. **Deletes are invisible.** A polling SELECT cannot observe a row that no longer exists. There is
   no tombstone in the result set.

For scenarios that need the *complete* ordered stream of changes — every intermediate update,
deletes as first-class events, and sub-second latency — polling is the wrong tool. The proper
PostgreSQL primitive is **logical decoding** (write-ahead-log based CDC), which is the direct
analogue of what `MongoChangeStreamSource<T>` already gives us for MongoDB via Change Streams.

This document parks the design so it is not re-derived from scratch when the need arises.

## Relationship to the existing sources

| Capability                   | `PostgresXminTailSource` (polling) | `PostgresLogicalReplicationSource` (proposed)            | `MongoChangeStreamSource` |
| ---------------------------- | ---------------------------------- | -------------------------------------------------------- | ------------------------- |
| INSERTs                      | ✅                                  | ✅                                                        | ✅                         |
| UPDATEs                      | ✅ *(if cursor column re-stamps)*¹  | ✅ (every update)                                         | ✅ (replace/update)        |
| Intermediate update versions | ❌ coalesced to latest              | ✅ each one                                               | ✅ each one                |
| DELETEs                      | ❌                                  | ✅ (with `REPLICA IDENTITY`)                              | ✅                         |
| Latency                      | ~`PollingInterval`                 | sub-second (server push)                                 | ~`MaxAwaitTime`           |
| Server-side prerequisites    | none                               | `wal_level=logical`, slot, publication, REPLICATION priv | replica set               |
| Operational footprint        | low                                | medium (slot/WAL retention monitoring)                   | low                       |
| Resume token                 | cursor values (our JSON)           | LSN (opaque string)                                      | BSON resume token         |

¹ The polling source catches UPDATEs only when `OrderByColumns` points at a column that is
re-stamped on every write (a write-stamped monotone key such as a `RowVersion`), not at an
immutable business key. See `docs/dataflow/streaming-sources.md`.

The two PostgreSQL sources are **complementary**, not a replacement. The polling source stays the
default for "give me new/changed rows, latest wins"; the logical-replication source is for
"give me the exact ordered change log including deletes".

## Design

Build on Npgsql's built-in replication support — no third-party dependency, no server-side
extension install (use the built-in `pgoutput` plugin, not `wal2json`).

- `Npgsql.Replication.LogicalReplicationConnection`
- `Npgsql.Replication.PgOutput.PgOutputReplicationSlot` + `PgOutputReplicationOptions`
- The connection must be opened with the `replication=database` parameter.

Consumption loop (mirrors the Mongo source's shape — long-running, resumable, single-flight):

```csharp
await using var conn = new LogicalReplicationConnection(connectionString);
await conn.Open(ct);

var slot    = new PgOutputReplicationSlot(SlotName);
var options = new PgOutputReplicationOptions(PublicationName, protocolVersion: 2);

await foreach (var message in conn.StartReplication(slot, options, ct))
{
    switch (message)
    {
        case InsertMessage ins:  Emit(MapInsert(ins));   break;
        case UpdateMessage upd:  Emit(MapUpdate(upd));    break; // old image needs REPLICA IDENTITY FULL
        case DeleteMessage del:  Emit(MapDelete(del));    break;
        case CommitMessage cmt:  SaveCheckpoint(cmt.CommitLsn); break;
    }
    // Acknowledge so the server can recycle WAL behind us.
    conn.SetReplicationStatus(message.WalEnd);
}
```

Proposed public surface (parallels `MongoChangeStreamSource<T>`):

```csharp
public class PostgresLogicalReplicationSource<TOutput> : DataFlowSource<TOutput>
{
    public string ConnectionString { get; set; }   // must include replication=database
    public string SlotName { get; set; }            // pre-created via DDL, or auto-create on first run
    public string PublicationName { get; set; }
    public ICheckpointStore? CheckpointStore { get; set; }  // stores the last committed LSN
    public Func<ReplicationMessage, TOutput?> EventMapper { get; set; }  // null => skip this message
}
```

The resume token is the LSN — a 64-bit, strictly monotone, gap-free WAL position. It maps cleanly
onto the existing `ICheckpointStore` (opaque string payload). No `xmin` frontier and no wraparound
concern: LSN is the natural cursor.

## Server-side prerequisites

```ini
# postgresql.conf (requires restart)
wal_level = logical
max_replication_slots = 10      # default 10
max_wal_senders = 10            # default 10
```

```sql
ALTER ROLE etl_user REPLICATION;
CREATE PUBLICATION my_pub FOR TABLE my_table;
ALTER TABLE my_table REPLICA IDENTITY FULL;  -- or DEFAULT (PK only) if before-image isn't needed
```

The replication slot can be created by the client API on first run or provisioned via a migration.

## Operational concerns (the real cost)

1. **Slot/WAL retention.** A replication slot pins WAL until the consumer acknowledges. If the
   consumer is down or lagging, WAL accumulates and can fill the disk. Monitor
   `pg_replication_slots.confirmed_flush_lsn` lag and alert on it. This is the single biggest
   operational difference from the polling source, which has zero server-side state.
2. **Initial sync.** A slot streams changes from its creation point forward; pre-existing rows are
   not replayed. Pair it with a one-time bootstrap load (the existing `PostgresXminTailSource` is a
   natural fit for the initial backfill) or create the slot before traffic starts.
3. **Long-lived connection.** The replication connection is a persistent TCP stream; the source must
   implement reconnect-with-last-LSN on network blips (MongoDB's driver does this internally for
   Change Streams; for Npgsql we handle it explicitly).
4. **Privileges.** `REPLICATION` is a powerful role attribute; in locked-down environments it may
   require a dedicated account and review.

## When to build this

Deferred until a concrete consumer needs full change history, deletes, or sub-second latency.
In the MLRSSL-1509 roadmap (§5.8) this lands in the **V3+** bucket ("Debezium / wal2json (CDC)");
the V1/V2 needs are met by `PostgresXminTailSource` (polling) and, for low-latency push, PostgreSQL
`LISTEN/NOTIFY` layered on top of polling.

No work is required on the existing components to enable this — it is a net-new, additive source in
`EtlKit.PostgresStreaming`.
