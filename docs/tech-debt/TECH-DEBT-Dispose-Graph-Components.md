# Tech Debt: dispose graph components, not just their IDisposable properties

## Context

Came out of MR !5 (Kafka, RSSL-11867). `KafkaTransformation` acquired an internal `IProducer`
that must be flushed and disposed. It was made to implement `IDisposable`, but nothing calls that
`Dispose()` automatically — so the producer is only released by its own finalizer, non-deterministically.

Digging into how the flow releases resources showed the ownership model is **property-centric**, not
**graph-centric**: the flow disposes the `IDisposable` *properties* of components, but never the
components themselves.

## Current state

The reusable owner is `DataFlowResources` (`IDataFlowResourceOwner`). Its `Dispose()` releases exactly
two pools:

- connection managers added via `GetOrAddConnectionManager`;
- arbitrary `IDisposable` resources added via `GetOrAddResource`.

Those pools are populated by `DataFlowXmlReader` **only while binding properties**:

- a property typed `IConnectionManager` → `GetOrAddConnectionManager`
  (`SetInterfaceProperty`);
- a property whose type is `IDisposable` → `GetOrAddResource` (`AssignDisposableResource`,
  in `SetClassProperty` / `SetInterfaceProperty`).

Graph nodes — sources, transformations, destinations — are created via `CreateObject → CreateInstance`
(and, for link targets, `AddDestinationAndInvokeMethod`). Neither path checks `instance is IDisposable`
nor registers the node in any pool. The reference `EtlDataFlow` (`EtlDataFlowStep` in the serialization
tests) disposes only `_resources` — it does not walk `Source` / `Destinations` / transformations.

**Consequence:** a component that implements `IDisposable` (like `KafkaTransformation`) is never disposed
by the flow. Making a component `IDisposable` today does nothing automatic. Any resource a component owns
internally (a Kafka producer, an internally-created `HttpClient`, etc.) leaks unless the component also
exposes that resource as an `IDisposable` *property* so the reader happens to register it.

## Problem

1. Ownership does not follow the object graph. The natural expectation — "if a component is
   `IDisposable`, the flow that owns it disposes it" — does not hold.
2. Split / ambiguous ownership. If a component is `IDisposable` **and** has `IDisposable` properties,
   the reader registers those properties with the flow individually, so the flow and the component both
   have a claim on them. A self-disposing component should own its own properties instead.
3. Code-built pipelines have no owner at all, so this only improves the `IDataFlow` (serialized) path —
   see "Non-goals".

## Proposed direction

Make disposal follow the graph, keyed on whether the component itself is `IDisposable`:

- **Component implements `IDisposable`** → the flow owns and disposes the **component**; the component is
  responsible for its own properties/resources. The reader must **not** separately register that
  component's `IDisposable` properties into the flow pool (the component owns its subtree).
- **Component does not implement `IDisposable`** → keep today's behavior: the flow registers and disposes
  the component's `IDisposable` properties (and connection managers) individually.

Sketch:

- When `DataFlowXmlReader` instantiates a top-level component (in `CreateInstance` and/or the
  `AddDestinationAndInvokeMethod` link path), check `is IDisposable`. If so, register the component via
  `GetOrAddResource` (dedup by key) and **skip** per-property `IDisposable` registration for that
  component's subtree. Otherwise fall back to the current per-property registration.
- `EtlDataFlow.Dispose()` needs no change — components land in the same `_resources` pool it already
  disposes.

## Edge cases to respect

- **Connection managers are shared and deduplicated across components.** They must stay flow-owned even
  when a component is `IDisposable` — a single component must not dispose a connection manager another
  component still uses. The "component owns its properties" rule applies to non-shared `IDisposable`
  resources, not to pooled connection managers.
- **Externally-owned resources** (`ILifetimeAwareActivator.IsExternallyOwned`) must remain excluded from
  flow ownership, exactly as today.
- **Backward compatibility.** Components that are `IDisposable` today but silently never disposed will now
  start being disposed — audit for anything that would break on an actual `Dispose()`, and for potential
  double-dispose where a resource was previously registered as a property.

## Non-goals

- Automatic disposal for hand-built (code, not XML) pipelines — there is no owning `IDataFlow` there, so
  the caller still owns lifetimes. A separate, consistent cancellation/teardown story for code-built
  pipelines is out of scope here (see the timeout/cancellation tech-debt note if present).

## Relation to MR !5 (RSSL-11867)

Interim fix in that MR: register the Kafka producer via `owner.GetOrAddResource(key, () => builder.Build())`
(the `HttpClient` / `MongoClient` precedent), which already works under the current property-centric model.
This tech-debt generalizes it so component-level `IDisposable` is honored and the `IDisposable` on the
transformation stops being dead weight.
