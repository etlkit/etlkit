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
`KafkaSource<TOutput, TKafkaValue>`, which handles subscription and the consume loop.

| Property | Meaning |
|---|---|
| `Topic` | The topic to subscribe to |
| `ConsumerConfig` | A `Confluent.Kafka.ConsumerConfig` — at minimum `BootstrapServers` and `GroupId` |
| `ConfigureConsumerBuilder` | Optional `Action` to customize the underlying `ConsumerBuilder` (deserializers, handlers) before it is built |
| `JsonSerializerOptions` | Options used to deserialize the JSON message value |

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
