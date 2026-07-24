using Confluent.Kafka;
using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using Moq;

namespace EtlKit.Kafka.Tests;

// Locks in the contract between KafkaTransformation's confirm stage and CheckpointWriter:
// a row is re-emitted downstream only after its delivery report confirms a durable write, so a
// CheckpointWriter placed after the transformation can never commit a position past an
// unconfirmed or failed message. This is the exact precondition CheckpointWriter's at-least-once
// design states for its upstream ("a record reaching the writer implies it was already durably
// written") — previously only implied by two independent test suites, never pinned as a pair.
public class KafkaTransformationCheckpointWriterTests
{
    private sealed record Row(long Position, string Value);

    private sealed class TestableKafkaTransformation : KafkaTransformation<Row, string>
    {
        public TestableKafkaTransformation(IProducer<string, string> producer)
            : base(producer) { }

        protected override string BuildMessageValue(Row input) => input.Value;
    }

    // Delivery reports fire synchronously inside Produce: success for every value except
    // failValue, which gets an error report — the shape of a broker rejecting one message.
    private static Mock<IProducer<string, string>> NewProducerMock(string? failValue = null)
    {
        var mockProducer = new Mock<IProducer<string, string>>();
        mockProducer
            .Setup(p =>
                p.Produce(
                    It.IsAny<string>(),
                    It.IsAny<Message<string, string>>(),
                    It.IsAny<Action<DeliveryReport<string, string>>>()
                )
            )
            .Callback<string, Message<string, string>, Action<DeliveryReport<string, string>>>(
                (_, message, handler) =>
                    handler(
                        new DeliveryReport<string, string>
                        {
                            Error =
                                message.Value == failValue
                                    ? new Error(
                                        ErrorCode.BrokerNotAvailable,
                                        "Broker not available"
                                    )
                                    : new Error(ErrorCode.NoError),
                            Message = message,
                        }
                    )
            );
        return mockProducer;
    }

    private static (
        MemorySource<Row> Source,
        TestableKafkaTransformation Transformation,
        CheckpointWriter<Row?, long> Writer
    ) NewPipeline(InMemoryCheckpointStore<long> store, string checkpointId, string? failValue)
    {
        var source = new MemorySource<Row>(
            new[] { new Row(1, "first"), new Row(2, "second"), new Row(3, "third") }
        );
        var transformation = new TestableKafkaTransformation(NewProducerMock(failValue).Object)
        {
            TopicName = "test-topic",
        };
        var writer = new CheckpointWriter<Row?, long>
        {
            CheckpointStore = store,
            CheckpointId = checkpointId,
            Position = r => r!.Position,
        };
        source.LinkTo(transformation);
        transformation.LinkTo(writer);
        return (source, transformation, writer);
    }

    [Fact]
    public async Task CommitsUpToLastRow_WhenEveryDeliveryIsConfirmed()
    {
        const string checkpointId = "kafka-cw-happy";
        var store = new InMemoryCheckpointStore<long>();
        var (source, _, writer) = NewPipeline(store, checkpointId, failValue: null);

        await source.ExecuteAsync();
        writer.Wait();

        var (found, position) = await store.LoadAsync(checkpointId, CancellationToken.None);
        Assert.True(found);
        Assert.Equal(3, position);
    }

    [Fact]
    public async Task DoesNotCommitPastFailedRow_WhenNoErrorBufferLinked()
    {
        const string checkpointId = "kafka-cw-fault";
        var store = new InMemoryCheckpointStore<long>();
        var (source, _, writer) = NewPipeline(store, checkpointId, failValue: "second");

        await source.ExecuteAsync();
        var exception = Record.Exception(() => writer.Wait());

        // The delivery failure of row 2 faults the pipeline in row order: row 1 was confirmed and
        // committed, rows 2 and 3 never reach the writer, so a restart replays from position 1
        // (at-least-once, nothing lost).
        Assert.NotNull(exception);
        var cause = exception;
        while (cause is AggregateException { InnerException: { } inner })
            cause = inner;
        Assert.IsType<ProduceException<string, string>>(cause);

        var (found, position) = await store.LoadAsync(checkpointId, CancellationToken.None);
        Assert.True(found);
        Assert.Equal(1, position);
    }

    [Fact]
    public async Task CommitsPastFailedRow_WhenErrorBufferLinked()
    {
        const string checkpointId = "kafka-cw-errbuf";
        var store = new InMemoryCheckpointStore<long>();
        var (source, transformation, writer) = NewPipeline(
            store,
            checkpointId,
            failValue: "second"
        );
        var errorDest = new MemoryDestination<EtlKitError>();
        transformation.LinkErrorTo(errorDest);

        await source.ExecuteAsync();
        writer.Wait();
        errorDest.Wait();

        // Linking an error buffer is the explicit opt-out: the failed row is diverted there and
        // will NOT be replayed by the checkpoint — the commit advances past it to row 3. Callers
        // who want failed deliveries retried via checkpoint replay must not link an error buffer.
        Assert.Single(errorDest.Data);
        var (found, position) = await store.LoadAsync(checkpointId, CancellationToken.None);
        Assert.True(found);
        Assert.Equal(3, position);
    }
}
