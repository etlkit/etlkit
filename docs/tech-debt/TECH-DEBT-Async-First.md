# Tech Debt: async-first data flow and control flow

## Context

Every EtlKit component exposes a synchronous contract and reaches asynchronous work only through
blocking wrappers. TPL Dataflow, the engine underneath every data flow, accepts asynchronous
delegates natively: `TransformBlock` takes `Func<TInput, Task<TOutput>>`, `ActionBlock` takes
`Func<TInput, Task>`, and every block exposes a `Completion` task. The engine does not need
replacing. The synchrony sits in the component contracts, the base classes, and the database access
layer built on top of the engine.

This note records where the blocking calls are, what async-first means for this library, and the
order in which the change can land without a big-bang rewrite.

## Current state

**Platform.** Shipped packages target `netstandard2.0`. That target has no `IAsyncEnumerable<T>`
or `IAsyncDisposable` without `Microsoft.Bcl.AsyncInterfaces`, and no default interface methods, so
any member added to a public interface breaks external implementations. `IConnectionManager`
(`EtlKit.Primitives/IConnectionManager.cs`) is written against `IDbCommand` and `IDataReader`,
interfaces that have no asynchronous methods at all. The asynchronous methods live on the abstract
classes `DbConnection`, `DbCommand`, and `DbDataReader`, which are available on `netstandard2.0`.

**Contracts in Primitives.** A source implements `void Execute(CancellationToken)`;
`DataFlowSource.ExecuteAsync` (`EtlKit.Common/DataFlow/DataFlowSource.cs`) wraps it in
`Task.Factory.StartNew`, so every running source occupies a thread-pool thread for its whole
lifetime. `IConnectionManager` is synchronous end to end: `Open`, `ExecuteNonQuery`,
`ExecuteScalar`, `ExecuteReader`, `BulkInsert`. Control-flow tasks expose `void Execute()`.
`IDataFlowDestination` already has `Task Completion`, with `Wait()` as the blocking wrapper, which is
the shape the rest of the library should converge on.

**Base classes in Common.** `RowTransformation` accepts only `Func<TInput, TOutput>`, and
`TransformBlockWithCompletion` (`EtlKit.Common/TaskUtilities/TransformBlockWithCompletion.cs`)
builds its `TransformBlock` over that synchronous delegate. `RowBatchTransformation` accepts
`Func<TInput[], TOutput[]>`. `CustomDestination` accepts `Action<TInput>`.
`DataFlowBatchDestination.WriteBatch` calls `BulkInsert` synchronously inside an `ActionBlock`.
`ErrorHandler.Send` blocks on `Wait()`.

**Components.** Thirty-three library files contain a blocking call (`.Wait()`, `.Result`,
`GetAwaiter().GetResult()`). The pattern `Buffer.SendAsync(row).Wait()` inside a synchronous
`Execute` appears in `CustomSource`, `MemorySource`, `DbSource`, `CsvSource`, `JsonSource`,
`XmlSource`, `ExcelSource`, `CrossJoin`, `Aggregation`, `BlockTransformation`, `KafkaSource`,
`MongoChangeStreamSource`, and `PostgresXminTailSource`. Sync-over-async sits where the work is
asynchronous by nature:

| Component | Blocking call |
|---|---|
| `RestTransformation` | `RestMethodAsync(source).GetAwaiter().GetResult()` per row |
| `AIBatchTransformation` | `InvokeAsync(batch).GetAwaiter().GetResult()` per batch |
| `ScriptedRowTransformation` | `runner.RunAsync(arg).Result` per row |
| `PostgresXminTailSource` | `Task.Delay(PollingInterval, ct).Wait(ct)` in the poll loop |
| `MongoChangeStreamSource` | `CheckpointStore.LoadAsync(...).GetAwaiter().GetResult()` |
| `CheckpointWriter` | `CommitMaxAsync(...).GetAwaiter().GetResult()` |
| `DbConnectionManager` | `Task.Delay(1000).Wait()` between login attempts |
| `DataFlowStreamSource` | `HttpClient.GetStreamAsync(uri).Result` |

Each of these holds a TPL Dataflow worker thread for the duration of an I/O call. With the default
`MaxDegreeOfParallelism` of 1 the throughput of a REST, AI, or scripted transformation is bounded by
one blocked thread per block instead of by the number of requests in flight.

**Control flow.** About thirty tasks under `EtlKit/src/Toolbox/ControlFlow` and
`EtlKit/src/Toolbox/Logging` expose `void Execute()` and static facades such as
`SqlTask.ExecuteNonQuery(...)`. `DbTask` (`EtlKit/src/Definitions/TaskBase/ControlFlow/DbTask.cs`)
drives `IDbCommand` directly. `DbSource.ReadAll` reads through `SqlTask.ExecuteReader` and its
synchronous callback model (`BeforeRowReadAction`, `Actions`, `AfterRowReadAction`).

**Ambient state.** `ControlFlow.Stage`, `ControlFlow.CurrentLoadProcess`,
`ControlFlow.DefaultDbConnection`, and `ControlFlow.LoggerFactory` are process-wide statics. Two
pipelines running concurrently in one process share them; an asynchronous host (Worker Service,
ASP.NET, a module host with hot reload) runs pipelines concurrently as a matter of course.
`TECH-DEBT-Test-Global-LoggerFactory.md` records the test-side symptom of the same problem.

**Cancellation.** The `CancellationToken` flows through `Execute`/`ExecuteAsync` but components do
not observe it (`TECH-DEBT-Unified-Timeout-Cancellation.md`, item 4). A synchronous read loop has
to check the token by hand; an asynchronous one observes it through `SendAsync(row, ct)` and
`ReadAsync(ct)` with no extra code.

**Tests.** Test projects call `.Execute()` at 341 sites and `.Wait()` at 425 sites, against 24
calls to `ExecuteAsync`. The synchronous API is the one the tests exercise.

## Problem

1. **One thread per source and one blocked thread per I/O-bound transformation.** A pipeline with a
   REST enrichment step and a Kafka source holds two thread-pool threads for its lifetime and
   serializes REST calls on a third.
2. **Cancellation cannot be honored without touching every component**, because the blocking calls
   sit between the token and the I/O.
3. **The library cannot be hosted cleanly inside an asynchronous process.** Running `Execute()` from
   an `IHostedService` or from a message-driven host means `Task.Run` around a blocking call, plus
   shared statics across concurrent pipelines.
4. **No `IAsyncEnumerable<T>` boundary.** Sources cannot consume an asynchronous stream from
   another library, and destinations cannot expose one, which blocks interoperability with
   message-routing frameworks whose SQL and HTTP components produce `IAsyncEnumerable<T>` bodies.

## Direction

Async-first means: the asynchronous method is the one a component implements, the synchronous
method is a wrapper, and the only blocking point is the pipeline entry (`Execute()` and `Wait()`
on the caller's thread). The phases below are ordered so that each one ships on its own and the
synchronous surface keeps working throughout.

### Phase 0: platform

- [ ] Multi-target shipped packages per `TECH-DEBT-Multi-Targeting.md`; add
      `Microsoft.Bcl.AsyncInterfaces` for the `netstandard2.0` asset so `IAsyncEnumerable<T>` and
      `IAsyncDisposable` are available on every TFM.
- [ ] Move `IConnectionManager` from `IDbCommand`/`IDataReader` to `DbCommand`/`DbDataReader`.
      `CreateCommand` returns `DbCommand`, `ExecuteReader` returns `DbDataReader`. Every in-repo
      implementation already derives from `DbConnectionManager<TConnection, ...>`; the test double
      `RecordingConnectionManager` is the one direct implementation to update.

Breaking: `IConnectionManager` has 330 call sites in the repository, most of them reading
`IDataReader`. Pairs with the major version the multi-targeting work already needs.

### Phase 1: contracts in Primitives and Common

- [ ] `DataFlowSource<TOutput>`: `ExecuteAsync(CancellationToken)` becomes the abstract member,
      `Execute(CancellationToken)` becomes the blocking wrapper. `IDataFlowSource<T>` keeps both
      members, the documentation flips which one is primary.
- [ ] `IConnectionManager`: add `OpenAsync`, `ExecuteNonQueryAsync`, `ExecuteScalarAsync`,
      `ExecuteReaderAsync`, `BulkInsertAsync`, `BeginTransactionAsync`, `CommitTransactionAsync`,
      `RollbackTransactionAsync`, each taking a `CancellationToken`. `DbConnectionManager` implements
      them over the `Db*` async API and reimplements the synchronous members as wrappers, so the
      per-driver subclasses only override `BulkInsertAsync` (`SqlBulkCopy.WriteToServerAsync`,
      Npgsql `BeginBinaryImportAsync`; drivers without an async bulk API keep the synchronous call
      inside the async override).
- [ ] `DbTask`: `ExecuteNonQueryAsync`, `ExecuteScalarAsync`, `ExecuteReaderAsync`,
      `BulkInsertAsync`; the existing members become wrappers.
- [ ] Replace `Task.Delay(1000).Wait()` in `DbConnectionManager.TryOpenConnectionXTimes` with an
      awaited delay in the async path.

Breaking for external subclasses of `DataFlowSource` (they override `ExecuteAsync` instead of
`Execute`) and for external implementations of `IConnectionManager`. Neither is known to exist
outside this repository.

### Phase 2: base classes, no source changes yet

- [ ] `RowTransformation`: add `Func<TInput, CancellationToken, Task<TOutput>>` as the primary
      delegate; a synchronous `Func<TInput, TOutput>` is wrapped with `Task.FromResult`.
      `TransformBlockWithCompletion` builds over `Func<TInput, Task<TOutput>>`.
- [ ] `RowBatchTransformation`: `Func<TInput[], CancellationToken, Task<TOutput[]>>` alongside the
      synchronous delegate; `ExecuteBatchAsync` is already async internally.
- [ ] `CustomDestination`: `Func<TInput, Task> WriteActionAsync` alongside `WriteAction`.
- [ ] `DataFlowBatchDestination`: `WriteBatchAsync` over `ActionBlock<TInput[]>(Func<_, Task>)`,
      calling `BulkInsertAsync`.
- [ ] `ErrorHandler.Send`: await the error buffer instead of `Wait()`.
- [ ] Rewrite `RestTransformation`, `AIBatchTransformation`, `ScriptedRowTransformation`, and
      `CheckpointWriter` onto the async delegates. This is the first user-visible gain: I/O-bound
      transformations stop blocking worker threads, and `MaxDegreeOfParallelism` starts to mean
      requests in flight.

Not breaking: the synchronous overloads stay.

### Phase 3: sources and destinations

- [ ] Flip every `Buffer.SendAsync(row).Wait()` to `await Buffer.SendAsync(row, ct)` inside an
      `ExecuteAsync` override. Order of work: `CustomSource`, `MemorySource`, `JsonSource`,
      `XmlSource`, `CsvSource`, `DbSource`, `CrossJoin`, `Aggregation`, `BlockTransformation`,
      `MongoChangeStreamSource`, `PostgresXminTailSource`.
- [ ] `DbSource`: read through `DbDataReader.ReadAsync` directly instead of the `SqlTask` callback
      model. The row-mapping delegates (`DefineActions`) are reusable; only the driving loop moves.
- [ ] `CsvSource`, `JsonSource`, `XmlSource`: use the readers' own async APIs (`CsvReader.ReadAsync`,
      `JsonTextReader.ReadAsync`, `XmlReader.ReadAsync` with `XmlReaderSettings.Async`).
- [ ] `DataFlowStreamSource`/`DataFlowStreamDestination`: await the `HttpClient` calls.
- [ ] Add an `IAsyncEnumerable<T>` source (`AsyncEnumerableSource<T>`) and expose
      `IAsyncEnumerable<T>` from `MemoryDestination` or a dedicated destination.
- [ ] `ExcelSource` and `KafkaSource` stay synchronous inside: ExcelDataReader has no async read, and
      `Confluent.Kafka` consumption is a blocking `Consume` call. Their `ExecuteAsync` runs the loop
      on a dedicated long-running task and awaits it; that is the documented exception, not a
      leftover.

### Phase 4: control flow

- [ ] `ExecuteAsync` on every task under `Toolbox/ControlFlow` and `Toolbox/Logging`, implemented
      over the `DbTask` async members; `Execute()` becomes the wrapper.
- [ ] Async variants of the static facades (`SqlTask.ExecuteNonQueryAsync(...)`, and so on).
- [ ] `Sequence`/`Package`: `Func<Task>` task delegates alongside `Action`.

Mechanical, one task per commit is fine. Not breaking.

### Phase 5: ambient state

- [ ] Replace `ControlFlow.Stage`, `CurrentLoadProcess`, and `DefaultDbConnection` with an
      `AsyncLocal<T>`-backed context or an explicit context object resolved through DI, so two
      pipelines in one process do not share them. `LoggerFactory` follows the direction in
      `TECH-DEBT-Test-Global-LoggerFactory.md`.

Partially breaking: code that sets the statics keeps working if the statics become the default
value of the ambient context.

### Phase 6: guard rails

- [ ] Enable an analyzer that flags `.Wait()`, `.Result`, and `GetAwaiter().GetResult()` in
      library projects (for example `VSTHRD002` from `Microsoft.VisualStudio.Threading.Analyzers`),
      with suppressions only at the documented blocking points: `Execute()`, `Wait()`, and the two
      dedicated-thread sources.
- [ ] Document the convention in `docs/dataflow/`: implement the async member, never block inside
      a block delegate, observe the token.
- [ ] Tests stay on the synchronous entry points; new tests for async components use
      `ExecuteAsync` and `await Completion`.

## Compatibility

The synchronous entry points remain and become thin wrappers:
`Execute()` is `ExecuteAsync().GetAwaiter().GetResult()`, `Wait()` is
`Completion.GetAwaiter().GetResult()`. Console ETL jobs have no `SynchronizationContext`, so the
wrapper cannot deadlock there; a caller inside a UI or legacy ASP.NET context uses the async entry
points. The 766 synchronous call sites in tests therefore need no change when a phase lands.

Phases 0 and 1 are the breaking ones and should land together in one major release with
multi-targeting. The alternative, parallel `IAsyncConnectionManager` and `IAsyncDataFlowSource`
interfaces next to the existing ones, avoids the break but doubles the public surface for as long
as both exist; given that no external implementers of these contracts are known, the break is the
cheaper path.

## Non-goals

- Replacing TPL Dataflow with `System.Threading.Channels`, Rx, or a custom scheduler. Dataflow
  already provides async delegates, bounded capacity, and completion propagation.
- Making `ExcelSource` or `KafkaSource` asynchronous internally; their drivers do not offer it.
- Rewriting tests to the async API in bulk; they migrate as components are touched.

## Relation to other tech-debt documents

- `TECH-DEBT-Multi-Targeting.md`: phase 0 depends on it and shares the major release.
- `TECH-DEBT-Unified-Timeout-Cancellation.md`: item 4 (honor the token end to end) is a by-product
  of phases 2 and 3.
- `TECH-DEBT-Test-Global-LoggerFactory.md`: phase 5 generalizes its direction to the other statics.
- `TECH-DEBT-KafkaSource-Offset-Commit-Alignment.md`: the terminal committer it proposes is an
  async destination and should be written against the phase 2 base classes.
