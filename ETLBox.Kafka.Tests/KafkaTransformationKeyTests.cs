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

    // Demonstrates that the base class is not limited to string keys: a non-string (byte[]) key type
    // plus a key resolver delegate produces a keyed message of that type.
    private class TestableBytesKafkaTransformation
        : KafkaTransformation<ExpandoObject, byte[], string>
    {
        public TestableBytesKafkaTransformation(
            IProducer<byte[], string> producer,
            Func<ExpandoObject, byte[]>? keyResolver
        )
            : base(producer)
        {
            MessageKeyResolver = keyResolver;
        }

        protected override string BuildMessageValue(ExpandoObject input) => "value";
    }

    private static (
        Mock<IProducer<byte[], string>> producer,
        List<Message<byte[], string>> captured
    ) CreateCapturingBytesProducer()
    {
        var captured = new List<Message<byte[], string>>();
        var mockProducer = new Mock<IProducer<byte[], string>>();
        mockProducer
            .Setup(p =>
                p.Produce(
                    It.IsAny<string>(),
                    It.IsAny<Message<byte[], string>>(),
                    It.IsAny<Action<DeliveryReport<byte[], string>>>()
                )
            )
            .Callback<string, Message<byte[], string>, Action<DeliveryReport<byte[], string>>>(
                (_, message, _) => captured.Add(message)
            );
        return (mockProducer, captured);
    }

    [Fact]
    public void ShouldProduceMessageWithNonStringKey_WhenResolverSet()
    {
        // Arrange: base class parametrised with a non-string (byte[]) key + a key resolver delegate
        var (mockProducer, captured) = CreateCapturingBytesProducer();
        var expectedKey = new byte[] { 1, 2, 3 };
        var data = new ExpandoObject();

        var transformation = new TestableBytesKafkaTransformation(
            mockProducer.Object,
            _ => expectedKey
        )
        {
            TopicName = "test-topic",
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
        Assert.Equal(expectedKey, captured[0].Key);
        Assert.Equal("value", captured[0].Value);
    }

    [Fact]
    public void ShouldProduceKeyForEveryRow_WhenKeyTemplateSet()
    {
        // Arrange: several rows. The keyed/keyless decision is made once (template set),
        // so every row must get a key - no per-row mixing of keyed and keyless messages.
        var (mockProducer, captured) = CreateCapturingProducer();

        dynamic row1 = new ExpandoObject();
        row1.loyalty_program_id = 2;
        row1.transaction_id = 100;
        dynamic row2 = new ExpandoObject();
        row2.loyalty_program_id = 2;
        row2.transaction_id = 200;

        var transformation = new TestableStringKafkaTransformation(mockProducer.Object)
        {
            TopicName = "test-topic",
            MessageTemplate = "{{transaction_id}}",
            MessageKeyTemplate = "{{loyalty_program_id}}:{{transaction_id}}",
        };
        var source = new MemorySource<ExpandoObject>(new ExpandoObject[] { row1, row2 });
        var dest = new MemoryDestination<ExpandoObject?>();

        // Act
        source.LinkTo(transformation);
        transformation.LinkTo(dest);
        source.Execute();
        dest.Wait();

        // Assert: both rows produced and keyed, none keyless
        Assert.Equal(2, captured.Count);
        Assert.All(captured, m => Assert.NotNull(m.Key));
        Assert.Equal(
            new[] { "2:100", "2:200" },
            captured.Select(m => m.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray()
        );
    }

    [Fact]
    public void ShouldProduceMessageWithoutKey_WhenResolverNotSet_OnNonStringKeyType()
    {
        // Arrange: generic (byte[]) key base WITHOUT a resolver -> keyless for every row
        var (mockProducer, captured) = CreateCapturingBytesProducer();
        var data = new ExpandoObject();

        var transformation = new TestableBytesKafkaTransformation(
            mockProducer.Object,
            keyResolver: null
        )
        {
            TopicName = "test-topic",
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
