# Tech Debt: tests mutate the global `ControlFlow.LoggerFactory`

## Context

Every task in the library resolves its logger lazily, falling back to a process-wide static when
none was injected (`EtlKit.Common/ControlFlow/GenericTask.cs`):

```csharp
public ILogger Logger => _logger ??= ControlFlow.LoggerFactory.CreateLogger<GenericTask>();
```

`ControlFlow.LoggerFactory` is a mutable static. That is a deliberate convenience for applications —
`EtlKit.Logging.Database/DatabaseLoggingConfiguration.cs:62` sets it as part of the public
configuration API, and that use is fine.

The debt is on the **test** side: a test that wants to observe what a component logs has no other
handle on it, so it overwrites the global.

## Problem

Tests overwrite a process-wide static, and xUnit runs test classes within an assembly **in
parallel** by default. A component that did not receive an injected logger therefore resolves
whichever mock the most recent test happened to install — possibly a mock belonging to a test class
running concurrently in another thread.

The failure mode is a test failing because of an assertion made by a *different* test.

### Observed

`EtlKit.Kafka.Tests.KafkaTransformationDeliveryHandlerTests.ShouldNotLogError_WhenDeliveryReportSucceeds`
fails intermittently in CI. Its own producer mock returns `NoError` for every message, so its
transformation cannot log an error — yet the mock records one:

```
Moq.MockException : Expected invocation on the mock should never have been performed, but was 1 times:
  x => x.Log<It.IsAnyType>(LogLevel.Error, ...)
Recorded: ILogger.Log<FormattedLogValues>(LogLevel.Error, 0, Failed: second, Error: Broker not available, ...)
```

`"second"` and `"Broker not available"` are the fixture data of
`KafkaTransformationCheckpointWriterTests` — a different class, running in parallel, whose
transformation had no injected logger and so logged through the global factory this test had just
replaced.

Seen on pipelines
[38542](https://git.rapidsoft.ru/open-source/etlkit/-/pipelines/38542) (MR !12) and
[38237](https://git.rapidsoft.ru/open-source/etlkit/-/pipelines/38237) (`develop`, unrelated commit,
two weeks earlier). It does not reproduce on a developer machine, where the classes tend to drift
apart in time.

## Why the tests cannot simply stop doing it

`KafkaTransformation` has no constructor accepting a producer **and** a logger. The producer
constructor chains to the logger-less one
(`EtlKit.Kafka/KafkaTransformation.cs:157`):

```csharp
protected KafkaTransformation(IProducer<TKafkaKey, TKafkaValue> producer)
    : this()            // -> this(logger: null) -> base(null)
{
    _producer = producer;
}
```

So a test double built around a mock producer always leaves `_logger` null and always lands on the
static fallback. Overwriting the global is the only lever the test has. The debt is an API gap, not
test sloppiness.

## Direction

1. Add a producer-and-logger constructor at both levels of the hierarchy. Type the parameter as the
   non-generic `ILogger` — that is what `GenericTask` stores, and it lets tests pass the
   `Mock<ILogger>` they already build instead of mocking a three-parameter generic:

   ```csharp
   // KafkaTransformation<TInput, TKafkaKey, TKafkaValue>
   protected KafkaTransformation(IProducer<TKafkaKey, TKafkaValue> producer, ILogger? logger)
       : base(logger)
   {
       _producer = producer;
       TaskName = "Execute Kafka transformation";
   }

   // KafkaTransformation<TInput, TKafkaValue>
   protected KafkaTransformation(IProducer<string, TKafkaValue> producer, ILogger? logger)
       : base(producer, logger) { }
   ```

2. Migrate the test doubles to forward a logger, and drop the global assignments.
3. Work through the remaining sites (below). Each needs its own answer — some components may need
   the same constructor treatment, others already accept a logger.
4. Only once no test writes the global, consider whether anything else is needed. Do **not** reach
   for `parallelizeTestCollections: false` as a substitute: it hides the shared state instead of
   removing it, and its effect here was never measured cleanly (see the note below).

## Sites

| File | Line | Note |
|---|---|---|
| `EtlKit.Kafka.Tests/KafkaTransformationDeliveryHandlerTests.cs` | 28, 95 | Installs a `Mock<ILoggerFactory>`; the observed failure |
| `EtlKit.Kafka.Tests/KafkaJsonSourceTests.cs` | 41 | Installs a real `LoggerFactory` |
| `EtlKit.Kafka.Tests/KafkaTransformationTests.cs` | 33 | Installs a real `LoggerFactory` |
| `EtlKit.RabbitMq.Tests/RabbitMqTestBase.cs` | 17 | Same pattern in another assembly |
| `EtlKit.Logging.Database/DatabaseLoggingConfiguration.cs` | 62 | Production configuration API — **not** part of this debt |

No test restores the previous factory afterwards, so the pollution also outlives the test that
caused it.

## Note on an unverified measurement

An earlier attempt to gauge whether serializing the Kafka test assembly helps produced 13 failures
out of 23. That measurement is **void**: the local `localkafka` container had no published ports at
the time, so the tests requiring a live broker were failing for that reason alone. Whether
serialization helps or hurts is unknown and was never established. It is also beside the point — the
direction above removes the shared state rather than scheduling around it.

## Related

The same investigation found a second, independent defect in
`KafkaTransformationCheckpointWriterTests.DoesNotCommitPastFailedRow_WhenNoErrorBufferLinked`: the
test asserted that row 1's checkpoint commit had landed before the pipeline faulted on row 2, which
pins a race rather than the at-least-once contract (committing nothing is legal — a restart replays
and loses nothing). That one is already fixed and is not part of this debt.
