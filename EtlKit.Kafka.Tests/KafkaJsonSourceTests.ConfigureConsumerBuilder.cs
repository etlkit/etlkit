using System.Dynamic;
using System.Threading.Tasks.Dataflow;
using EtlKit.DataFlow;
using Confluent.Kafka;
using Moq;

namespace EtlKit.Kafka.Tests;

public partial class KafkaJsonSourceTests
{
    private class FakeDeserializer : IDeserializer<string>
    {
        public string Deserialize(
            ReadOnlySpan<byte> data,
            bool isNull,
            SerializationContext context
        ) => "{\"name\": \"test\"}";
    }

    [Fact]
    public void ShouldInvokeCustomDeserializer()
    {
        // Arrange
        const string jsonString = "{}";
        ProduceJson(jsonString);
        var (block, target) = SetupMockTarget();
        target.Setup(t => t.TargetBlock).Returns(block.Object);
        var deserializer = new FakeDeserializer();
        var kafkaSource = new KafkaJsonSource<ExpandoObject>
        {
            ConsumerConfig = GetConsumerConfig(true),
            Topic = TopicName,
            ConfigureConsumerBuilder = builder =>
            {
                builder.SetValueDeserializer(deserializer);
            }
        };
        kafkaSource.LinkTo(target.Object);
        // Act
        kafkaSource.Execute();
        // Assert
        block.Verify(b =>
            b.OfferMessage(
                It.IsAny<DataflowMessageHeader>(),
                It.IsAny<ExpandoObject>(),
                It.IsAny<ISourceBlock<ExpandoObject>>(),
                It.IsAny<bool>()
            )
        );
        dynamic result = block.Invocations[0].Arguments[1];
        Assert.Equivalent(new Dictionary<string, object?>() { ["name"] = "test" }, result);
    }
}
