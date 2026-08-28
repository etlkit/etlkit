# TODO

## Bugs & refactorings

### Future release

- New feature: Bounded Capacity for all Buffers (separately for every component besides
  `DataFlowBatchDestination` & general property in ConnectionManager), to restrict buffer size and
  max memory consumption
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
  - New component alongside `LookupTransformation` with serializable
    `MatchColumns`/`RetrieveColumns` POCO lists
  - `DictionarySource: IDataFlowSource<T>` property deserialized via existing `DataFlowXmlReader`
    mechanism (no reader changes)
  - Optional `ScriptedFieldLookupTransformation` in `EtlKit.Scripting` with Roslyn enrichment script
    string
- [PostgresLogicalReplicationSource — WAL/CDC streaming source](docs/tech-debt/TECH-DEBT-Postgres-Logical-Replication-Source.md)
  - Net-new source in `EtlKit.PostgresStreaming` over `Npgsql.Replication` (built-in `pgoutput`, no
    extension)
  - Complements (does not replace) `PostgresXminTailSource`: full ordered change log incl. DELETEs
    and every intermediate UPDATE, sub-second latency
  - Resume token = LSN via existing `ICheckpointStore`; deferred to V3+ per MLRSSL-1509 §5.8
- [Align `KafkaSource` offset commits with the checkpoint model (at-least-once)](docs/tech-debt/TECH-DEBT-KafkaSource-Offset-Commit-Alignment.md)
  - Today `enable.auto.commit` (Confluent default) commits offsets at read time — at-most-once,
    silently weaker than `PostgresXminTailSource`/`MongoChangeStreamSource` and the producer side
    fixed in MR !5
  - Direction: disable auto-commit, emit `TopicPartitionOffset` with each record, commit strictly
    forward per partition in a terminal committer mirroring `CheckpointWriter` (Kafka's consumer
    group offset IS the checkpoint store)
- [Tests mutate the global `ControlFlow.LoggerFactory`](docs/tech-debt/TECH-DEBT-Test-Global-LoggerFactory.md)
  - A task with no injected logger falls back to the process-wide static, so a test that replaces it
    hands its mock to components owned by other test classes running in parallel — one class fails on
    another class's log line (seen on pipelines 38237 and 38542)
  - Root cause is an API gap: `KafkaTransformation` has no constructor taking a producer *and* a
    logger, so a mock-producer test double has no way to avoid the static
  - Direction: add the missing constructor overloads, migrate the four test sites off the global;
    serializing the assembly hides the shared state rather than removing it
- [Generic type arguments in XML pipeline notation — `typeArguments` attribute](docs/tech-debt/TECH-DEBT-Xml-Generic-Type-Arguments.md)
  - XAML-style notation: tag stays the generic definition name, arguments in a `typeArguments`
    attribute (`<RowTransformation typeArguments="Order, OrderDto">`), parentheses for nesting
  - Replaces the hardcoded `MakeGenericType(typeof(ExpandoObject))` in `GetTypeByName` with an
    arity-matched, alias-registry-backed resolver; no attribute → current behavior (backward
    compatible)
  - Phase 1 covers interior/auxiliary types only; typed end-to-end flows blocked on the `IDataFlow`
    `ExpandoObject` boundary (Phase 2, separate decision)
- [Split `DataTypeConverter` driver conventions before moving type-mapping to Common](docs/tech-debt/TECH-DEBT-DataTypeConverter-Driver-Split.md)
  - Pure type-mapping → Common/Primitives; per-driver SQL-type conventions → driver packages behind
    a DI abstraction (drop the central `switch (ConnectionManagerType)`)
  - Unblocks moving `QueryParameter` to Common (and `ITableColumn` to Primitives); ride along with
    broader driver-package/DI modularization
- [UseRowAccessor mode for ScriptedRowTransformation](docs/tech-debt/TECH-DEBT-ScriptedTransformation-UseRowAccessor.md)
  - Fixes a real bug: scripts with Roslyn warnings only (e.g. CS0472) are incorrectly rejected outright
  - Null fields compile to `dynamic` and blow up with CS0656 because `Microsoft.CSharp` is never referenced (RSSL-12005) — that half has a one-line fix in `ScriptBuilder`; opt-in `Row.Field` still covers the absent-field case
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
- [`DbConnectionString.ToString()` bypasses the `GetConnectionString()` normalization](docs/tech-debt/TECH-DEBT-DbConnectionString-ToString-Divergence.md)
  - `Value` routes through the virtual `GetConnectionString()`, but `ToString()` returns `Builder.ConnectionString` directly — `SqlConnectionString` (the SSPI rewrite) reports two different strings for the same instance; internals only consume `.Value`, so the divergence is public-surface-only
  - Direction: one-line fix (`ToString() => GetConnectionString()`) plus a `ToString() == Value` regression test on `SqlConnectionString`; surfaced by PR #4 review
- [Unified timeout and cancellation approach across sources and transformations](docs/tech-debt/TECH-DEBT-Unified-Timeout-Cancellation.md)
  - No shared convention today: DB is `CommandTimeout = 0` (infinite, not configurable), REST relies
    on `HttpClient` default + retry count/interval, Kafka (MR !5) would silently override the client
    default
  - Principle: don't silently override the client's timeout/retry defaults; expose the knob, keep
    the client default, honor the `CancellationToken` already threaded through
    `Execute`/`ExecuteAsync`
  - Prefer leaving `MessageTimeoutMs` at the librdkafka default in MR !5; fold any unified work here
    rather than into the bug fix
- [Dispose graph components, not just their IDisposable properties](docs/tech-debt/TECH-DEBT-Dispose-Graph-Components.md)
  - Flow cleanup (`DataFlowResources`) disposes only `IDisposable` _properties_ registered by the
    reader; a component that is itself `IDisposable` (e.g. `KafkaTransformation`) is never disposed
  - Proposal: if a component implements `IDisposable`, the flow disposes the component and it owns
    its own properties; if not, keep today's per-property registration
  - Shared connection managers stay flow-owned; externally-owned resources stay excluded; came out
    of MR !5 (RSSL-11867)
- [Multi-target EtlKit packages instead of netstandard2.0-only](docs/tech-debt/TECH-DEBT-Multi-Targeting.md)
  - A netstandard2.0-only binary is compiled against ns2.0 dependency groups (Npgsql pulls
    `System.Collections.Immutable >= 8.0.0` there) while net6.0+ consumers restore per-TFM graphs
    where that edge vanishes — runtime `FileNotFoundException` in `ScriptedTransformation`
    (RSSL-11885)
  - Direction: `netstandard2.0;net6.0;net8.0` so every shipped binary matches the dependency graph
    its consumers actually restore (`EtlKit.MongoDB` already went net6.0-only)
  - Interim rule: anything the compiler bakes into the ns2.0 binary must be reachable through
    declared dependencies on every consumer TFM

## Other

- PrimaryKeyConstrainName now is part of TableDefinition, but not read from `GetTableDefinitionFrom`
- in order to have these tests fully working, add something like MaxBufferSize as DataFlow parameter
  for all DataFlowTasks and use this when creating DF components - also have a static
  DefaultMaxBufferSize as Fallback value
