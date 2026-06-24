# Tech Debt: `<Pipeline>` Flat-Sequence Sugar for DataFlowXmlReader

> **Status: COMPLETED** (2026-05-15) — shipped in 1.18.0 (`feature/MLRSSL-1510-pipeline-xml-sequence`).
> `Pipeline<TIn, TOut>` and the non-generic `Pipeline` live in `EtlKit.Serialization`, and the
> `IDataFlowXmlSerializable` / `IDataFlowXmlContext` extension points let any component own its XML
> deserialization while still creating child objects through the reader's DI-aware factory. See
> [CHANGELOG.md](../../CHANGELOG.md) entry for 1.18.0.

## Context

The current XML structure for sequential DataFlow pipelines requires nested `<LinkTo>` elements —
each step wraps the next, growing one indent per step. With four transformations the destination
sits six levels deep. The goal is a `<Pipeline>` container that lists steps in order at a flat
level, with the reader wiring `LinkTo` calls automatically.

```xml
<!-- Before: every transformation adds one indent level -->
<CsvSource>
  <Uri>file.csv</Uri>
  <LinkTo>
    <JsonTransformation>
      <LinkTo>
        <AiTransformation>
          <LinkTo><MemoryDestination/></LinkTo>
        </AiTransformation>
      </LinkTo>
    </JsonTransformation>
  </LinkTo>
</CsvSource>

<!-- After: flat sequence inside <LinkTo> -->
<CsvSource>
  <Uri>file.csv</Uri>
  <LinkTo>
    <Pipeline>
      <JsonTransformation/>
      <AiTransformation/>
      <MemoryDestination/>
    </Pipeline>
  </LinkTo>
</CsvSource>
```

---

## Design

### Extension-point interfaces (new, in `EtlKit.Serialization`)

```
IDataFlowXmlContext        — exposes CreateObject (DI-aware only — no method invocation)
IDataFlowXmlSerializable   — void ReadXml(XElement element, IDataFlowXmlContext context)
```

`DataFlowXmlReader` implements `IDataFlowXmlContext`. In `CreateInstance`, one generic `if` block
checks for `IDataFlowXmlSerializable` and delegates — no hard-coded name checks, fully extensible.

**Separation of concerns:**
- `IDataFlowXmlContext` is solely a DI-aware object factory. The reader holds an `IDataFlowActivator`
  (optionally backed by `IServiceProvider`) and `context.CreateObject` routes through it. Components
  implementing `IDataFlowXmlSerializable` must use `context.CreateObject` for all object creation.
- Method invocation (`<LinkTo>`, `<LinkErrorTo>`) is entirely the component's own responsibility
  inside `ReadXml`. The context has no knowledge of methods; it only creates objects. `Pipeline`
  handles method-like XML elements with a private reflection helper that finds methods by element
  name on `this` and calls them — the context is not involved.

### Class hierarchy

```
DataFlowTransformation<TIn, TOut>         (existing base class)
  └── Pipeline<TIn, TOut>                 (new — transformation pipeline)
        └── Pipeline                      (new — ExpandoObject pipeline, also IDataFlowSource)
```

### `Pipeline<TIn, TOut>` — transformation pipeline (primary role)

- Inherits `DataFlowTransformation<TIn, TOut>` — gets all `LinkTo` overloads, `ITask` members,
  predecessor completions, and the base error handler for free
- Stores all steps in `List<object> _steps` for error forwarding
- Wires `TransformBlock = DataflowBlock.Encapsulate(head.TargetBlock, tail.SourceBlock)` —
  same pattern already used by `RowBatchTransformation`
- **Overrides `LinkErrorTo`** to forward the call to every step implementing `ILinkErrorSource`;
  a single `pipeline.LinkErrorTo(dest)` covers the entire internal chain

### `Pipeline` (non-generic) — ExpandoObject pipeline

Inherits `Pipeline<ExpandoObject, ExpandoObject>` and additionally implements
`IDataFlowSource<ExpandoObject>`, making it detectable as a root source by `DataFlowXmlReader`
when needed.

#### Special case A — source as first child

If the first child resolves to `IDataFlowSource<ExpandoObject>`, the Pipeline stores it as
`_source` and uses the **second** child as `_head`.

**Completion wiring (important):** `_source` must NOT be linked via EtlKit's `LinkTo` —
doing so would register `_source.SourceBlock.Completion` in `_head.PredecessorCompletions`,
not in `pipeline.PredecessorCompletions`. If an external upstream is also connected,
`_head.CheckCompleteAction()` would fire as soon as `_source` alone finishes, closing
`_head.TargetBlock` before the external data arrives.

Instead, wire `_source` at the raw TPL Dataflow level and register its completion in
Pipeline's own predecessor list:

```csharp
// Raw TPL link — bypasses EtlKit completion registration on _head
_source.SourceBlock.LinkTo(_head.TargetBlock);
// Register in Pipeline's predecessor list so CheckCompleteAction waits for both
AddPredecessorCompletion(_source.SourceBlock.Completion);
```

`pipeline.CheckCompleteAction()` then calls `Task.WhenAll(PredecessorCompletions)`, which
includes both the external upstream completion and `_source.SourceBlock.Completion`.
`_head.TargetBlock.Complete()` is called only after **all** sources are done.

```xml
<!-- Pipeline at root level with internal source -->
<Pipeline>
  <CsvSource><Uri>file.csv</Uri></CsvSource>
  <JsonTransformation/>
  <MemoryDestination/>
</Pipeline>

<!-- Pipeline inside <LinkTo> with external source driving it -->
<ExternalSource>
  <LinkTo>
    <Pipeline>
      <JsonTransformation/>
      <MemoryDestination/>
    </Pipeline>
  </LinkTo>
</ExternalSource>
```

`Execute()` / `ExecuteAsync()` delegate to `_source` when present; throw
`InvalidOperationException` if called with no internal source and no external driver.

#### Special case B — auto `VoidDestination` when last step has unclaimed output

If the last step implements `IDataFlowLinkSource<ExpandoObject>` and no external `LinkTo` has
been bound, `Execute()` automatically links a `VoidDestination<ExpandoObject>` and registers it
in `_dataFlow.Destinations` so the execution loop can wait for completion.

Tracking mechanism: override all six `LinkTo` overloads to set `_outputBound = true` before
delegating to base. This requires marking `LinkTo` (and `LinkErrorTo`, see below) as `virtual`
in `DataFlowTransformation<TIn, TOut>` — a one-keyword non-breaking change to the base class.

`override` (rather than `new`/shadow) is essential: it makes tracking dispatch-path-independent.
A shadow-based approach would silently bypass `_outputBound` when the call went through the
interface or a base-class reference, e.g.

```csharp
IDataFlowLinkSource<ExpandoObject> src = pipeline;
src.LinkTo(dest); // shadow not invoked → _outputBound stays false
```

`EnsureOutputBound` would then add a `VoidDestination` while `dest` was already linked, producing
a silent duplicate-output bug. With `virtual`/`override`, every dispatch path (interface,
base ref, derived ref, reflection) routes through Pipeline's tracking method.

At `Execute()` time, call `EnsureOutputBound()`:

```csharp
private bool _outputBound;

public override IDataFlowLinkSource<ExpandoObject> LinkTo(
    IDataFlowLinkTarget<ExpandoObject> target)
{
    _outputBound = true;
    return base.LinkTo(target);
}
// ... same for the other 5 overloads

private void EnsureOutputBound()
{
    if (_outputBound) return;
    if (_tail is IDataFlowDestination<ExpandoObject>) return;

    var sink = new VoidDestination<ExpandoObject>();
    _tail.LinkTo(sink);
    _outputBound = true;
}
```

`Pipeline` manages this tracking entirely on its own — it does not register anything with the
outer `DataFlow`. When Pipeline is used standalone, `DataFlowXmlReader` treats it as an
`IDataFlowSource` and calls `Execute()` on it; Pipeline's own `SourceBlock.Completion` signals
when the internal chain is done.

#### `<LinkTo>` and `<LinkErrorTo>` as direct children of `<Pipeline>`

Both are method invocations, not step types. `Pipeline.ReadXml` calls
`TryInvokeXmlMethod(child, context)` before attempting `context.CreateObject`. This helper
(on `Pipeline<TIn,TOut>`) finds methods by element name via reflection on `this` — no element
names are hardcoded, and the context is not involved in method dispatch.

- **`<LinkTo>`** — calls `this.LinkTo(target)`, connecting Pipeline's output to the next
  component. Works identically to `<LinkTo>` on any other EtlKit component.
- **`<LinkErrorTo>`** — calls `this.LinkErrorTo(target)`, which forwards to **all** internal
  steps via the `LinkErrorTo` override in `Pipeline<TIn, TOut>`. Placement has no effect.

Elements inside the internal steps (e.g. `<JsonTransformation><LinkTo>…`) are **forbidden** —
`context.CreateObject` creates each step from its own `XElement` without following its link
children. If a step's XML contains `<LinkTo>` or `<LinkErrorTo>`, `ReadXml` throws
`InvalidDataException`.

In XML mode, `_linkAllErrorsTo` on `DataFlowXmlReader` already auto-wires each step during
`CreateInstance`, so an explicit `<LinkErrorTo>` inside `<Pipeline>` is usually not needed.

---

## Files to create / modify

| File | Change |
|------|--------|
| `EtlKit.Common/DataFlow/DataFlowTransformation.cs` | Modify — mark all 6 `LinkTo` overloads and `LinkErrorTo` as `virtual` |
| `EtlKit.Serialization/DataFlow/IDataFlowXmlContext.cs` | New |
| `EtlKit.Serialization/DataFlow/IDataFlowXmlSerializable.cs` | New |
| `EtlKit.Serialization/DataFlow/Pipeline.cs` | New (both classes) |
| `EtlKit.Serialization/DataFlow/DataFlowXmlReader.cs` | Modify — implement context + delegate |

---

## Step 1 — `IDataFlowXmlContext`

`IDataFlowXmlContext` is purely a DI-aware object factory — no method invocation, no
ExpandoObject coupling. `Pipeline` manages its own execution and completion via
`IDataFlowSource`; it has no need to register objects back into the outer DataFlow.

`DataFlowXmlReader` holds an `IDataFlowActivator` (backed by an optional `IServiceProvider`).
`context.CreateObject` delegates to `DataFlowXmlReader.CreateObject` → `_activator.CreateInstance(type)`,
so all objects resolved via `context.CreateObject` inside `ReadXml` share the same DI container
as the rest of the flow. This is the primary reason `IDataFlowXmlContext` is passed to `ReadXml`.

Any `ReadXml` implementation **must** use `context.CreateObject` for all object instantiation;
using `new` or `Activator.CreateInstance` directly bypasses DI.

```csharp
public interface IDataFlowXmlContext
{
    /// <summary>
    /// Resolves a registered type by name without creating an instance.
    /// Used for type inspection (e.g. checking if the first child is a source) before
    /// committing to object creation.
    /// </summary>
    Type? ResolveType(string typeName);

    /// <summary>
    /// Creates an object using the reader's activator (DI-aware). Always use this instead of
    /// new/Activator.CreateInstance to preserve dependency injection.
    /// </summary>
    object? CreateObject(string typeName, XElement element);
}
```

`DataFlowXmlReader` implements `IDataFlowXmlContext`.

## Step 2 — `IDataFlowXmlSerializable`

```csharp
public interface IDataFlowXmlSerializable
{
    void ReadXml(XElement element, IDataFlowXmlContext context);
}
```

## Step 3 — `Pipeline<TIn, TOut>`

```csharp
[PublicAPI]
public class Pipeline<TIn, TOut> : DataFlowTransformation<TIn, TOut>, IDataFlowXmlSerializable
{
    protected readonly List<object> Steps = new();
    protected IDataFlowLinkTarget<TIn>? Head;
    protected IDataFlowLinkSource<TOut>? Tail;

    protected void SetHeadAndTail(IDataFlowLinkTarget<TIn> head, IDataFlowLinkSource<TOut> tail)
    {
        Head = head;
        Tail = tail;
        TransformBlock = DataflowBlock.Encapsulate(head.TargetBlock, tail.SourceBlock);
    }

    public override void LinkErrorTo(IDataFlowLinkTarget<EtlKitError> target)
    {
        foreach (var step in Steps)
            if (step is ILinkErrorSource src)
                src.LinkErrorTo(target);
    }

    // Processes children[startIndex..] as pipeline steps, linking them in sequence.
    // New steps are appended to the shared Steps list. Head/Tail are set via SetHeadAndTail.
    // Subclasses override ReadXml and call ReadSteps to share the wiring loop without duplication.
    protected void ReadSteps(IList<XElement> children, int startIndex, IDataFlowXmlContext context)
    {
        int stepsStartIndex = Steps.Count;
        object? prev = null;
        Type? prevOutputType = null;

        for (int i = startIndex; i < children.Count; i++)
        {
            var child = children[i];
            // Method-like elements (LinkTo, LinkErrorTo, etc.) are invoked on this via reflection.
            // The context is not involved — it only creates objects; method dispatch is Pipeline's
            // own responsibility.
            if (TryInvokeXmlMethod(child, context)) continue;

            var step = context.CreateObject(child.Name.LocalName, child)
                       ?? throw new InvalidOperationException($"...");
            Steps.Add(step);

            // Validate that step accepts the output type of the previous step.
            // GetLinkTargetInputType() finds IDataFlowLinkTarget<T> via reflection and returns T.
            var inputType = GetLinkTargetInputType(step);
            if (prevOutputType != null && inputType != prevOutputType)
                throw new InvalidDataException(
                    $"Type mismatch at '{child.Name.LocalName}': " +
                    $"expected IDataFlowLinkTarget<{prevOutputType.Name}>, got IDataFlowLinkTarget<{inputType?.Name}>");

            // Link previous step to this one via TPL (type-erased) and register completion.
            if (prev != null && prevOutputType != null)
                LinkSteps(prev, prevOutputType, step); // reflection helper

            // GetLinkSourceOutputType() finds IDataFlowLinkSource<T> and returns T.
            prevOutputType = GetLinkSourceOutputType(step);
            prev = step;
        }

        if (Steps.Count == stepsStartIndex) return;

        // Validate head and tail match TIn / TOut.
        // stepsStartIndex points to the first step added by this call (not necessarily Steps[0]).
        var head = Steps[stepsStartIndex] as IDataFlowLinkTarget<TIn>
                   ?? throw new InvalidDataException(
                       $"First step must implement IDataFlowLinkTarget<{typeof(TIn).Name}>");
        var tail = Steps[^1] as IDataFlowLinkSource<TOut>
                   ?? throw new InvalidDataException(
                       $"Last step must implement IDataFlowLinkSource<{typeof(TOut).Name}>");
        SetHeadAndTail(head, tail);
    }

    public virtual void ReadXml(XElement element, IDataFlowXmlContext context)
    {
        var children = element.Elements().ToList();
        if (children.Count == 0) return;
        ReadSteps(children, 0, context);
    }

    // Extracts T from IDataFlowLinkTarget<T> via reflection.
    private static Type? GetLinkTargetInputType(object step) =>
        step.GetType().GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                 i.GetGenericTypeDefinition() == typeof(IDataFlowLinkTarget<>))
            ?.GetGenericArguments()[0];

    // Extracts T from IDataFlowLinkSource<T> via reflection.
    private static Type? GetLinkSourceOutputType(object step) =>
        step.GetType().GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                 i.GetGenericTypeDefinition() == typeof(IDataFlowLinkSource<>))
            ?.GetGenericArguments()[0];

    // Finds the method on this whose name matches element.Name.LocalName, creates each child
    // element as a step via context.CreateObject (DI-aware), and invokes the method with it.
    // Returns false if no matching method exists (element is a step, not a method call).
    // This is Pipeline's own responsibility — the context is not involved in method dispatch.
    private bool TryInvokeXmlMethod(XElement element, IDataFlowXmlContext context)
    {
        var method = GetType().GetMethods()
            .Where(m => m.Name == element.Name.LocalName && m.GetParameters().Length == 1)
            .FirstOrDefault();
        if (method is null) return false;

        foreach (var childEl in element.Elements())
        {
            var target = context.CreateObject(childEl.Name.LocalName, childEl)
                         ?? throw new InvalidOperationException($"...");
            method.Invoke(this, new[] { target });
        }
        return true;
    }

    // Links source.SourceBlock → target.TargetBlock via TPL using reflection (type-erased).
    // Registers target.AddPredecessorCompletion so CheckCompleteAction works correctly.
    private static void LinkSteps(object source, Type itemType, object target)
    {
        // Equivalent to (at runtime):
        //   var sourceBlock = ((IDataFlowLinkSource<T>)source).SourceBlock;
        //   var targetBlock = ((IDataFlowLinkTarget<T>)target).TargetBlock;
        //   sourceBlock.LinkTo(targetBlock);
        //   ((IDataFlowLinkTarget<T>)target).AddPredecessorCompletion(sourceBlock.Completion);
        var linkSourceType = typeof(IDataFlowLinkSource<>).MakeGenericType(itemType);
        var linkTargetType = typeof(IDataFlowLinkTarget<>).MakeGenericType(itemType);
        var sourceBlock = linkSourceType.GetProperty("SourceBlock")!.GetValue(source);
        var targetBlock = linkTargetType.GetProperty("TargetBlock")!.GetValue(target);
        // ISourceBlock<T>.LinkTo(ITargetBlock<T>) via reflection
        typeof(DataflowBlock)
            .GetMethod("LinkTo", new[] { typeof(ISourceBlock<>).MakeGenericType(itemType),
                                        typeof(ITargetBlock<>).MakeGenericType(itemType) })!
            .Invoke(null, new[] { sourceBlock, targetBlock });
        // Register completion
        var completion = sourceBlock!.GetType().GetProperty("Completion")!.GetValue(sourceBlock);
        linkTargetType.GetMethod("AddPredecessorCompletion")!.Invoke(target, new[] { completion });
    }
}
```

## Step 4 — `Pipeline` (non-generic)

```csharp
[PublicAPI]
public sealed class Pipeline : Pipeline<ExpandoObject, ExpandoObject>, IDataFlowSource<ExpandoObject>
{
    private IDataFlowSource<ExpandoObject>? _source;
    private bool _outputBound;

    // IDataFlowSource
    public void Execute(CancellationToken cancellationToken = default)
    {
        EnsureOutputBound();
        _source?.Execute(cancellationToken);
    }

    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        EnsureOutputBound();
        return _source?.ExecuteAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Pipeline has no internal source. Drive it by linking an external source.");
    }

    // Override all 6 LinkTo overloads to set _outputBound.
    // Requires LinkTo to be `virtual` in DataFlowTransformation<TIn,TOut> — base-class change.
    // `override` (not `new`/shadow) is required so calls via interface, base-class reference,
    // or reflection also hit this method; a shadow would silently bypass tracking on those paths
    // and produce a duplicate-output bug (VoidDestination + real target).
    public override IDataFlowLinkSource<ExpandoObject> LinkTo(
        IDataFlowLinkTarget<ExpandoObject> target)
    {
        _outputBound = true;
        return base.LinkTo(target);
    }
    // ... remaining 5 overloads identical pattern

    private void EnsureOutputBound()
    {
        if (_outputBound || Tail is IDataFlowDestination<ExpandoObject>) return;
        if (Tail == null) return;

        var sink = new VoidDestination<ExpandoObject>();
        Tail.LinkTo(sink);
        _outputBound = true;
    }

    public override void ReadXml(XElement element, IDataFlowXmlContext context)
    {
        var children = element.Elements().ToList();
        if (children.Count == 0) return;

        int stepStart = 0;
        var firstType = context.ResolveType(children[0].Name.LocalName);
        if (firstType != null && typeof(IDataFlowSource<ExpandoObject>).IsAssignableFrom(firstType))
        {
            _source = context.CreateObject(children[0].Name.LocalName, children[0])
                as IDataFlowSource<ExpandoObject>
                ?? throw new InvalidDataException(
                    $"'{children[0].Name.LocalName}' resolved as source type but could not be cast to IDataFlowSource<ExpandoObject>.");
            Steps.Add(_source);
            stepStart = 1;
        }

        if (stepStart < children.Count)
            ReadSteps(children, stepStart, context);

        // Raw TPL link: bypasses EtlKit completion registration on Head so that
        // _source.SourceBlock.Completion goes into Pipeline's PredecessorCompletions,
        // not into Head.PredecessorCompletions. This prevents the race condition where
        // Head closes before an external upstream finishes.
        if (_source != null && Head != null)
        {
            _source.SourceBlock.LinkTo(Head.TargetBlock);
            AddPredecessorCompletion(_source.SourceBlock.Completion);
        }
    }
}
```

## Step 5 — `DataFlowXmlReader` changes

### 5a — Implement `IDataFlowXmlContext`

```csharp
public sealed class DataFlowXmlReader : IDataFlowXmlContext
{
    // Type resolution without instantiation — used by Pipeline.ReadXml for source detection.
    Type? IDataFlowXmlContext.ResolveType(string typeName) =>
        GetTypeByName(_types, typeName);

    // DI-aware object creation — delegates to the existing CreateObject/GetTypeByName machinery.
    object? IDataFlowXmlContext.CreateObject(string typeName, XElement element) =>
        CreateObject(GetTypeByName(_types, typeName), element);
}
```

### 5b — Delegate in `CreateInstance` (~line 311)

```csharp
if (instance is IDataFlowXmlSerializable xmlSerializable)
{
    xmlSerializable.ReadXml((XElement)node, this);
    ApplyLinkAllErrorsTo(instance);  // extracted helper from existing _linkAllErrorsTo block
    return instance;
}
```

---

## Tests to add

File: `EtlKit.Serialization.Tests/DataFlowPipelineTests.cs`

1. **Transformation pipeline** — source → `<Pipeline>` with 2 transforms + destination runs correctly.
2. **Internal source** — `<Pipeline>` at root level with source as first child executes and produces output.
3. **Auto-void destination** — last step is transformation with no external `LinkTo`; pipeline completes without stalling.
4. **External `LinkTo` suppresses auto-void** — output flows to external destination only.
5. **`LinkErrorTo` forwarding** — `pipeline.LinkErrorTo(dest)` routes errors from all internal steps.
6. **`<LinkErrorTo>` in XML** — `<LinkErrorTo>` child element inside `<Pipeline>` wires error routing.
7. **`_linkAllErrorsTo` in XML** — universal error destination covers all steps automatically.
8. **Non-source first step in root position** — throws informative error.
9. **Non-linkable step** — step not implementing `IDataFlowLinkTarget` throws `InvalidDataException`.
10. **Backward compat** — existing nested `<LinkTo>` XML works alongside a `<Pipeline>` flow.
11. **Generic `Pipeline<TIn, TOut>`** — typed pipeline links and propagates data; `LinkErrorTo` forwards.
12. **`IDataFlowXmlSerializable` extensibility** — custom component's `ReadXml` is delegated correctly.

---

## Verification

```bash
dotnet build EtlKit.Serialization/EtlKit.Serialization.csproj
dotnet test EtlKit.Serialization.Tests/EtlKit.Serialization.Tests.csproj --filter "Pipeline"
```
