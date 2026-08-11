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
- [UseRowAccessor mode for ScriptedRowTransformation](docs/tech-debt/TECH-DEBT-ScriptedTransformation-UseRowAccessor.md)
  - Fixes a real bug: scripts with Roslyn warnings only (e.g. CS0472) are incorrectly rejected outright
  - Opt-in `Row.Field` accessor sidesteps the compile errors that null/missing fields currently cause, which today produce a silently-null output instead
- [EtlKit.DynamicLinq AssemblyLoadContext unloading](docs/tech-debt/TECH-DEBT-DynamicLinq-AssemblyLoadContext.md)
  - Multi-target to `net6.0` and wrap `DynamicClassFactory.CreateType` in a collectible ALC, with eviction added to `ExpandoTypeMapper._fastPathCache`
  - Deferred until it can land together with the sibling `ScriptBuilder` ALC work
- [Expression Engine Unification — Roslyn vs Dynamic LINQ follow-up](docs/tech-debt/TECH-DEBT-Expression-Engine-Unification.md)
  - Package split (`EtlKit.Scripting` vs `EtlKit.DynamicLinq`) already shipped; remaining: audit real `ScriptedRowTransformation` usage, build `ExpressionRowTransformation<TInput,TOutput>`, then decide keep-both vs. drop-one
- [XML documentation: 2 known gaps left after the coverage initiative](docs/changelog/TECH-DEBT-XML-Documentation-Coverage.md)
  - `EtlKit.Scripting` — 2 undocumented types, out of scope for all 4 phases
  - `EtlKit.Common.DataFlow.CustomDestination<TInput>` — undocumented, not part of any phase's checklist
- [`Sequence<T>` shadows `Tasks`/`Execute` instead of overriding them](docs/tech-debt/TECH-DEBT-Sequence-Generic-Shadowing.md)
  - A `Sequence<T>` behind a `Sequence`-typed reference runs the base `Execute()`, which invokes the null base `Tasks` delegate — NRE after a `START` log entry with no `END`
  - Direction: make `Execute()` virtual + override, guard the null delegate with a clear exception; surfaced by PR #4 review
- [Three copies of `ExpandoObjectConverter` (Kafka, AI, Rest) — consolidate into Common](docs/tech-debt/TECH-DEBT-ExpandoObjectConverter-Consolidation.md)
  - Copies already disagree: only Kafka honors `PropertyNamingPolicy`, only AI preserves null array elements; XML docs drifted between copies in PR #4
  - Direction: one public converter in `EtlKit.Common` (naming policy honored, nulls preserved), migrate the three call sites, move the AI tests to Common.Tests

## Other

- PrimaryKeyConstrainName now is part of TableDefinition, but not read from `GetTableDefinitionFrom`
- in order to have these tests fully working, add something like MaxBufferSize as DataFlow parameter
  for all DataFlowTasks and use this when creating DF components - also have a static
  DefaultMaxBufferSize as Fallback value
