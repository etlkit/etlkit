# Tech Debt: EtlKit.DynamicLinq AssemblyLoadContext Unloading

## Problem

`EtlKit.DynamicLinq` does not run Roslyn and does not emit user-defined assemblies at runtime, but
`System.Linq.Dynamic.Core.DynamicClassFactory.CreateType(properties)` emits a dynamic type per
unique `ExpandoObject` shape (see `ExpandoTypeMapper`). These generated types are loaded into the
default `AssemblyLoadContext`, which is non-unloadable - the same accumulation pattern documented
in [TECH-DEBT-ScriptBuilder-AssemblyLoadContext.md](TECH-DEBT-ScriptBuilder-AssemblyLoadContext.md) (sibling document landing in MR !115; the link will resolve once that MR is merged).

User-supplied assemblies (`AdditionalAssemblyNames`) are also loaded into the default ALC by
`AssemblyResolver.Load`. Those load once per assembly name and are not part of the per-row hot
path, so the accumulation pressure there is bounded by the user's configuration, not by data shape.

## Why The Risk Is Smaller Than In ScriptBuilder

- No Roslyn compile per shape - `DynamicClassFactory` just emits a `DynamicProperty[]`-backed type
  via `Reflection.Emit`. Smaller per-shape footprint than a Roslyn-compiled assembly.
- Inside `DynamicClassFactory` there is internal deduplication on the property signature, so two
  different `ExpandoObject` rows with the same `(field name, runtime type)` tuples reuse the same
  emitted type. The fast-path cache in `ExpandoTypeMapper` already keys on the same signature, so
  the actual number of emitted types tracks the number of distinct shapes the pipeline observes,
  not the number of rows.
- No `Assembly.Load(byte[])` from MR !116 - all `Assembly.Load(...)` calls in `AssemblyResolver`
  resolve user-named assemblies once and are bounded by `AdditionalAssemblyNames` cardinality.

## Why The Fix Is Still Worth Tracking

The risk surfaces when `ExpandoObject` rows produce many distinct flat-field shapes - schema drift,
ad hoc analytics over wide unions, `MapWithReflection` slow path with deeply nested heterogeneous
shapes. In those workloads the emitted types pin memory for the lifetime of the process.

## Why The Fix Was Deferred

Same root cause as `EtlKit.Scripting`: `EtlKit.DynamicLinq` targets `netstandard2.0` and the
collectible constructor `new AssemblyLoadContext(string name, bool isCollectible)` requires
`net5.0+`. A clean fix needs the package to multi-target.

## Proposed Fix

The fix mirrors the ScriptBuilder plan and should land in the same multi-target sweep so the two
packages stay aligned:

1. **Multi-target the package**

   ```xml
   <TargetFrameworks>netstandard2.0;net6.0</TargetFrameworks>
   ```

2. **Wrap `DynamicClassFactory.CreateType` in a collectible ALC for `net5.0+`**

   The fast-path cache in `ExpandoTypeMapper` (`_fastPathCache`) is the right boundary: each cache
   entry pairs a `Type` with the ALC that owns it, and eviction tears down both together. On
   `netstandard2.0` the behaviour is unchanged (default ALC, non-unloadable).

3. **Add eviction policy to `_fastPathCache`**

   Currently unbounded - any pipeline with high-cardinality schemas keeps growing the cache. LRU
   or TTL-based eviction matches the eviction work needed in `ScriptBuilder._cache`.

4. **Coordinate with `ScriptBuilder` work**

   Both packages emit types and both feed dataflow steps. A shared helper that wraps a
   collectible ALC + emits a type + tracks lifecycle would deduplicate the multi-target boilerplate
   and the eviction logic.

## Out Of Scope For MR !116

MR !116 introduces `ExpressionRowFiltration` against the existing `netstandard2.0` target. Adding
a multi-target build and collectible ALC support in the same MR would conflate the feature with a
cross-cutting infrastructure change. The risk profile is well-understood, bounded by user data
shape, and matches the documented `ScriptBuilder` plan - so this is tracked as a follow-up to be
done together with the `ScriptBuilder` ALC work.

## References

- Note 84542 in MR !116 (`ETLBox.DynamicLinq/AssemblyResolver.cs:42`)
- [TECH-DEBT-ScriptBuilder-AssemblyLoadContext.md](TECH-DEBT-ScriptBuilder-AssemblyLoadContext.md)
- `System.Linq.Dynamic.Core.DynamicClassFactory.CreateType` - upstream type emission entry point
- `ETLBox.DynamicLinq/ExpandoTypeMapper.cs` - per-shape cache and emission boundary
