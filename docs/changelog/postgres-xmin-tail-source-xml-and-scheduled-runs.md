# PostgresXminTailSource: usable from XML packages and from a schedule

> **Status: COMPLETED** (2026-09-16) — RSSL-12061

## Problem

Four ETL packages exporting `ContactsEvents` to ClickHouse re-implemented a tail read by hand: a
plain `DbSource` with `where "Id" > (select "LastId" …) order by "Id"` and **no `LIMIT`**, plus a
home-made checkpoint (`Aggregation(Max)` → `DbMerge`). Running once a minute, each package made the
planner scan a 56 GB table end to end, saturating the disk and pushing unrelated readers of the same
table past their 30-second command timeout (RSSL-12059).

`PostgresXminTailSource` already does exactly this correctly — batched with `LIMIT`, keyset cursor
over `OrderByColumns`, `xmin` frontier, `ICheckpointStore` with at-least-once commits. It could not
be adopted, because those packages are defined in XML and executed by a runtime that instantiates
components by name through `DataFlowXmlReader`. Three things stood in the way:

1. **The source was invisible to the reader.** `DataFlowXmlReader` builds its type cache from types
   implementing `IDataFlowLinkSource<>` / `IDataFlowLinkTarget<>` / `IDataFlowTransformation<,>`.
   `PostgresXminTailSource<TOutput>` derived from `DataFlowSource<TOutput>` but — unlike `DbSource`,
   `MemorySource` and `CustomSource` — never declared `IDataFlowSource<TOutput>`. The base class is
   where the linking machinery lives, so the component worked perfectly in code and simply did not
   exist as far as the reader was concerned: `Could not find type by name 'PostgresXminTailSource'`.
   `MongoChangeStreamSource<TOutput>`, the other streaming source, had the same omission.

2. **`RowMapper` was mandatory.** It is a `Func<IDataRecord, TOutput>`, and a delegate cannot be
   written in XML. `DbSource` has long had a dynamic fallback that copies every column onto an
   `ExpandoObject` by name; the tail source did not.

3. **The run never ended.** The source polls until cancelled, which is right for a resident service
   and wrong for a package started by a scheduler every minute — there is nobody to cancel it.

4. **The source fought the rest of the flow for one connection.** This only surfaced once a real
   XML package ran end to end. `DataFlowXmlReader` hands out **one connection manager per connection
   string**, shared by the source, the destination and the checkpoint store. A manager is not
   shared-safe: with the default `LeaveOpen` its `Open()` *closes and replaces* the underlying
   connection. Every other database component in EtlKit knows this and works on
   `CloneIfAllowed()` (`DbTask`, `DbDestination`, `DbRowTransformation`) — but
   `PostgresXminTailSource` and `DbCheckpointStore` used the shared instance directly. The store
   commits a position per record while the source is polling, so it pulled the connection out from
   under the source's open reader: `Received backend message BindComplete while expecting
   ReadyForQueryMessage`. It is a race, so it passed as often as it failed.

## Fix

Three additive changes, all backward compatible:

- `PostgresXminTailSource<TOutput>` and `MongoChangeStreamSource<TOutput>` now declare
  `IDataFlowSource<TOutput>` explicitly, matching every other source, which puts them in the XML
  reader's type cache. No behaviour change in code-defined flows.

- `RowMapper` became optional (`Func<IDataRecord, TOutput>?`) and defaults, for `ExpandoObject`
  output, to the same column-name-keyed mapping `DbSource` performs. The `_xmin_val` frontier alias
  the polling query selects alongside the table's columns is left off the row, so a destination sees
  the table's own shape. For any other output type an unset mapper now fails with an explicit
  message instead of a `NullReferenceException` on the first row.

- `StopWhenEmpty` (default `false`) ends the flow after the first polling round that returns no
  rows, instead of sleeping `PollingInterval`. Each scheduled run drains whatever accumulated since
  the last one and terminates on its own; the checkpoint is untouched, so the next run resumes where
  the previous one committed.

- `PostgresXminTailSource` and `DbCheckpointStore` now take `CloneIfAllowed()` of their connection
  manager, like every other database component. The source additionally drains a batch into memory
  and releases the reader **before** emitting any row downstream — holding a reader open across
  `SendAsync` is what let a shared connection be swapped mid-iteration. `BatchSize` bounds what is
  held, which is the point of batching in the first place.

Nothing else was needed: the non-generic `CheckpointWriter` / `DbCheckpointStore` (with
`PositionColumn` instead of a `Position` delegate) already close the generic parameters XML cannot
supply, and `DataFlowXmlReader.GetType` already resolves interface-typed properties such as
`CheckpointStore` across the loaded assemblies. **`DataFlowXmlReader` is unchanged.**

## Result

A tail read with checkpointing is now expressible with no compiled code at all. The acceptance test
(`XmlPackageTailReadTests`) runs this package against a real PostgreSQL container: it reads the tail
in batches of two, terminates without a cancellation token, writes through
`SqlCommandTransformation`, and commits the position only afterwards — a second run resumes past the
committed position instead of replaying, and a run with nothing pending still terminates and leaves
the checkpoint where it was.

```xml
<PostgresXminTailSource>
  <TableName>ContactsEvents</TableName>
  <Schema>cdb1</Schema>
  <OrderByColumns><Column>StreamPosition</Column></OrderByColumns>
  <AdditionalWhere>"EventSourceSystem" = 'cj-cxd-platform'</AdditionalWhere>
  <BatchSize>500</BatchSize>
  <StopWhenEmpty>true</StopWhenEmpty>
  <CheckpointId>export-cj</CheckpointId>
  <CheckpointStore type="DbCheckpointStore"> … </CheckpointStore>
  <LinkTo>
    <SqlCommandTransformation>
      …
      <LinkTo>
        <CheckpointWriter>
          <CheckpointId>export-cj</CheckpointId>
          <PositionColumn>StreamPosition</PositionColumn>
          <CheckpointStore type="DbCheckpointStore"> … </CheckpointStore>
        </CheckpointWriter>
      </LinkTo>
    </SqlCommandTransformation>
  </LinkTo>
</PostgresXminTailSource>
```

The full example and the surrounding guidance are in
[`docs/dataflow/streaming-sources.md`](../dataflow/streaming-sources.md).

## Consumers

The package must sit next to the host that executes such a package — the XML reader resolves
component names by scanning the assemblies in the application directory, so a missing
`EtlKit.PostgresStreaming` surfaces at run time as "could not find type by name", not as a build
error. Wiring it into the ContactDatabase ETL host is RSSL-12062; switching the four packages over
is BEREKECXD-1015.
