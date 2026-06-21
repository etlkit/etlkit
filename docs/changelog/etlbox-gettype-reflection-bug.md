# Bug: DataFlowXmlReader.GetType missing ReflectionTypeLoadException handling

> **Status: COMPLETED** (2026-04-12) — fixed in commit `5c3314e3 fix(RSSL-11572): DataFlowXmlReader reflection robustness`

## Problem

In `EtlKit.Serialization.DataFlow.DataFlowXmlReader` the static `GetType` method
called `Assembly.GetTypes()` against every loaded assembly without handling
`ReflectionTypeLoadException`. The neighbouring `GetDataFlowTypes` method in the same
class already handled the same failure mode through `SafeGetTypes`.

When the test host loaded assemblies that referenced `Microsoft.Testing.Platform` (a
transitive dependency of `Microsoft.NET.Test.Sdk`), XML deserialization of DataFlow
packages failed with `ReflectionTypeLoadException`.

## Workaround (now removed)

`EtlDataFlowStep.RecreateDataFlow` carried a temporary `AppDomain.CurrentDomain.AssemblyResolve`
handler that, on a failed load, searched for an already-loaded assembly with a
compatible name (version-agnostic).

## Fix

In `DataFlowXmlReader.GetType` the call to `assembly.GetTypes()` was replaced with
`SafeGetTypes(assembly)`:

```csharp
foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
{
    var types = SafeGetTypes(assembly).Where(t => t.Name == typeName);
    // ...
}
```

`SafeGetTypes` swallows `ReflectionTypeLoadException` and returns the types that did
load:

```csharp
private static Type[] SafeGetTypes(Assembly assembly)
{
    try { return assembly.GetTypes(); }
    catch (ReflectionTypeLoadException ex)
    {
        return ex.Types.Where(t => t is not null).ToArray()!;
    }
}
```

## Affected package

`EtlKit.Serialization`
