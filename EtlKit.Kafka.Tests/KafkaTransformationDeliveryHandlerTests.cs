using Confluent.Kafka;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TransformationLogger = Microsoft.Extensions.Logging.ILogger<EtlKit.DataFlow.KafkaTransformation<
    string,
    string,
    string
>>;

namespace EtlKit.Kafka.Tests;

// Loggers are injected through constructors rather than published on the process-global
// ControlFlow.LoggerFactory: that static is shared by every test class in this assembly, and xUnit
// runs classes in parallel, so a mock installed there also collects the log records of whatever
// else happens to be running. That is what made ShouldNotLogError_WhenDeliveryReportSucceeds flaky
// — a concurrent delivery failure in another class landed an Error in this class's mock and broke
// its Times.Never verification. With injection each mock is reachable only by its own pipeline.
public class KafkaTransformationDeliveryHandlerTests
{
    private class TestableKafkaTransformation : KafkaTransformation<string, string>
    {
        public TestableKafkaTransformation(
            IProducer<string, string> producer,
            TransformationLogger logger
        )
            : base(producer, logger) { }

        protected override string BuildMessageValue(string input) => input;
    }

    private static Mock<TransformationLogger> NewLoggerMock()
    {
        var mockLogger = new Mock<TransformationLogger>();
        mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        return mockLogger;
    }

    private static Mock<IProducer<string, string>> NewProducerMock(Error deliveryError)
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
                            Error = deliveryError,
                            Message = message,
                        }
                    )
            );
        return mockProducer;
    }

    [Fact]
    public void ShouldSendToErrorBufferAndLogError_WhenDeliveryReportHasError()
    {
        // Arrange
        var mockLogger = NewLoggerMock();
        var mockProducer = NewProducerMock(
            new Error(ErrorCode.BrokerNotAvailable, "Broker not available")
        );

        using var transformation = new TestableKafkaTransformation(
            mockProducer.Object,
            mockLogger.Object
        )
        {
            TopicName = "test-topic",
        };
        var source = new MemorySource<string>(NullLogger<MemorySource<string>>.Instance)
        {
            Data = new List<string> { "test-value" },
        };
        var dest = new MemoryDestination<string?>(NullLogger<MemoryDestination<string?>>.Instance);
        var errorDest = new MemoryDestination<EtlKitError>(
            NullLogger<MemoryDestination<EtlKitError>>.Instance
        );
        source.LinkTo(transformation);
        transformation.LinkTo(dest);
        transformation.LinkErrorTo(errorDest);

        // Act
        source.Execute();
        dest.Wait();
        errorDest.Wait();

        // Assert
        mockLogger.Verify(
            x =>
                x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (v, _) =>
                            v.ToString()!.Contains("test-value")
                            && v.ToString()!.Contains("Broker not available")
                    ),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );

        Assert.Empty(dest.Data);
        Assert.Single(errorDest.Data);
    }

    [Fact]
    public void ShouldNotLogError_WhenDeliveryReportSucceeds()
    {
        // Arrange
        var mockLogger = NewLoggerMock();
        var mockProducer = NewProducerMock(new Error(ErrorCode.NoError));

        using var transformation = new TestableKafkaTransformation(
            mockProducer.Object,
            mockLogger.Object
        )
        {
            TopicName = "test-topic",
        };
        var source = new MemorySource<string>(NullLogger<MemorySource<string>>.Instance)
        {
            Data = new List<string> { "test-value" },
        };
        var dest = new MemoryDestination<string?>(NullLogger<MemoryDestination<string?>>.Instance);
        source.LinkTo(transformation);
        transformation.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert
        mockLogger.Verify(
            x =>
                x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Never
        );

        Assert.Single(dest.Data);
    }

    [Fact]
    public void ShouldSendToErrorBuffer_WhenProduceThrows()
    {
        // Arrange
        var mockProducer = new Mock<IProducer<string, string>>();
        mockProducer
            .Setup(p =>
                p.Produce(
                    It.IsAny<string>(),
                    It.IsAny<Message<string, string>>(),
                    It.IsAny<Action<DeliveryReport<string, string>>>()
                )
            )
            .Throws(new InvalidOperationException("Simulated produce failure"));

        using var transformation = new TestableKafkaTransformation(
            mockProducer.Object,
            NullLogger<KafkaTransformation<string, string, string>>.Instance
        )
        {
            TopicName = "test-topic",
        };
        var errorDest = new MemoryDestination<EtlKitError>(
            NullLogger<MemoryDestination<EtlKitError>>.Instance
        );
        var source = new MemorySource<string>(NullLogger<MemorySource<string>>.Instance)
        {
            Data = new List<string> { "test-value" },
        };
        var dest = new MemoryDestination<string?>(NullLogger<MemoryDestination<string?>>.Instance);
        source.LinkTo(transformation);
        transformation.LinkTo(dest);
        transformation.LinkErrorTo(errorDest);

        // Act
        source.Execute();
        dest.Wait();
        errorDest.Wait();

        // Assert
        Assert.Single(errorDest.Data);
        Assert.Contains("Simulated produce failure", errorDest.Data.First().ErrorText);
    }
}
