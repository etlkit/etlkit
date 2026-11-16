# Reading from Kafka

The `EtlKit.Kafka` package adds an Apache Kafka **source** that consumes messages from a topic and
emits them into the data flow. It is built on [Confluent.Kafka](https://github.com/confluentinc/confluent-kafka-dotnet),
so consumer configuration uses the standard `ConsumerConfig` type.

> Add the package reference `EtlKit.Kafka` to use the Kafka components. The Kafka **producer**
> (writing messages from a flow) is the `KafkaTransformation` covered in
> [Transformations](transformations.md#kafkatransformation).

## KafkaJsonSource

`KafkaJsonSource<TOutput>` reads messages whose value is JSON and deserializes each one into
`TOutput` (a POCO, or `ExpandoObject` for the non-generic dynamic form). It derives from the abstract
`KafkaSource<TOutput, TKafkaValue>`, which ignores the message key and passes only the value to the
conversion. The subscription and the consume loop live in `KafkaSource<TOutput, TKafkaKey, TKafkaValue>`
(see [below](#reading-the-key-and-message-metadata)).

| Property | Meaning |
|---|---|
| `Topic` | The topic to subscribe to |
| `ConsumerConfig` | A `Confluent.Kafka.ConsumerConfig` — at minimum `BootstrapServers` and `GroupId` |
| `ConfigureConsumerBuilder` | Optional `Action` to customize the underlying `ConsumerBuilder` (deserializers, handlers) before it is built |
| `JsonSerializerOptions` | Options used to deserialize the JSON message value |

## Reading the key and message metadata

`KafkaSource<TOutput, TKafkaKey, TKafkaValue>` is the base for a source that needs more than the
message value: the key, the publish time (`Message.Timestamp`), the headers, or the partition and
offset. Derive from it and override `ConvertConsumeResult`, which receives the whole
`ConsumeResult<TKafkaKey, TKafkaValue>`. It has the same `Topic`, `ConsumerConfig` and
`ConfigureConsumerBuilder` properties (the builder is `ConsumerBuilder<TKafkaKey, TKafkaValue>`) and
the same consume loop:

- the end of a partition (with `EnablePartitionEof`) stops reading;
- a message with a `null` value is skipped before `ConvertConsumeResult` is called; a `null` key is
  passed through;
- a conversion that returns `null` emits nothing; report the failed row through `logRowOnError` to
  send it to the error output;
- a `ConsumeException` (for example, a deserialization error) goes to the error output, when one is
  linked, with the offset and the raw key and value in base64; without an error output it is
  rethrown.

`TKafkaKey` selects the key deserializer: `byte[]` gives the raw bytes (no decoding errors for keys
written in any format), `string` decodes UTF-8, `Ignore` skips the key. `KafkaSource<TOutput, TKafkaValue>`
is this class with `TKafkaKey = Ignore`.

```csharp
using Confluent.Kafka;

public record OrderEvent(byte[]? Key, string Payload, DateTime PublishedUtc, int Partition, long Offset);

public class OrderEventSource : KafkaSource<OrderEvent, byte[], string>
{
    protected override OrderEvent ConvertConsumeResult(
        ConsumeResult<byte[], string> consumeResult,
        Action<Exception, string>? logRowOnError = null
    ) =>
        new(
            consumeResult.Message.Key,
            consumeResult.Message.Value,
            consumeResult.Message.Timestamp.UtcDateTime,
            consumeResult.Partition.Value,
            consumeResult.Offset.Value
        );
}
```

`Message.Timestamp.Type` tells where the time comes from: `CreateTime` (set by the producer),
`LogAppendTime` (set by the broker, depending on the topic's `message.timestamp.type`) or
`NotAvailable`.

## Streaming semantics

A Kafka source is a **streaming source**: `Execute(CancellationToken)` subscribes to the topic and
keeps consuming until the token is cancelled — it does not signal completion on its own. Drive it
with a `CancellationToken` and stop it by cancelling. (See [Streaming sources](streaming-sources.md)
for the general model and for checkpoint-based resume with other streaming components.)

## Example

```csharp
using Confluent.Kafka;

using var cts = new CancellationTokenSource();

var source = new KafkaJsonSource<MyEvent>
{
    Topic = "orders",
    ConsumerConfig = new ConsumerConfig
    {
        BootstrapServers = "localhost:9092",
        GroupId = "etl-consumer",
        AutoOffsetReset = AutoOffsetReset.Earliest
    }
};

DbDestination<MyEvent> dest = new DbDestination<MyEvent>(connection, "orders");
source.LinkTo(dest);

source.Execute(cts.Token);   // consumes until cts is cancelled
dest.Wait();
```

For the dynamic form, use the non-generic `KafkaJsonSource` (which produces `ExpandoObject` rows).

## XML configuration

In an [XML-defined data flow](xml-configuration.md) the element is `<KafkaJsonSource>`; the consumer
settings are nested under `<ConsumerConfig>`:

```xml
<KafkaJsonSource>
    <Topic>orders</Topic>
    <ConsumerConfig>
        <BootstrapServers>localhost:9092</BootstrapServers>
        <GroupId>etl-consumer</GroupId>
    </ConsumerConfig>
    <LinkTo>
        <DbDestination>
            <TableName>orders</TableName>
        </DbDestination>
    </LinkTo>
</KafkaJsonSource>
```
