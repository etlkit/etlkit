using EtlKit.Common.ControlFlow;
using EtlKit.DataFlow;
using Confluent.Kafka;
using EtlKit.Primitives;
using Microsoft.Extensions.Logging;
using Moq;

namespace EtlKit.Kafka.Tests;

public class KafkaTransformationDeliveryHandlerTests
{
    private class TestableKafkaTransformation : KafkaTransformation<string, string>
    {
        public TestableKafkaTransformation(IProducer<string, string> producer)
            : base(producer) { }

        protected override string BuildMessageValue(string input) => input;
    }

    [Fact]
    public void ShouldLogError_WhenDeliveryReportHasError()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var mockFactory = new Mock<ILoggerFactory>();
        mockFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
        Common.ControlFlow.ControlFlow.LoggerFactory = mockFactory.Object;

        var capturedHandlers = new List<Action<DeliveryReport<string, string>>>();
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
                (_, _, handler) => capturedHandlers.Add(handler)
            );

        var transformation = new TestableKafkaTransformation(mockProducer.Object)
        {
            TopicName = "test-topic",
        };
        var source = new MemorySource<string>(new[] { "test-value" });
        var dest = new MemoryDestination<string?>();
        source.LinkTo(transformation);
        transformation.LinkTo(dest);
        source.Execute();
        dest.Wait();

        Assert.Single(capturedHandlers);

        // Act: Kafka reports an error
        capturedHandlers[0]
            (
                new DeliveryReport<string, string>
                {
                    Error = new Error(ErrorCode.BrokerNotAvailable, "Broker not available"),
                    Message = new Message<string, string> { Value = "test-value" },
                }
            );

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
    }

    [Fact]
    public void ShouldNotLogError_WhenDeliveryReportSucceeds()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var mockFactory = new Mock<ILoggerFactory>();
        mockFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
        Common.ControlFlow.ControlFlow.LoggerFactory = mockFactory.Object;

        var capturedHandlers = new List<Action<DeliveryReport<string, string>>>();
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
                (_, _, handler) => capturedHandlers.Add(handler)
            );

        var transformation = new TestableKafkaTransformation(mockProducer.Object)
        {
            TopicName = "test-topic",
        };
        var source = new MemorySource<string>(new[] { "test-value" });
        var dest = new MemoryDestination<string?>();
        source.LinkTo(transformation);
        transformation.LinkTo(dest);
        source.Execute();
        dest.Wait();

        Assert.Single(capturedHandlers);

        // Act: Kafka reports success
        capturedHandlers[0]
            (
                new DeliveryReport<string, string>
                {
                    Error = new Error(ErrorCode.NoError),
                    Message = new Message<string, string> { Value = "test-value" },
                }
            );

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

        var transformation = new TestableKafkaTransformation(mockProducer.Object)
        {
            TopicName = "test-topic",
        };
        var errorDest = new MemoryDestination<EtlKitError>();
        var source = new MemorySource<string>(new[] { "test-value" });
        var dest = new MemoryDestination<string?>();
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
