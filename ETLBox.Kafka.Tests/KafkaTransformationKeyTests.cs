using System.Dynamic;
using ALE.ETLBox.DataFlow;
using Confluent.Kafka;
using Moq;

namespace ETLBox.Kafka.Tests;

public class KafkaTransformationKeyTests
{
    private class TestableStringKafkaTransformation : KafkaStringTransformation<ExpandoObject>
    {
        public TestableStringKafkaTransformation(IProducer<string, string> producer)
            : base(producer) { }
    }

    private static (
        Mock<IProducer<string, string>> producer,
        List<Message<string, string>> captured
    ) CreateCapturingProducer()
    {
        var captured = new List<Message<string, string>>();
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
                (_, message, _) => captured.Add(message)
            );
        return (mockProducer, captured);
    }

    [Fact]
    public void ShouldProduceMessageWithRenderedKey_WhenKeyTemplateSet()
    {
        // Arrange
        var (mockProducer, captured) = CreateCapturingProducer();

        dynamic data = new ExpandoObject();
        data.loyalty_program_id = 2;
        data.transaction_id = 12345;

        var transformation = new TestableStringKafkaTransformation(mockProducer.Object)
        {
            TopicName = "test-topic",
            MessageTemplate = "{{transaction_id}}",
            MessageKeyTemplate = "{{loyalty_program_id}}:{{transaction_id}}",
        };
        var source = new MemorySource<ExpandoObject>(new ExpandoObject[] { data });
        var dest = new MemoryDestination<ExpandoObject?>();

        // Act
        source.LinkTo(transformation);
        transformation.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert
        Assert.Single(captured);
        Assert.Equal("2:12345", captured[0].Key);
        Assert.Equal("12345", captured[0].Value);
    }

    [Fact]
    public void ShouldProduceMessageWithoutKey_WhenKeyTemplateNotSet()
    {
        // Arrange
        var (mockProducer, captured) = CreateCapturingProducer();

        dynamic data = new ExpandoObject();
        data.transaction_id = 12345;

        var transformation = new TestableStringKafkaTransformation(mockProducer.Object)
        {
            TopicName = "test-topic",
            MessageTemplate = "{{transaction_id}}",
        };
        var source = new MemorySource<ExpandoObject>(new ExpandoObject[] { data });
        var dest = new MemoryDestination<ExpandoObject?>();

        // Act
        source.LinkTo(transformation);
        transformation.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert
        Assert.Single(captured);
        Assert.Null(captured[0].Key);
    }
}
