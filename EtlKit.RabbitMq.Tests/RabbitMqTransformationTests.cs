using System.Dynamic;
using System.Text;
using System.Threading.Tasks.Dataflow;
using EtlKit.DataFlow;
using EtlKit.DataFlow.Models;
using EtlKit.Primitives;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit.Abstractions;
using PublicationAddress = EtlKit.DataFlow.Models.PublicationAddress;

namespace EtlKit.RabbitMq.Tests
{
    [Collection(nameof(RabbitMqCollection))]
    public class RabbitMqTransformationTests : RabbitMqTestBase
    {
        private string Queue { get; } = $"test-{Guid.NewGuid()}";

        public RabbitMqTransformationTests(RabbitMqFixture fixture, ITestOutputHelper logger)
            : base(fixture, logger) { }

        [Fact]
        public void ShouldPublishAndConsume()
        {
            // Arrange

            dynamic data = new ExpandoObject();
            data.TestName = "Tom";

            using var connection = Fixture.GetConnectionFactory().CreateConnection();

            // Signal the completion of message reception.
            EventWaitHandle waitHandle = new ManualResetEvent(false);

            using var channelToConsume = connection.CreateModel();
            channelToConsume.QueueDeclare(Queue, false, false, false, null);

            string? actualMessage = null;

            // Configure consuming a message from the channel.
            var consumer = new EventingBasicConsumer(channelToConsume);
            consumer.Received += (_, eventArgs) =>
            {
                actualMessage = Encoding.Default.GetString(eventArgs.Body.ToArray());
                waitHandle.Set();
            };

            var correlationId = Guid.NewGuid().ToString();

            var transformation = new RabbitMqTransformation(Fixture.GetConnectionFactory())
            {
                MessageTemplate = "{\"NewMessage\": {\"TestValue\":\"{{TestName}}\"}}",
                Queue = Queue,
                Properties = new RabbitMqProperties { CorrelationId = correlationId },
            };

            var source = new MemorySource<ExpandoObject>([data]);
            var dest = new MemoryDestination<ExpandoObject?>();

            //Act

            source.LinkTo(transformation);
            transformation.LinkTo(dest);
            source.Execute();
            dest.Wait();

            // Consume a published message to check it is published for sure.
            channelToConsume.BasicConsume(Queue, true, consumer);
            waitHandle.WaitOne(TimeSpan.FromSeconds(1));

            waitHandle.Dispose();

            // Assert

            Assert.NotNull(actualMessage);

            Assert.Equal("{\"NewMessage\": {\"TestValue\":\"Tom\"}}", actualMessage);
        }

        [Fact]
        public void RabbitMqPropertiesShouldBeInitialized()
        {
            // Arrange

            dynamic data = new ExpandoObject();
            data.TestName = "Tom";

            var channel = new Mock<IModel>();
            channel.Setup(c => c.CreateBasicProperties()).Returns(new BasicPropertiesStub());
            var connection = new Mock<IConnection>();
            connection.Setup(c => c.CreateModel()).Returns(channel.Object);
            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory.Setup(f => f.CreateConnection()).Returns(connection.Object);

            var properties = new RabbitMqProperties
            {
                AppId = Guid.NewGuid().ToString(),
                CorrelationId = Guid.NewGuid().ToString(),
                ClusterId = Guid.NewGuid().ToString(),
                ContentEncoding = "utf-8",
                ContentType = "text",
                DeliveryMode = 1,
                Expiration = "2024-12-12",
                Headers = new Dictionary<string, string>(),
                MessageId = Guid.NewGuid().ToString(),
                Persistent = false,
                Priority = 1,
                ReplyTo = "test",
                ReplyToAddress = new PublicationAddress
                {
                    ExchangeType = "test",
                    ExchangeName = "test",
                    RoutingKey = "/",
                },
                Timestamp = DateTime.Now.Ticks,
                Type = "test",
                UserId = "test",
            };

            var transformation = new RabbitMqTransformation(connectionFactory.Object)
            {
                MessageTemplate = "{\"NewMessage\": {\"TestValue\":\"{{TestName}}\"}}",
                Queue = Queue,
                Properties = properties,
            };

            var source = new MemorySource<ExpandoObject>(new ExpandoObject[] { data });
            var dest = new MemoryDestination<ExpandoObject?>();

            //Act

            source.LinkTo(transformation);
            transformation.LinkTo(dest);
            source.Execute();
            dest.Wait();

            // Assert
            channel.Verify(
                c =>
                    c.BasicPublish(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<bool>(),
                        It.Is<IBasicProperties>(p => AreEqual(properties, p)),
                        It.IsAny<ReadOnlyMemory<byte>>()
                    ),
                Times.Once
            );
        }

        private static bool AreEqual(RabbitMqProperties properties, IBasicProperties p)
        {
            return properties.AppId == p.AppId
                && properties.CorrelationId == p.CorrelationId
                && properties.ContentType == p.ContentType
                && properties.ContentEncoding == p.ContentEncoding
                && properties.DeliveryMode == p.DeliveryMode
                && properties.Expiration == p.Expiration
                && properties.Headers!.All(h =>
                    p.Headers.ContainsKey(h.Key) && p.Headers[h.Key].ToString() == h.Value
                )
                && properties.MessageId == p.MessageId
                && properties.Persistent == p.Persistent
                && properties.Priority == p.Priority
                && properties.ReplyTo == p.ReplyTo
                && properties.ReplyToAddress!.ExchangeName == properties.ReplyToAddress.ExchangeName
                && properties.ReplyToAddress!.ExchangeType == properties.ReplyToAddress.ExchangeType
                && properties.ReplyToAddress!.RoutingKey == properties.ReplyToAddress.RoutingKey
                && properties.Timestamp == p.Timestamp.UnixTime
                && properties.Type == p.Type
                && properties.UserId == p.UserId;
        }

        [Fact]
        public void ExceptionsShouldBeHandled()
        {
            // Arrange

            var errorTarget = new Mock<IDataFlowLinkTarget<EtlKitError>>();
            var targetBlock = new Mock<ITargetBlock<EtlKitError>>();
            errorTarget.Setup(t => t.TargetBlock).Returns(targetBlock.Object);

            var transformation = new RabbitMqTransformation();
            transformation.LinkErrorTo(errorTarget.Object);

            //Act

            var result = transformation.Publish(new ExpandoObject());

            // Assert

            Assert.Null(result);

            targetBlock.Verify(
                t =>
                    t.OfferMessage(
                        It.IsAny<DataflowMessageHeader>(),
                        It.IsAny<EtlKitError>(),
                        It.IsAny<ISourceBlock<EtlKitError>?>(),
                        It.IsAny<bool>()
                    ),
                Times.Once
            );
        }
    }
}
