# Publishing to RabbitMQ

The `RabbitMqTransformation` (in the optional `EtlKit.RabbitMq` package) publishes one message per
input row to a RabbitMQ queue and then forwards the row to its output, so it can sit anywhere in a
flow. It is built on [RabbitMQ.Client](https://github.com/rabbitmq/rabbitmq-dotnet-client).

> Add the package reference `EtlKit.RabbitMq`.

## How it works

For each row the message body is produced by rendering `MessageTemplate` — a
[DotLiquid](https://github.com/dotliquid/dotliquid) template — against the row's fields, then
published to `Queue`. If the rendered message is empty the publish is skipped and the row is passed
through unchanged. Because the row continues to the output, you can publish *and* keep writing to a
database (or another destination) in the same flow.

The non-generic `RabbitMqTransformation` works on dynamic rows (`ExpandoObject`); the generic
`RabbitMqTransformation<TInput, TOutput>` lets you publish typed input and emit a different output
type.

## Configuration

| Property | Meaning |
|---|---|
| `ConnectionString` | AMQP URI, e.g. `amqp://guest:guest@localhost:5672/` |
| `Queue` | Target queue name |
| `MessageTemplate` | Liquid template for the message body, rendered from each row |
| `Properties` | Optional `RabbitMqProperties` — AMQP basic properties (persistence, priority, headers, content type, …) |

Instead of `ConnectionString` you can pass a configured `IConnectionFactory` to the constructor.

## Example

```csharp
var rabbit = new RabbitMqTransformation
{
    ConnectionString = "amqp://guest:guest@localhost:5672/",
    Queue = "orders",
    MessageTemplate = "{ \"orderId\": {{ Id }}, \"item\": \"{{ Item }}\" }"
};

source.LinkTo(rabbit);
rabbit.LinkTo(destination);   // rows still flow on after publishing
source.Execute();
destination.Wait();
```

To set message properties such as persistence:

```csharp
var rabbit = new RabbitMqTransformation
{
    ConnectionString = "amqp://guest:guest@localhost:5672/",
    Queue = "orders",
    MessageTemplate = "{{ Id }}",
    Properties = new RabbitMqProperties { Persistent = true, ContentType = "application/json" }
};
```

## XML configuration

In an [XML-defined data flow](xml-configuration.md) the element is `<RabbitMqTransformation>`:

```xml
<RabbitMqTransformation>
    <ConnectionString>amqp://guest:guest@localhost:5672/</ConnectionString>
    <Queue>orders</Queue>
    <MessageTemplate>{ "orderId": {{ Id }}, "item": "{{ Item }}" }</MessageTemplate>
</RabbitMqTransformation>
```
