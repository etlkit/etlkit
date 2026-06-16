using System.Dynamic;
using ALE.ETLBox.Common.ControlFlow;
using ALE.ETLBox.DataFlow;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace ETLBox.Kafka.Tests
{
    public class KafkaTransformationTests : IClassFixture<KafkaFixture>
    {
        private readonly KafkaFixture _fixture;
        private readonly ITestOutputHelper _output;

        private string TopicName { get; } = $"test-{Guid.NewGuid()}";

        private ConsumerConfig GetConsumerConfig(bool enablePartitionEof, string? topicName = null)
        {
            return new ConsumerConfig
            {
                BootstrapServers = _fixture.BootstrapAddress,
                GroupId = $"{topicName ?? TopicName}-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnablePartitionEof = enablePartitionEof,
            };
        }

        public KafkaTransformationTests(KafkaFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            ControlFlow.LoggerFactory = new LoggerFactory(
                new[] { new TestOutputLoggerProvider(_output) }
            );
        }

        [Fact]
        public void ShouldProduceAndConsumeDirectlyToKafka()
        {
            // Arrange
            dynamic data = new ExpandoObject();
            data.TestName = "Tom";

            var transformation = new KafkaTransformation()
            {
                ProducerConfig = new ProducerConfig
                {
                    BootstrapServers = _fixture.BootstrapAddress,
                },
                MessageTemplate = "{\"NewMessage\": {\"TestValue\":\"{{TestName}}\"}}",
                TopicName = TopicName,
            };

            var source = new MemorySource<ExpandoObject>(new ExpandoObject[] { data });
            var dest = new MemoryDestination<ExpandoObject?>();

            //Act
            source.LinkTo(transformation);
            transformation.LinkTo(dest);
            source.Execute();
            dest.Wait();

            var result = ConsumeJson(true, CancellationToken.None).ToArray();

            // Assert
            Assert.Single(result);

            Assert.Equal("{\"NewMessage\": {\"TestValue\":\"Tom\"}}", result[0]);
        }

        [Fact]
        public void ShouldProduceMessageWithKey_WhenMessageKeyTemplateSet()
        {
            // Arrange
            dynamic data = new ExpandoObject();
            data.loyalty_program_id = 2;
            data.transaction_id = 12345;

            var transformation = new KafkaTransformation()
            {
                ProducerConfig = new ProducerConfig
                {
                    BootstrapServers = _fixture.BootstrapAddress,
                },
                MessageTemplate = "{{transaction_id}}",
                MessageKeyTemplate = "{{loyalty_program_id}}:{{transaction_id}}",
                TopicName = TopicName,
            };

            var source = new MemorySource<ExpandoObject>(new ExpandoObject[] { data });
            var dest = new MemoryDestination<ExpandoObject?>();

            // Act
            source.LinkTo(transformation);
            transformation.LinkTo(dest);
            source.Execute();
            dest.Wait();

            var result = ConsumeKeyed(true, CancellationToken.None).ToArray();

            // Assert: composite key rendered from MessageKeyTemplate, body from MessageTemplate
            Assert.Single(result);
            Assert.Equal("2:12345", result[0].Key);
            Assert.Equal("12345", result[0].Value);
        }

        private IEnumerable<string> ConsumeJson(
            bool enablePartitionEof,
            CancellationToken cancellationToken,
            string? topicName = null
        )
        {
            using var consumer = new ConsumerBuilder<Ignore, string>(
                GetConsumerConfig(enablePartitionEof, topicName)
            ).Build();
            _output.WriteLine($"Subscribing to topic {topicName ?? TopicName}...");
            consumer.Subscribe(topicName ?? TopicName);
            while (true)
            {
                ConsumeResult<Ignore, string> consumeResult;
                try
                {
                    consumeResult = consumer.Consume(cancellationToken);
                    _output.WriteLine(
                        $"Consumed direct message {consumeResult.Message?.Value ?? "null"}"
                    );
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                if (consumeResult.IsPartitionEOF || consumeResult.Message is null)
                {
                    break;
                }

                yield return consumeResult.Message.Value;
            }
        }

        // Like ConsumeJson, but reads the message key too (ConsumerBuilder<string, string> instead of
        // <Ignore, string>) so the rendered Kafka key can be asserted.
        private IEnumerable<(string? Key, string Value)> ConsumeKeyed(
            bool enablePartitionEof,
            CancellationToken cancellationToken,
            string? topicName = null
        )
        {
            using var consumer = new ConsumerBuilder<string, string>(
                GetConsumerConfig(enablePartitionEof, topicName)
            ).Build();
            _output.WriteLine($"Subscribing to topic {topicName ?? TopicName}...");
            consumer.Subscribe(topicName ?? TopicName);
            while (true)
            {
                ConsumeResult<string, string> consumeResult;
                try
                {
                    consumeResult = consumer.Consume(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                if (consumeResult.IsPartitionEOF || consumeResult.Message is null)
                {
                    break;
                }

                yield return (consumeResult.Message.Key, consumeResult.Message.Value);
            }
        }
    }
}
