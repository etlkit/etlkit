# Defining data flows in XML

In addition to building a data flow in C# code, EtlKit can read a data flow from an **XML
configuration** with the optional `EtlKit.Serialization` package. This is useful when the pipeline
shape should be configurable without recompiling — for example stored in a file, a database, or sent
as part of a request.

The XML describes the same components you would otherwise `new` up in C#; at load time
`DataFlowXmlReader` instantiates each component, wires the `LinkTo` graph, and hands you a runnable
data flow.

> Add the package reference `EtlKit.Serialization` to use the types in this article.

## How the XML maps to components

The mapping is direct and mechanical:

- **An element name is a component's C# class name.** `<MemorySource>` creates a `MemorySource`,
  `<RowTransformation>` a `RowTransformation`, `<KafkaJsonSource>` a `KafkaJsonSource`, and so on.
  Any component the reader can find in the loaded assemblies can be used.
- **A child element is a property name** on that component. `<KafkaJsonSource><Topic>orders</Topic></KafkaJsonSource>`
  sets the `Topic` property. Value-type properties (string, int, enum, …) are parsed from the element
  text; class-typed properties are filled recursively from their own child elements.
- **`<LinkTo>` wires a component to the next.** The child of a `<LinkTo>` element is the target
  component (a transformation or destination) that the parent's output is linked to.
- **`<LinkErrorTo>` routes errors** to a destination of type `EtlKitError`, the XML equivalent of
  `LinkErrorTo(...)` in code.

## The root: an `IDataFlow` wrapper

The reader fills an object that implements `IDataFlow` (from `EtlKit.Serialization`). The interface
exposes the three collections the reader populates:

```csharp
public interface IDataFlow : IDisposable
{
    IConnectionManager GetOrAddConnectionManager(
        Type connectionManagerType, string? key, Func<Type, string?, IConnectionManager> factory);

    IDataFlowSource<ExpandoObject> Source { get; set; }
    IList<IDataFlowDestination<ExpandoObject>> Destinations { get; set; }
    IList<IDataFlowDestination<EtlKitError>> ErrorDestinations { get; set; }
}
```

EtlKit does not ship a concrete implementation — you provide a small wrapper. A minimal one that
delegates resource handling to `DataFlowResources` looks like this:

```csharp
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using EtlKit.Serialization;
using EtlKit.Serialization.DataFlow;

[Serializable]
public class EtlDataFlowStep : IDataFlow, IDataFlowResourceOwner, IXmlSerializable
{
    private readonly DataFlowResources _resources = new();

    public IDataFlowSource<ExpandoObject> Source { get; set; } = null!;
    public IList<IDataFlowDestination<ExpandoObject>> Destinations { get; set; } = null!;
    public IList<IDataFlowDestination<EtlKitError>> ErrorDestinations { get; set; } = null!;

    public IConnectionManager GetOrAddConnectionManager(
        Type type, string? key, Func<Type, string?, IConnectionManager> factory)
        => _resources.GetOrAddConnectionManager(type, key, factory);

    public IDisposable GetOrAddResource(string key, Func<IDisposable> factory)
        => _resources.GetOrAddResource(key, factory);

    public XmlSchema? GetSchema() => null;
    public void ReadXml(XmlReader reader) => new DataFlowXmlReader(this).Read(reader);
    public void WriteXml(XmlWriter writer) => throw new NotSupportedException();

    public void Dispose() => _resources.Dispose();
}
```

## Loading and running

The simplest entry point is the static `DataFlowXmlReader.Deserialize<T>`, which takes the XML, an
`ErrorLogDestination` for unrouted errors, and an optional `IServiceProvider` (used to resolve
components from a DI container):

```csharp
var errorDest = new ErrorLogDestination();
EtlDataFlowStep step = DataFlowXmlReader.Deserialize<EtlDataFlowStep>(xml, errorDest);

// Run it: start the source, then await every destination
step.Source.Execute();
Task.WaitAll(
    step.Destinations.Select(d => d.Completion)
        .Concat(step.ErrorDestinations.Select(e => e.Completion))
        .ToArray());
```

Because the wrapper implements `IXmlSerializable`, you can also deserialize it with a plain
`System.Xml.Serialization.XmlSerializer` — its `ReadXml` delegates to `DataFlowXmlReader`.

## A complete example

A `MemorySource` linked through a filter into a `MemoryDestination`. Note that only transformations
configured entirely by properties work in XML — such as `ExpressionRowFiltration`,
`RestTransformation`, `AIBatchTransformation` or `ScriptedTransformation`. A plain `RowTransformation`
does **not**, because its behaviour is a C# delegate that XML cannot supply.

```xml
<EtlDataFlowStep>
    <MemorySource>
        <LinkTo>
            <ExpressionRowFiltration>
                <FilterExpression>Amount &gt; 0</FilterExpression>
                <LinkTo>
                    <MemoryDestination />
                </LinkTo>
            </ExpressionRowFiltration>
        </LinkTo>
    </MemorySource>
</EtlDataFlowStep>
```

### Sequencing steps with `<Pipeline>`

Nesting one `<LinkTo>` per step gets verbose. The `<Pipeline>` element (from `EtlKit.Serialization`)
flattens a linear chain: list the steps as children and they are linked to each other in order.

- The **first child may be a source**; the remaining children are transformations chained one to the
  next.
- A `<LinkTo>` **inside** the `<Pipeline>` routes the pipeline's output to an external destination.
- Steps inside a `<Pipeline>` must **not** contain their own `<LinkTo>` — routing happens at the
  pipeline level.

```xml
<EtlDataFlowStep>
    <Pipeline>
        <MemorySource />
        <ExpressionRowFiltration>
            <FilterExpression>Amount &gt; 0</FilterExpression>
        </ExpressionRowFiltration>
        <LinkTo>
            <MemoryDestination />
        </LinkTo>
    </Pipeline>
</EtlDataFlowStep>
```

## Property mapping details

- **Connection managers** — an `IConnectionManager` property is created from its child elements and
  pooled per connection string via `GetOrAddConnectionManager`, so two components sharing the same
  connection string reuse one manager:

  ```xml
  <DbDestination>
      <ConnectionManager type="SqlConnectionManager">
          <ConnectionString>Data Source=.;Initial Catalog=demo;Integrated Security=SSPI;</ConnectionString>
      </ConnectionManager>
      <TableName>orders</TableName>
  </DbDestination>
  ```

- **Enums** are parsed from their member name (e.g. `<DeltaMode>Delta</DeltaMode>`).
- **Dictionaries** (`IDictionary<string, object?>`) are parsed from nested elements, where each
  child element name becomes a key.
- **XML special characters** in a value must be escaped: `&lt;` `&gt;` `&amp;` `&quot;`.

## Components that are commonly configured in XML

Many extension components are designed to be driven from XML. See their dedicated guides for the
exact element and property names:

- [Kafka source](kafka.md) — `<KafkaJsonSource>` with `<Topic>` and `<ConsumerConfig>`.
- [REST transformation](rest.md) — `<RestTransformation>` with `<RestMethodInfo>`.
- [AI transformation](ai.md) — `<AIBatchTransformation>` with `<ApiSettings>` and `<Prompt>`.
- [Row filtration](row-filtration.md) — `<ExpressionRowFiltration>` with `<FilterExpression>`.
- [Scripted transformation](scripted-transformation.md) — `<ScriptedTransformation>` with `<Mappings>`.
