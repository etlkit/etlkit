# TODO

## Bugs & refactorings

### Future release

- New feature: Bounded Capacity for all Buffers (separately for every component besides
  `DataFlowBatchDestination` & general property in ConnectionManager), to restrict buffer size and max
  memory consumption
- After XML deserialization most of the components need to re-initialize internal TPL structures.
  This is handled inconsistently in different components. There needs to be a common method (similar
  to existing `InitObjects`) to be called after properties are initialized, but before execution
  starts.
- If not everything is connected to a destination when using predicates, it can be that the dataflow
  never finishes. Write some tests. See
  [Github project DataflowEx](https://github.com/gridsum/DataflowEx) for implementation how to
  create a predicate that always discards records not transferred.

## Update Documentation

- Improving Lookup with new set of attributes to define matching and retrieving properties. Also a
  new `Aggregation` component that simplifies creating aggregates (e.g. to calculate SUM, MIN, MAX
  or Count or any other custom defined calculation).
- All text files source (Csv, Json, Xml) now accept either a file path OR an URL which is loaded
  with a HttpClient.
- Excel source now skip blank lines

## Enhancements

- CreateTableTask.CreateOrAlter(): add functionality to alter a table (with migration if there is
  data in the table).
- CreateTableTask: Function for adding test data into table (depending on table definition)

## Tech Debt

- [XML Documentation Coverage — 59% → 95%](docs/tech-debt/TECH-DEBT-XML-Documentation-Coverage.md)
  - Phase 1: Core interfaces in EtlKit.Primitives (14 types)
  - Phase 2: Abstract base classes in EtlKit.Common + main library (13 types)
  - Phase 3: Fully undocumented projects — ClickHouse, Logging.Database (5 types)
  - Phase 4: Remaining main library gaps — enums, attributes, models, transforms (42 types)
- [FieldLookupTransformation — declarative field-name-based lookup with XML serialization support](docs/tech-debt/field-lookup-transformation-roadmap.md)
  - New component alongside `LookupTransformation` with serializable `MatchColumns`/`RetrieveColumns` POCO lists
  - `DictionarySource: IDataFlowSource<T>` property deserialized via existing `DataFlowXmlReader` mechanism (no reader changes)
  - Optional `ScriptedFieldLookupTransformation` in `EtlKit.Scripting` with Roslyn enrichment script string
- [PostgresLogicalReplicationSource — WAL/CDC streaming source](docs/tech-debt/TECH-DEBT-Postgres-Logical-Replication-Source.md)
  - Net-new source in `EtlKit.PostgresStreaming` over `Npgsql.Replication` (built-in `pgoutput`, no extension)
  - Complements (does not replace) `PostgresXminTailSource`: full ordered change log incl. DELETEs and every intermediate UPDATE, sub-second latency
  - Resume token = LSN via existing `ICheckpointStore`; deferred to V3+ per MLRSSL-1509 §5.8
- [Split `DataTypeConverter` driver conventions before moving type-mapping to Common](docs/tech-debt/TECH-DEBT-DataTypeConverter-Driver-Split.md)
  - Pure type-mapping → Common/Primitives; per-driver SQL-type conventions → driver packages behind a DI abstraction (drop the central `switch (ConnectionManagerType)`)
  - Unblocks moving `QueryParameter` to Common (and `ITableColumn` to Primitives); ride along with broader driver-package/DI modularization

## Other

- PrimaryKeyConstrainName now is part of TableDefinition, but not read from `GetTableDefinitionFrom`
- in order to have these tests fully working, add something like MaxBufferSize as DataFlow parameter
  for all DataFlowTasks and use this when creating DF components - also have a static
  DefaultMaxBufferSize as Fallback value
