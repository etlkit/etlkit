using System.Dynamic;
using Confluent.Kafka;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using Microsoft.Extensions.Logging;
using Moq;

namespace EtlKit.Kafka.Tests;

// Regression test for a reported precedent: pointing KafkaTransformation at an invalid/unreachable
// broker host never failed the pipeline. Produce() is fire-and-forget, so the connectivity error only
// reached the async delivery-report callback (KafkaTransformation.SendToKafkaInternal), which used to
// just log it - by then SendToKafka had already returned the row as "successfully" processed.
// SendToKafkaInternal now blocks on the delivery report and throws when it carries an error, so a
// delivery failure is routed through SendToKafka's error handling like any other failure.
public class KafkaTransformationInvalidHostTests
{
    [Fact]
    public void ShouldSendToErrorBufferAndLogError_WhenBootstrapServersHostIsInvalid()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<KafkaTransformation>>();
        mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        dynamic data = new ExpandoObject();
        data.TestName = "Tom";

        // Injecting the logger through the constructor means Logger no longer falls back to
        // ControlFlow.LoggerFactory (the ??= in GenericTask.Logger never triggers once _logger is set),
        // so this test no longer needs to touch process-wide static state.
        var transformation = new KafkaTransformation(mockLogger.Object)
        {
            ProducerConfig = new ProducerConfig
            {
                // ".invalid" is a reserved TLD (RFC 2606) guaranteed to never resolve, so this
                // deterministically reproduces an unreachable-broker host across every environment.
                BootstrapServers = "invalid-host.invalid:9092",
                MessageTimeoutMs = 2000,
            },
            MessageTemplate = "{{TestName}}",
            TopicName = $"test-{Guid.NewGuid()}",
        };

        var source = new MemorySource<ExpandoObject>([data]);
        var dest = new MemoryDestination<ExpandoObject?>();
        var errorDest = new MemoryDestination<EtlKitError>();

        source.LinkTo(transformation);
        transformation.LinkTo(dest);
        transformation.LinkErrorTo(errorDest);

        // Act
        source.Execute();
        dest.Wait();
        errorDest.Wait();

        // Assert: Kafka reports a delivery error for the unreachable broker, which is logged and
        // routed to the error buffer instead of reaching the destination.
        mockLogger.Verify(
            x =>
                x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );

        Assert.Empty(dest.Data);
        Assert.Single(errorDest.Data);
    }

    [Fact]
    public void ShouldThrow_WhenBootstrapServersHostIsInvalidAndNoErrorBufferLinked()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<KafkaTransformation>>();
        mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        dynamic data = new ExpandoObject();
        data.TestName = "Tom";

        var transformation = new KafkaTransformation(mockLogger.Object)
        {
            ProducerConfig = new ProducerConfig
            {
                BootstrapServers = "invalid-host.invalid:9092",
                MessageTimeoutMs = 2000,
            },
            MessageTemplate = "{{TestName}}",
            TopicName = $"test-{Guid.NewGuid()}",
        };

        var source = new MemorySource<ExpandoObject>([data]);
        var dest = new MemoryDestination<ExpandoObject?>();

        source.LinkTo(transformation);
        transformation.LinkTo(dest);
        // No LinkErrorTo: without an error buffer, SendToKafka rethrows the delivery error instead
        // of routing it away, so it must fault the pipeline instead of disappearing silently.

        // Act
        var exception = Record.Exception(() =>
        {
            source.Execute();
            dest.Wait();
        });

        // Assert: the delivery error propagates out of the pipeline. It may be wrapped in one or
        // more AggregateExceptions by the underlying TPL Dataflow completion-propagation, so unwrap
        // down to the original cause.
        Assert.NotNull(exception);
        var cause = exception;
        while (cause is AggregateException { InnerException: { } inner })
            cause = inner;
        Assert.IsType<ProduceException<string, string>>(cause);
    }
}
