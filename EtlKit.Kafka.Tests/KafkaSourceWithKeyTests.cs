using Confluent.Kafka;
using EtlKit.DataFlow;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace EtlKit.Kafka.Tests;

[Collection("Kafka")]
public class KafkaSourceWithKeyTests : IClassFixture<KafkaFixture>
{
    private readonly KafkaFixture _fixture;

    private string TopicName { get; } = $"test-{Guid.NewGuid()}";

    public KafkaSourceWithKeyTests(KafkaFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        Common.ControlFlow.ControlFlow.LoggerFactory = new LoggerFactory(
            new[] { new TestOutputLoggerProvider(output) }
        );
    }

    public sealed record ConsumedMessage(byte[]? Key, string Value, Timestamp Timestamp);

    private sealed class MessageSource : KafkaSource<ConsumedMessage, byte[], string>
    {
        protected override ConsumedMessage ConvertConsumeResult(
            ConsumeResult<byte[], string> consumeResult,
            Action<Exception, string>? logRowOnError = null
        ) =>
            new(
                consumeResult.Message.Key,
                consumeResult.Message.Value,
                consumeResult.Message.Timestamp
            );
    }

    [Fact]
    public void ShouldPassKeyAndPublishTimeToConversion()
    {
        // Arrange
        var key = new byte[] { 0x00, 0x01, 0xFE, 0xFF };
        var publishedAt = new DateTimeOffset(2026, 3, 14, 15, 9, 26, 535, TimeSpan.Zero);
        Produce(
            new Message<byte[], string>
            {
                Key = key,
                Value = "with key",
                Timestamp = new Timestamp(publishedAt),
            }
        );
        Produce(new Message<byte[], string> { Key = null!, Value = "without key" });

        var source = new MessageSource
        {
            ConsumerConfig = new ConsumerConfig
            {
                BootstrapServers = _fixture.BootstrapAddress,
                GroupId = $"{TopicName}-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnablePartitionEof = true,
                SocketTimeoutMs = 1000,
            },
            Topic = TopicName,
        };
        var destination = new MemoryDestination<ConsumedMessage>();
        source.LinkTo(destination);

        // Act
        source.Execute();
        destination.Wait();

        // Assert
        var messages = destination.Data.ToList();
        Assert.Equal(2, messages.Count);

        var withKey = messages[0];
        Assert.Equal("with key", withKey.Value);
        Assert.Equal(key, withKey.Key);
        Assert.Equal(TimestampType.CreateTime, withKey.Timestamp.Type);
        Assert.Equal(publishedAt.ToUnixTimeMilliseconds(), withKey.Timestamp.UnixTimestampMs);

        var withoutKey = messages[1];
        Assert.Equal("without key", withoutKey.Value);
        Assert.Null(withoutKey.Key);
    }

    private void Produce(Message<byte[], string> message)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _fixture.BootstrapAddress,
            SocketTimeoutMs = 1000,
        };
        using var producer = new ProducerBuilder<byte[], string>(config).Build();
        producer.Produce(TopicName, message);
        if (producer.Flush(TimeSpan.FromSeconds(10)) != 0)
            throw new TimeoutException("Message was not delivered to Kafka in time");
    }
}
