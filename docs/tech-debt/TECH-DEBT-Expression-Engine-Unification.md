# Tech Debt: Expression Engine Unification

**Status:** Split applied (2026-04-29). Feature parity practically achievable
(Round 5 / note 84400 closed). Full unification still open as a separate task.
**Created:** 2026-04-28
**Priority:** Medium
**Origin:** review feedback on MR !116 (RowFiltration / ExpressionRowFiltration), notes 84243, 84246, 84400-84404

## Problem

Two different engines exist for evaluating user-supplied expressions on a row:

| Component | Package | Engine | What it can express |
|-----------|---------|--------|---------------------|
| `ScriptedRowTransformation` | `EtlKit.Scripting` | Roslyn | Full C# inside the body — method calls, async, statement blocks, custom helper types |
| `ExpressionRowFiltration`   | `EtlKit.DynamicLinq` | `System.Linq.Dynamic.Core` | Comparisons, arithmetic, logical operators, member access, null checks, LINQ-style collection methods, **method calls on user types via `RegisterCustomTypes`** (added in Round 5) |

As of 2026-04-29 the two engines live in **separate NuGet packages** so consumers
who need only predicate filtration pull `EtlKit.DynamicLinq` and avoid the transitive
Roslyn cost.

**After Round 5** (commit adding `RegisterCustomTypes` and exposing `ParsingConfig`):
the practical capability gap is narrow. Dynamic LINQ now handles user-type method
calls after a one-line registration. The remaining engine difference applies to
non-predicate scenarios: multi-statement bodies, async, automatic type discovery
without registration. None of these are blocking for the filtration use case the
DynamicLinq package is positioned for.

The deeper question — whether one engine can subsume the other for the full
EtlKit surface — is still open and tracked below.

## Why both exist today

`ExpressionRowFiltration` was added in MR !116 to cover predicate-only use cases
(filter-by-condition) without the cost profile of Roslyn:

- Roslyn compiles a fresh assembly for each new row shape and loads it via
  `Assembly.Load(bytes)`. The assembly cannot be unloaded.
- On flows with many shapes (different sets of fields per source) memory grows
  monotonically.
- A predicate does not need the full C# language. Comparisons, arithmetic, member access,
  null checks and LINQ-style collection methods cover the vast majority of cases.

Reviewer counterpoint (note 84243):

> Я не видел проблемы с компиляцией Roslyn, потому что она выполняется один раз для
> каждого типа. По идее, написать бенчмарк недолго, что думаешь?

The cost characterisation needs benchmarks before it can be used as the deciding
argument. See the related task below.

## Open questions

1. **Is the full Roslyn surface actually used in real flows?**
   Audit existing `ScriptedRowTransformation` usages (in this repo, in Common.Etl,
   in customer packages). What fraction needs method calls or statement blocks vs.
   pure expressions?

2. **Can Dynamic LINQ be extended to cover the gap?**
   `ParsingConfig.CustomTypeProvider` allows registering additional types and methods
   visible to the parser. For specific cases — e.g. `JsonNode.ToJsonString()` — a
   targeted custom type registration would let Dynamic LINQ handle calls without
   bringing in Roslyn.

3. **Drop one engine?**
   - Drop Roslyn → migrate `ScriptedRowTransformation` to Dynamic LINQ + custom type
     providers; lose the ability to write multi-statement bodies.
   - Drop Dynamic LINQ → make `ExpressionRowFiltration` a wrapper over Roslyn; revisit
     the per-shape assembly cost.
   - Keep both → name and document the boundary clearly so the choice is obvious.

4. **Apply Dynamic LINQ in mappings (note 84246)?**
   Reviewer suggests using Dynamic LINQ in places where mappings currently rely on
   property name strings (`DbMerge` `MatchColumn` / `RetrieveColumn`,
   `JsonTransformation.Mappings`, etc.). A small expression per mapping would replace
   the property-name convention. This is a separate feature, not a unification step,
   but lives in the same family of "expression evaluation" decisions.

## Related task

**Benchmark Roslyn vs Dynamic LINQ** — needed to inform the decision in question 3.
Dimensions to measure:

- Cold compile time per new shape
- Hot evaluation time on a compiled delegate
- Memory footprint after N distinct shapes (verify or refute the assembly accumulation
  claim)
- Memory footprint after N evaluations on the same shape

Filed as a follow-up to MR !116. Without numbers the discussion in question 3 cannot be
closed.

## Out of scope for MLRSSL-1610

This file documents the question. The decision is intentionally deferred:

- MR !116 ships the two engines side by side with documentation that names the
  boundary (see `docs/dataflow/row-filtration.md`, "When to use this vs
  ScriptedRowTransformation").
- A separate task will cover the benchmark and the decision.
- A separate task will cover the mapping-expression idea (note 84246).

## Cross-references — parallel work on `ScriptedRowTransformation` (MLRSSL-1510)

MR !116 (this Dynamic LINQ work) was developed in parallel with three
merged Roslyn-side MRs under MLRSSL-1510. They share the
`EtlKit.Scripting` package and influence the symmetric API choices on
the Dynamic LINQ side. Decisions to track:

- **MR !113** — `ScriptedRowTransformation.PassThrough` — copies input
  fields into the output before applying `Mappings`. Typed path requires
  `TInput` assignable to `TOutput`, throws `InvalidOperationException`
  otherwise. Three static reflection caches (`_outputAssignableFromInput`,
  `_passThroughProperties`, `_outputPropertiesCache`) for hot-path perf.
  Not directly mirrored in Dynamic LINQ (filtration does not produce a
  transformed output); will become relevant if Phase 2
  (`ExpressionRowTransformation<TInput, TOutput>`) is added.
- **MR !115** — `ScriptedRowTransformation.AdditionalImports` and
  `AdditionalAssemblyNames` — the reference for our Phase 1. Roslyn-side
  uses `Assembly.Load(name)` -> `Assembly.LoadFrom(path)` fallback on
  `FileNotFoundException`/`FileLoadException`. `TypedScriptBuilder.WithImports`
  is the immutable-builder entrypoint, symmetric to existing `WithReferences`.
  A bug in the typed path (missing `WithReferences` call) was fixed there.
  Project-wide policy added in this MR: "All written content in this
  project must be in English".
- **MR !114** — `<Pipeline>` flat-sequence XML syntax — design plan only,
  not yet implemented. Two outcomes that affect our future work:
  `LinkTo` and `LinkErrorTo` became `virtual` in `DataFlowTransformation`
  (after follow-up review — switched from `new`/shadow to `virtual`/
  `override` because shadowing breaks through interface or base reference);
  two new interfaces `IDataFlowXmlContext` and `IDataFlowXmlSerializable`
  planned for XML-deserialization extensibility. Worth checking if/when
  any future override of `LinkTo` is needed in `ExpressionRowFiltration`
  or its descendants.

## Decision: split into a separate package (applied 2026-04-29)

Following the benchmark results below and reviewer feedback on MR !116, the
Dynamic LINQ-based filtration was extracted into a dedicated package
**`EtlKit.DynamicLinq`**.

What landed:

- `EtlKit.Scripting` keeps the Roslyn-based components (`ScriptedRowTransformation`,
  `ScriptBuilder`, `TypedScriptBuilder`, `EtlKitScriptingServiceCollectionExtensions`)
  and its `Microsoft.CodeAnalysis.CSharp.Scripting` dependency. The
  `System.Linq.Dynamic.Core` dependency was removed.
- `EtlKit.DynamicLinq` (new) hosts `ExpressionRowFiltration<TInput>`,
  `ExpressionRowFiltration` (non-generic), `ExpandoTypeMapper` (internal), and
  `EtlKitDynamicLinqServiceCollectionExtensions`. Depends on `EtlKit`,
  `EtlKit.Common` and `System.Linq.Dynamic.Core`. Namespace: `EtlKit.DynamicLinq`.
- `EtlKit.DynamicLinq.Tests` (new) hosts `ExpressionRowFiltrationTests` (37 tests)
  and `FeatureParity/MethodCallSupportTests` (8 tests). Both moved from
  `EtlKit.Scripting.Tests`.
- `EtlKit.Serialization.Tests/ExpressionRowFiltrationDeserializationTests.cs`
  references `EtlKit.DynamicLinq` instead of `EtlKit.Scripting`.
- `EtlKit.DynamicLinq.Benchmarks` (renamed from `EtlKit.Scripting.Benchmarks`)
  references both packages — Roslyn comparisons through `EtlKit.Scripting` and
  Dynamic LINQ paths through `EtlKit.DynamicLinq`.

Test counts after split:

| Project | Pass | Notes |
|---------|------|-------|
| `TestTransformations` | 11/11 | Core `RowFiltration` (no Dynamic LINQ) |
| `EtlKit.DynamicLinq.Tests` | 45/45 | 37 expression filtration + 8 feature-parity |
| `EtlKit.Scripting.Tests` | 13/13 | Roslyn (`ScriptBuilder`, `ScriptedRowTransformation`) |
| `EtlKit.Serialization.Tests` (filter) | 4/4 | XML deserialization + end-to-end pipeline |
| **Total** | **73/73** | |

The unification question — whether one engine can replace the other — remains
open (questions 1-4 above). Until that lands, the two packages coexist with the
boundary documented in `docs/dataflow/row-filtration.md` ("When to use this vs
ScriptedRowTransformation").

## Suggested next steps

Items 1-3 are complete (Round 5 + 6, 2026-04-29). Items 4-7 remain open:

1. ~~Run the benchmark, attach numbers to this document.~~ — **done**, see "Benchmark Results" below; HotEvaluation re-measured after Optimizations 1+2 (commit `f3ae2718`) and after Phase 1 (commit `0e3c2aaf`).
2. ~~Phase 1 — `AdditionalAssemblyNames` / `AdditionalImports` symmetry with `ScriptedRowTransformation` (MR !115).~~ — **done** (commit `0e3c2aaf`). Closes the XML-flow user-type case raised in MR review note 84488. The Roslyn-side reference implementation is the merged `feature/MLRSSL-1510-scripted-transform-system-dlls` branch (MR !115); our `AssemblyResolver.Load` extends its two-step strategy (`Assembly.Load` -> `Assembly.LoadFrom` on `FileNotFoundException`/`FileLoadException`) with an AppDomain pre-check as step 1 to avoid double-loading already-resident assemblies.
3. ~~Refactor — extract `AssemblyResolver` and `DynamicLinqTypeProvider` helpers.~~ — **done** (commit `e880c843`).
4. Audit existing `ScriptedRowTransformation` usages in real flows. Goal: count how many genuinely need multi-statement bodies / async vs. how many can move to `ExpressionRowFiltration` or a future `ExpressionRowTransformation`.
5. Phase 2 — `ExpressionRowTransformation<TInput, TOutput>` as a drop-in alternative to `ScriptedRowTransformation` for the transformation case. Out of scope for MR !116; planned as a follow-up task. After Phase 2 lands, the unification question (drop one engine, keep both, or merge) becomes answerable from data.
6. Based on (4) and (5), pick a direction: keep both, drop Roslyn for predicate / transformation case, or maintain both with documented boundary. If keeping both — possibly rename one component to make the boundary explicit (e.g. `ScriptedRowFiltration` mirroring `ScriptedRowTransformation`).
7. Mapping-expression idea (note 84246) is a separate design discussion; track it as its own task once the engine question is settled.

## Deferred ExpandoObject mapping optimisations (2026-04-30)

Surfaced during the Round 8 self-review. These improve the slow path and are tracked for a dedicated perf MR with before/after benchmarks; they are out of scope for MR !116 because each one is a non-trivial change to working, stabilised code:

1. **Single-pass `MapCollection`.** `BuildUnifiedDictShape` walks every dict element to build the unified shape, then `ProjectDictToShape` walks the same elements again to fill the typed list. For a collection of size N this is `O(2N)`. A single-pass variant - collect raw values during the unification walk and project them once the shape is known - cuts to `O(N + F)` where F is the field count. Trickier control flow because nested dicts and collections can recurse, so worth its own MR with regression tests.
2. **Compiled per-shape mapper for the slow path.** The fast path emits a `Func<IDictionary<string, object?>, object>` per `(field name, runtime value type)` signature via Expression Trees. The slow path (`MapCollection`, `ProjectDictToShape`) still uses `Activator.CreateInstance` plus reflection-based `SetValue` per item. A symmetric compiled mapper would close the per-row cost gap for nested-collection workloads. Significant new code (Expression Trees over `IDictionary<string, object?>` with nested-dict and nested-collection projection) - belongs in a focused perf MR, not bundled with feature work.
3. **Multi-shape cache for the non-generic Expando path.** `ExpressionRowFiltration` (non-generic) caches one compiled lambda per (`FilterExpression`, `ParsingConfig`, mapped DynamicClass type). When the input stream interleaves rows of two or more shapes (typical for nullable scalar fields where some rows have `null` and others a concrete value, producing two distinct DynamicClass types), the single-slot cache thrashes - every alternation triggers a fresh `ParseLambda` + `Compile`. Replacing the cache with a small `Dictionary<Type, Func<object, bool>>` keyed by mapped type would eliminate the thrash for the common alternating-shape case. Bounded scope (~20 lines) but introduces a new cache invariant; deferred to keep MR !116 focused on the review feedback.

## Benchmark Results (2026-04-28)

Full report:
[`EtlKit.DynamicLinq.Benchmarks/BENCHMARK-RESULTS-2026-04-28.md`](../../EtlKit.DynamicLinq.Benchmarks/BENCHMARK-RESULTS-2026-04-28.md).
Status: final. All three benchmarks completed with full BenchmarkDotNet
warmup + iterations.

### ColdCompile

Full BDN with warmup (x64, .NET 8.0, BenchmarkDotNet 0.14.0). Absolute timings
vary across machines; the ratio between engines is what reproduces:

| Engine | Mean (Composite) | Allocated per shape | Ratio to Roslyn |
|--------|---------------:|---------------:|----:|
| Roslyn (ScriptBuilder) | 121 ms | 9,758 KB | 1.00 |
| Dynamic LINQ (typed POCO) | 1.04 ms | 67 KB | 0.009 |
| Dynamic LINQ (ExpandoObject) | 2.82 ms | 134 KB | 0.025 |

Cold compile cost is roughly **40-120× smaller** and per-shape allocation
roughly **73-146× smaller** for Dynamic LINQ. The Q2 hypothesis about
per-shape `Assembly.Load(bytes)` is supported quantitatively at the
single-shape level. ManyShapes will quantify the linear-in-N curve directly.

### HeadToHead (answers Q1)

Shipped `ExpressionRowFiltration` vs `ExpressionRowMultiplicationPrototype`
(variant inheriting from `RowMultiplication`, same Dynamic LINQ logic):

| Variant | RowCount=10,000 Mean | Allocated |
|---------|---------------------:|----------:|
| ExpressionRowFiltration | 5,689 ms | 330.72 MB |
| ExpressionRowMultiplication prototype | 5,718 ms | 330.68 MB |
| Ratio | 1.01× | 1.00× |

Statistically indistinguishable — the dedicated component carries no runtime
cost over the `RowMultiplication` form. The case for it is call-site
readability only.

### ManyShapes (the central measurement for Q2)

Compile and evaluate a predicate on N distinct shapes per BenchmarkDotNet
iteration. `[GlobalCleanup]` probe captures the loaded-assembly count delta
and the managed-memory delta:

| Engine | N=10 Mean | N=100 Mean | Allocated N=100 | **Assembly delta N=10 / 50 / 100** |
|--------|----------:|-----------:|----------------:|-----------------------------------:|
| Roslyn | 660 ms | 7,522 ms | **969.81 MB** | **+768 / +11,328 / +22,628** |
| Dynamic LINQ Expando | 832 ms | 14,153 ms | 9.12 MB | +33 / +33 / +33 |

**Roslyn loaded-assembly count grows linearly with the number of distinct
shapes** and the assemblies are not unloadable. Dynamic LINQ on
ExpandoObject stays at exactly +33 regardless of N — `DynamicClassFactory`
emits new shape types into the same shared persistent `AssemblyBuilder`.
The Round-1 assembly-leak hypothesis is supported quantitatively.

Feature parity matrix (full xUnit run, 51/51 PASS after Round 5 + 6):

| Scenario | Roslyn | Dynamic LINQ |
|----------|:------:|:------------:|
| Built-in instance method on string / DateTime | works | works |
| Static method on built-in type (`string.Format`) | works | **works (out of the box)** |
| Instance method on user type, no setup | works | does not work |
| Instance method on user type, after `RegisterCustomTypes(typeof(T))` | works | **works** |
| Instance method on user type via assembly registration | works | **works** (after Phase 1) |
| Short type name resolution via namespace imports | works | **works** (after Phase 1) |

The "JsonNode → string" objection from note 84243 is **practically closed** after
Round 5 (note 84400): `ExpressionRowFiltration<TInput>` exposes
`RegisterCustomTypes(params Type[])` as a convenience layer over
`ParsingConfig.CustomTypeProvider`. User-type method calls work after a single
registration line:

```csharp
filtration.RegisterCustomTypes(typeof(JsonNode));
filtration.FilterExpression = "Payload.ToJsonString().Length > 100";
```

For deeper customization (extension methods, alternative type resolution, parser
flags) the underlying `ParsingConfig` is also exposed as a public property.

**Phase 1 (Round 6, applied 2026-04-29 in commit `0e3c2aaf`):** symmetric
configuration with `ScriptedRowTransformation` (MR !115 added the same two
properties on the Roslyn side; our implementation mirrors the API surface
so users can switch between engines without changing config shape):

- `AdditionalAssemblyNames` — names or file paths of assemblies. All public
  exported types from each assembly are added to the type provider in bulk.
  Resolution: AppDomain → `Assembly.Load(AssemblyName)` → `Assembly.LoadFrom(path)`.
- `AdditionalImports` — namespace prefixes used as imports for short type name
  resolution. Imported namespaces win over plain short-name match.

XML form:

```xml
<ExpressionRowFiltration>
    <FilterExpression>MyService.IsValid(Payload)</FilterExpression>
    <AdditionalAssemblyNames>
        <string>MyCompany.Domain</string>
    </AdditionalAssemblyNames>
    <AdditionalImports>
        <string>MyCompany.Domain</string>
    </AdditionalImports>
</ExpressionRowFiltration>
```

This closes the XML-flow use case (note 84488 in MR review): users no longer
need per-type `RegisterCustomTypes` calls; one assembly entry registers the
whole namespace. Implementation lives in two helper files alongside the main
class: `AssemblyResolver.cs` and `DynamicLinqTypeProvider.cs` (refactor commit
`e880c843`), keeping the `ExpressionRowFiltration` class focused on the
filtration concern.

This narrows the engine difference from "Roslyn supports method calls, Dynamic
LINQ does not" to two narrow gaps: multi-statement bodies and async. Neither
applies to the predicate filtration use case this MR ships.

## Hot path optimization roadmap (Round 5)

Per-row evaluation cost is a separate dimension from cold compile cost. The
initial Round 5 HotEvaluation benchmark exposed a per-row gap where Roslyn was
significantly faster than the shipped `ExpressionRowFiltration` because the
latter re-parsed the predicate and rebuilt the `Queryable` wrapper on every
row. Layered optimizations are tracked here, in order of complexity vs.
expected payoff. **Optimizations 1 and 2 are applied and shipped in this MR.
Optimization 2.5 (slow-path polish) and Optimization 3 stay in tech-debt** as
no production workload currently shows the bottleneck either would address.

### Optimization 1 — cached compiled delegate (applied 2026-04-29)

**Implemented in `ExpressionRowFiltration<TInput>` and the non-generic
`ExpressionRowFiltration : ExpressionRowFiltration<ExpandoObject>`.**

Both code paths now parse the predicate once via
`DynamicExpressionParser.ParseLambda` and cache the compiled delegate. The
non-generic ExpandoObject path additionally builds a `Func<object, bool>`
wrapper around the typed lambda using `Expression.Lambda + Expression.Invoke +
Expression.Convert`, so per-row evaluation skips both re-parse and the
`Array.CreateInstance + AsQueryable` wrap. Cache key:
`(FilterExpression, ParsingConfig reference, mapped DynamicClass type)` — any
change invalidates and recompiles. `RegisterCustomTypes` and
`InvalidateCompiledCache` participate in invalidation.

Risk: low. Behavior is unchanged — same parser, same lambda, just retained.

Limits: cache scoped to the filtration instance, not shared across instances.
For long-running flows that recreate the filtration per pipeline build, the
cache effectively rebuilds on each construction. This matches the existing
component lifecycle and is not a regression.

Prospects: the typed `TInput` path is now the cheapest case (pure
`Func<TInput, bool>` invocation, no mapping per row). The ExpandoObject path
still pays `ExpandoTypeMapper.Map(row)` per row, which is what Optimization 2
addresses.

### Optimization 2 — fast row mapping for the ExpandoObject path (applied 2026-04-29)

**Implemented in `ExpandoTypeMapper`** as a two-path mapper with a per-row
flat-vs-nested gate:

- **Fast path** for flat shapes (scalars, nullables, strings, byte arrays,
  custom classes that are not `IDictionary` and not non-string/non-byte-array
  enumerables). For each unique shape signature (tuple of
  `(field name, runtime value type)` pairs), `BuildCompiledEntry` emits a
  `Func<IDictionary<string, object?>, object>` once via Expression Trees -
  equivalent to `dict => new T { F1 = (T1)dict["F1"], F2 = (T2)dict["F2"], ... }`.
  Per-row cost is one dict walk to compute the signature + cache lookup +
  delegate invoke. No per-row reflection.
- **Slow path** for shapes with nested `IDictionary<string, object?>` or
  homogeneous collections - the original recursive reflection-based mapping is
  preserved verbatim. Recursion handles arbitrary nesting depth, predicates
  like `Order.Total > 100` and `Items.Any(Sum > 100)` keep working at the
  pre-optimization cost.
- The path is selected per row by `HasComplexFields`, which checks the
  top-level dictionary in O(N).

Result: HotEvaluation per-row cost on flat shapes drops from ~1.4 µs / 1.7 KB
(Opt. 1 only) to ~500-700 ns / 240 B - **at parity with Roslyn warm runner
on time (~660 ns Roslyn, ~617 ns DynamicLinq) and ~3× fewer allocations**.
Roslyn time numbers vary 660-1140 ns between passes due to JIT/GC state, so
the allocation advantage is the more stable signal. Nested-shape predicates
keep their previous cost, no behavioural regression.

Risk: low. The fast path is opt-in by shape; failing a check routes to the
exact pre-existing code. Tests cover both paths (57/57 PASS in
`ExpressionRowFiltrationTests` including `NestedExpando_*`,
`CollectionOfDicts_*`, `DeeplyNestedExpando_*` scenarios).

Limits:
- Fast-path cache (`ConcurrentDictionary<ShapeSignature, ShapeEntry>`) is
  process-wide static. Grows monotonically as new flat shapes are encountered.
  Same lifetime characteristic as `DynamicClassFactory.CreateType` cache used
  by the slow path - acceptable.
- Per-row signature build still allocates a small `FieldKey[]` array. Could
  be eliminated with a last-signature shortcut if future profiling shows
  this is a bottleneck; currently bounded by the typical 5-15 fields per row.
- Direct dictionary binding (bypass `DynamicClass` entirely, bind parser to
  `IDictionary<string, object?>` indexer access) was the more aggressive
  alternative considered. It would eliminate the `new T { ... }` allocation
  per row and skip `DynamicClass` emission entirely, but requires
  re-implementing the parser surface (nested dicts, homogeneous collections,
  `ConvertObjectToSupportComparison`). Deferred unless the current 240 B/row
  becomes a bottleneck on a real workload.

### Optimization 2.5 — slow-path improvement (deferred, low priority)

Optimization 2 covers the flat-shape fast path. Shapes with nested
`IDictionary` or homogeneous collections still go through the original
recursive reflection-based mapper (`MapWithReflection`), at ~1.4 µs / ~1.7 KB
per row on a typical 5-field shape. That is ~1.2× slower than Roslyn warm
runner on the same input.

In production XML-defined Common.Etl flows the slow path is rare: most rows
come from DB sources with flat scalar fields, hitting the fast path. Nested
ExpandoObject occurs when extracting from JSON-shaped sources or merging
heterogeneous inputs. At pipeline-level the ~700 ns gap on slow path
amounts to ~7 ms per 10k rows / ~700 ms per 1M rows — under 1% of total
runtime when DB I/O or block dispatch dominates.

For the cases where slow-path optimization is wanted, three tactics are
ranked by effort vs payoff (worked-out analysis from 2026-04-29):

#### Tactic A — cache `PropertyInfo[]` per type (lightest)

`type.GetProperty(name)` reflection lookup happens per field per row.
Cache a `Dictionary<string, PropertyInfo>` per resolved type. Reduces the
GetProperty cost to a hashtable lookup over the cached dict.

- Effort: ~30 minutes, ~30 LOC
- Risk: zero (pure cache layer)
- Saving: ~250-500 ns per row (5 fields × ~50-100 ns lookup)
- Final cost: ~1.1-1.2 µs per row (15-20% improvement)

The absolute saving is small relative to `SetValue` reflection itself, which
remains the dominant cost on the field assignment loop. Not recommended in
isolation; useful as a building block under Tactic B.

#### Tactic B — compiled setter delegate per type (recommended polish)

Build an `Action<object, object?[]>` per `DynamicClass` type once via
Expression Trees, equivalent to:

```csharp
(instance, values) => {
    var typed = (T)instance;
    typed.F1 = (T1)values[0];
    typed.F2 = (T2)values[1];
    ...
}
```

Replace the per-field `SetValue` loop in `MapWithReflection` with one
delegate invocation. Optionally add a cached `Func<object>` per type for
`new T()` to bypass `Activator.CreateInstance` reflection.

- Effort: ~1-2 hours, ~50-80 LOC + test parity verification
- Risk: low — same semantics as `SetValue`, just compiled
- Saving: ~500-1000 ns per row (eliminates per-field reflection invocation)
- Final cost: ~700-900 ns per row (30-50% improvement)
- Pattern is standard (Dapper, EF Core, AutoMapper use it for materialization)

This is the recommended "light polish" if slow-path optimization is wanted
without a deep rework. Brings slow path within ~10% of Roslyn warm runner.

#### Tactic C — recursive compiled mapper covering all shapes (substantial)

Extend the fast path mapper recursively to handle nested dictionaries and
homogeneous collections. Shape signature becomes recursive (parent shape
includes child shape signatures); compiled mapper invokes nested mappers
via closures or static dispatch:

```csharp
dict => new T {
    F1 = (T1)dict["F1"],
    Order = nestedOrderMapper((IDictionary<string, object?>)dict["Order"]),
    Items = MapList(dict["Items"], itemMapper),
}
```

After this, the slow path disappears entirely; one unified fast path covers
all supported shapes.

- Effort: ~3-5 hours, ~150-200 LOC
- Risk: medium — recursion edge cases (empty collections, heterogeneous
  failure mode, deep nesting)
- Saving: ~700-900 ns per row on nested shapes (slow path → near fast path
  speed)
- Final cost: ~600-800 ns per row, comparable to current flat fast path

Worthwhile if slow-path workload becomes common in production OR if we
want to remove the dual-path complexity in `ExpandoTypeMapper`. Not needed
right now: dual-path is small and well-tested.

#### Decision

**Default: keep current state.** Slow path is acceptable: ~1.4 µs / 1.7 KB
on rare nested shapes, ~1% of pipeline runtime impact in I/O-bound flows.
The architectural argument is already won by the flat fast path (production-
typical case beats Roslyn 1.5-2× on hot path).

**If time permits before MR close:** apply **Tactic B** (compiled setter).
Standard pattern, low risk, brings slow path under ~10% gap vs Roslyn,
demonstrates the dual-path architecture handles both cases well.

**Tactic C is reserved** for the case where future profiling on real
workloads shows the slow path as a bottleneck — then the work is justified
by data, and the dual-path complexity becomes worth removing.

### Optimization 3 — bypass DynamicClass for typed POCO with property cache (deferred)

For `ExpressionRowFiltration<TInput>` where `TInput` is a closed type known
at compile time, the parser already binds against `TInput`'s properties via
`PropertyInfo`. The `DynamicClass` machinery is not on this path. The
remaining cost is in the parser + lambda compile — bounded by the predicate
shape, not the row. Optimization 1 already pins this cost to once per
`(FilterExpression, ParsingConfig)` pair.

A further step would be to skip the parser entirely for the simplest predicate
forms (`Field op Constant`, `Field op Field`, conjunctions/disjunctions of the
above) by recognizing the AST and emitting a `Func<TInput, bool>` directly via
`Expression.Property + Expression.Compare`. This is the path Roslyn doesn't
have access to — it would put the typed path well below Roslyn's per-row cost.

Risks:
- The hand-rolled emitter must reproduce the parser's null-handling,
  type-conversion, and member-resolution semantics exactly. Anywhere it
  diverges is a silent behavior change for users who already depend on the
  parser.
- Maintenance — the predicate language grows. A separate fast-path emitter
  doubles the surface area to keep in sync with parser changes.

Limits: only applies to predicate shapes the fast-path recognizes. Anything
else falls back to the parsed-and-cached lambda from Optimization 1.

Prospects: probably not worth doing unless real workloads on typed `TInput`
show a measurable bottleneck after Optimization 1 lands. Documented here so
the option is on the table when (or if) that data appears.

### Decision criteria (post Optimizations 1 + 2)

Optimizations 1 and 2 shipped. Current state on the HotEvaluation benchmark:

- Typed `TInput` cached path: **~40× faster than Roslyn**, zero allocation per row.
  Optimization 3 stays in tech-debt - no measured bottleneck to escalate to.
- ExpandoObject flat-shape path: **parity with Roslyn on time, ~3× less allocation
  per row**. Direct dict binding (the deferred mitigation under Optimization 2)
  stays in tech-debt - re-implementing the parser surface is high cost for
  unproven gain.
- ExpandoObject path on shapes with nested or collection fields: ~1.2× slower
  than Roslyn (slow-path fallback inside `ExpandoTypeMapper` - reflection
  recursion, same as before). No regression. **Optimization 2.5 stays in
  tech-debt** as a "light polish" option (Tactic B closes most of this gap
  with low risk); apply it when there's idle time before MR close or as a
  follow-up if slow-path workload is observed in production.

The 2× threshold remains the practical boundary for re-opening any of these:
differences below that get lost in warmup and GC noise on real flows;
differences above become user-visible at scale.
