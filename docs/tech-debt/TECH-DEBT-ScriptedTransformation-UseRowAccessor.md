# Tech Debt: UseRowAccessor Mode for ScriptedRowTransformation

## Problem

`ScriptedRowTransformation` generates a per-shape static C# class for each unique `ExpandoObject`
schema and uses it as the Roslyn `globalsType`. This approach has two failure modes:

1. **Null-valued fields** — `ScriptBuilder` infers `null` values as `object` (because `null` has no
   runtime type). Arithmetic expressions such as `Score + 1` then fail at **compile time** with
   CS0019 ("operator '+' cannot be applied to 'object' and 'int'"). With `FailOnMissingField=false`
   the engine silently returns a null runner, so the output field becomes `null` instead of carrying
   out the intended computation.

2. **Absent fields** — if a field is missing from the `ExpandoObject`, the generated globals type
   has no corresponding property. The expression fails to compile (undeclared identifier), again
   producing `null` output silently.

A secondary bug compounds the issue: `GetScriptRunner` rejects scripts with **any** Roslyn
diagnostic, including mere warnings. For example, the expression `Score != null ? Score + 1 : 0`
generates CS0472 ("the result of the expression is always 'true'"), which causes the runner to be
rejected even though the script is semantically valid.

## Root Cause

`ScriptBuilder.BuildClassCode` maps null property values to `FullTypeName(null)` → `"object"`.
The generated constructor assignment is `Score = (object)extensions["Score"]`, so the Roslyn
type of `Score` inside the script is `object`. Standard numeric operators do not apply to `object`,
causing compile-time failure rather than a runtime null dereference.

Roslyn issue [#3194](https://github.com/dotnet/roslyn/issues/3194) prevents using `IDynamicMetaObjectProvider`
(i.e. `DynamicObject`) directly as `globalsType` — top-level member access generates compile errors.
This is why the per-shape static class workaround was originally adopted.

## Proposed Fix

Add an opt-in `UseRowAccessor` mode that uses a single shared `ScriptGlobals` class with a
`dynamic Row` property backed by a `DynamicObject` wrapper (`ScriptRow`). Scripts access fields via
`Row.Score` instead of bare `Score`. A plain class with a `dynamic` property sidesteps Roslyn
issue #3194 while still dispatching member access through `DynamicObject.TryGetMember` at runtime.

### New Files

**`EtlKit.Scripting/ScriptRow.cs`** — internal `DynamicObject` wrapper:

```csharp
internal sealed class ScriptRow : DynamicObject
{
    private readonly IDictionary<string, object?> _data;
    internal ScriptRow(IDictionary<string, object?> data) => _data = data;

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        _data.TryGetValue(binder.Name, out result);
        return true; // always succeed; result is null for absent or null fields
    }
}
```

**`EtlKit.Scripting/ScriptGlobals.cs`** — public globals classes:

```csharp
public sealed class ScriptGlobals
{
    public dynamic Row { get; }
    internal ScriptGlobals(ScriptRow row) => Row = row;
}

public sealed class ScriptGlobals<T>
{
    public T Row { get; }
    internal ScriptGlobals(T row) => Row = row;
}
```

### Changes to `ScriptedRowTransformation.cs`

1. **Add `UseRowAccessor` property** (opt-in, default `false`, backward-compatible):
   ```csharp
   public bool UseRowAccessor { get; set; }
   ```

2. **Fix `diagnostics.Any()` bug** — filter to `DiagnosticSeverity.Error` only:
   ```csharp
   // was: if (!diagnostics.Any())
   if (!diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
   ```

3. **Branch `TransformWithScriptDynamic`** — when `UseRowAccessor=true`:
    - Build `TypedScriptBuilder<ScriptGlobals>` from `ScriptBuilder.Default.ForType<ScriptGlobals>()`.
    - Use cache key `"Row::{expression}"` (single runner per expression, shared across all shapes).
    - Construct `new ScriptGlobals(new ScriptRow(arg))` and call `runner.Script.RunAsync(globals)` directly.
    - Catch `AggregateException` with inner `RuntimeBinderException` when `FailOnMissingField=false`.
    - **Exception contract**: only `RuntimeBinderException` is caught (narrow contract: "field not
      resolvable by the DLR binder"). All other exceptions (`NullReferenceException`,
      `DivideByZeroException`, etc.) propagate to the caller — null-propagation and arithmetic safety
      remain the explicit responsibility of the script author. Rationale: widening the catch to
      `Exception` would silently swallow genuine script errors.

4. **Branch `TransformWithScriptTyped`** — when `UseRowAccessor=true`:
    - Build `TypedScriptBuilder<ScriptGlobals<TInput>>` from `ScriptBuilder.Default.ForType<ScriptGlobals<TInput>>()`.
    - Use cache key `"Row<{typeof(TInput).FullName}>::{expression}"`.
    - Construct `new ScriptGlobals<TInput>(arg)` and call `runner.Script.RunAsync(globals)` directly.

5. **Refactor `TypedScriptBuilder` → `TypedScriptBuilder<TGlobals>`** — make the class generic over
   the complete globals type (not just `GlobalsTypeInfo.Type` internally). `ScriptBuilder.ForType<TGlobals>()`
   already captures the type at the call site; surfacing it as the class-level generic parameter keeps
   it visible through caching and execution without erasing it to `Type`:
   ```csharp
   // before
   TypedScriptBuilder builder = ScriptBuilder.Default.ForType<ScriptGlobals>();
   // after
   TypedScriptBuilder<ScriptGlobals> builder = ScriptBuilder.Default.ForType<ScriptGlobals>();
   ```
   This makes `TypedScriptBuilder<TGlobals>` usable outside `ScriptedRowTransformation` with any
   custom globals class — callers construct `TGlobals` directly and pass it to `RunAsync`, with no
   `Activator.CreateInstance` reflection involved.
   `ScriptRunner.cs` and `GlobalsTypeInfo.cs` require no changes; `ScriptBuilder.cs` return type
   changes from `TypedScriptBuilder` to `TypedScriptBuilder<TGlobals>`.

### Behavior Change Summary

| Scenario                                 | `UseRowAccessor=false` (default)         | `UseRowAccessor=true`                       |
|------------------------------------------|------------------------------------------|---------------------------------------------|
| Field present, non-null                  | Works                                    | Works (`Row.Field`)                         |
| Field present, null                      | Compile error → silent null              | `RuntimeBinderException` caught → null      |
| Field absent                             | Compile error → silent null              | `RuntimeBinderException` caught → null      |
| `FailOnMissingField=true` + absent field | Throws at compile time                   | Throws `RuntimeBinderException` at runtime  |
| Script with warnings (e.g. CS0472)       | Incorrectly rejected (BUG)               | Fixed (errors only)                         |
| `PassThrough=true`                       | Copies input fields to output            | Identical — independent of `UseRowAccessor` |

## Cache Key Design

- Old mode: `$"{globalsType.FullName}::{expression}"` — one runner per (shape × expression)
- New mode (dynamic): `$"Row::{expression}"` — one runner per expression, all shapes share it
- New mode (typed): `$"Row<{typeof(TInput).FullName}>::{expression}"` — one runner per (type × expression)

The single runner per expression in `UseRowAccessor` mode is a meaningful performance improvement
for workloads with many distinct ExpandoObject schemas.

## Tests to Add

File: `EtlKit.Scripting.Tests/ScriptedRowTransformationTests.cs`

- Update `ShouldHandleNullAndMissingFieldInMapping` — add `UseRowAccessor=true`, change expression
  to `Row.Score + 1`, assert null is returned gracefully (no exception).
- `UseRowAccessor_BasicArithmetic` — `Score=10`, `Row.Score + 1` → `11`.
- `UseRowAccessor_NullField_ReturnsNull` — `Score=null`, `FailOnMissingField=false` → null.
- `UseRowAccessor_MissingField_ReturnsNull` — no `Score` key, `FailOnMissingField=false` → null.
- `UseRowAccessor_MissingField_FailOnMissingField_Throws` — `FailOnMissingField=true`, missing field → throws.
- `UseRowAccessor_PassThrough` — `PassThrough=true`, verify input fields copied + mapped field computed.
- `UseRowAccessor_MultipleShapes_SameExpression` — two different shapes, same expression, verify single cache entry.
- `UseRowAccessor_Typed` — typed `TInput`/`TOutput`, `Row.Property + 1`.
- `ShouldNotRejectScriptsWithWarnings` — expression `"Score != null ? Score + 1 : 0"` triggers
  CS0472; the runner must compile and return `11`. Run with both `UseRowAccessor=false` and `true`
  (the `DiagnosticSeverity.Error` filter in `GetScriptRunner` is shared by both paths).
- Regression suite — all existing tests must pass without `UseRowAccessor`.

## Release Checklist

Before closing the implementation PR, update `CHANGELOG.md` with the following entries:

- **Added** — `ScriptedRowTransformation.UseRowAccessor` property (opt-in, default `false`).
- **Added** — `ScriptGlobals` and `ScriptGlobals<T>` public types (`EtlKit.Scripting` namespace).
- **Added** — `TypedScriptBuilder<TGlobals>` — generic form of `TypedScriptBuilder`, usable with
  custom globals types outside `ScriptedRowTransformation`.
- **Fixed** — scripts with Roslyn warnings (e.g. CS0472) were incorrectly rejected; now only
  `DiagnosticSeverity.Error` diagnostics block compilation.

## Why Deferred

The fix is low-risk but requires careful branching to preserve the existing per-shape path (which
offers compile-time field validation that some users rely on). The `diagnostics.Any()` bug fix is
safe and should be included in the same PR.
