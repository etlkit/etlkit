# Row filtration

Two transformations drop rows that should not continue down the flow:

- `RowFiltration<TInput>` (in `EtlKit`) — filters by a `Func<TInput, bool>` delegate.
- `ExpressionRowFiltration<TInput>` (in `EtlKit.DynamicLinq`) — filters by a string expression evaluated through `System.Linq.Dynamic.Core`. Suited for XML-defined flows where the predicate is configured in the package, not in C# code.

Both are non-blocking. Internally they wrap a `TransformManyBlock<TInput, TInput>` that returns a single-element collection on a passing row and an empty collection otherwise.

---

## RowFiltration

Use `RowFiltration<TInput>` when the predicate is known at compile time.

```csharp
DbSource<MySimpleRow> source = new DbSource<MySimpleRow>("SourceTable");
RowFiltration<MySimpleRow> filtration = new RowFiltration<MySimpleRow>(row => row.Col1 > 0);
DbDestination<MySimpleRow> dest = new DbDestination<MySimpleRow>("DestTable");

source.LinkTo(filtration);
filtration.LinkTo(dest);
```

For dynamic rows (`ExpandoObject`) use the non-generic `RowFiltration`:

```csharp
RowFiltration filtration = new RowFiltration(row => ((dynamic)row).Col1 > 0);
```

### Why a dedicated component?

`RowMultiplication` can technically filter by returning an empty array — `row => predicate ? new[] { row } : Array.Empty<T>()`. `RowFiltration` is preferred for two reasons:

- The intent reads from the name and signature. `RowMultiplication` reads as "one to N", and using it as "one to 0 or 1" is the inverse case.
- The `Func<TInput, bool>` signature avoids the boilerplate of materializing arrays at every call site, and the base class handles the try/catch around the predicate.

This is the same separation that exists between `RowDuplication` and `RowMultiplication`: the building block is the same `TransformManyBlock`, but a focused component with a focused signature makes the pipeline easier to read.

### Error handling

If the predicate throws and an error destination is linked via `LinkErrorTo`, the failing row is forwarded there with the exception attached. Without an error destination the exception propagates and stops the flow.

```csharp
MemoryDestination<EtlKitError> errorDest = new MemoryDestination<EtlKitError>();
filtration.LinkErrorTo(errorDest);
```

`null` rows are dropped silently without invoking the predicate.

---

## ExpressionRowFiltration

`ExpressionRowFiltration` lives in `EtlKit.DynamicLinq` (separate package, no Roslyn dependency). The predicate is a string parsed by `System.Linq.Dynamic.Core` into an expression tree and compiled via `Expression.Compile()`. No Roslyn, no per-shape assembly emission, no `Assembly.Load`.

Two forms:

- `ExpressionRowFiltration<TInput>` — generic, for typed POCOs.
- `ExpressionRowFiltration : ExpressionRowFiltration<ExpandoObject>` — non-generic, the form used by the XML reader.

### Typed POCO

```csharp
public class ChangeRatioRow
{
    public int AdminReserveRatio { get; set; }
    public int AdminReserveRatioPrevious { get; set; }
}

ExpressionRowFiltration<ChangeRatioRow> filtration = new ExpressionRowFiltration<ChangeRatioRow>(
    "AdminReserveRatioPrevious != AdminReserveRatio");

source.LinkTo(filtration);
filtration.LinkTo(destination);
```

Property names in the expression resolve to public properties of `TInput`.

### ExpandoObject

```csharp
ExpressionRowFiltration filtration = new ExpressionRowFiltration(
    "Reserve > 0 && Type == \"Day\"");
```

### XML

```xml
<MemorySource>
    <LinkTo>
        <ExpressionRowFiltration>
            <FilterExpression>AdminReserveRatioPrevious != AdminReserveRatio</FilterExpression>
            <LinkTo>
                <SqlCommandTransformation>
                    ...
                </SqlCommandTransformation>
            </LinkTo>
        </ExpressionRowFiltration>
    </LinkTo>
</MemorySource>
```

When the expression contains XML special characters they must be escaped: `&gt;`, `&lt;`, `&amp;&amp;`, `&quot;`. An empty `<FilterExpression></FilterExpression>` is accepted at deserialization but throws `InvalidOperationException` at runtime when the first row arrives.

### Supported in expressions

| Category | Examples |
|----------|----------|
| Comparisons | `==`, `!=`, `>`, `<`, `>=`, `<=` |
| Logical | `&&`, `\|\|`, `!`, parentheses |
| Arithmetic | `+`, `-`, `*`, `/`, `%` |
| String literals | `Type == "Day"` |
| Null check | `AuthLimit != null` |
| Member access — nested ExpandoObject | `Order.Total > 100`, `Owner.Address.City.Name == "Moscow"` |
| Member access — custom class | `Person.Name == "John" && Person.Age > 18` |
| Collection methods | `Items.Any(Sum > 100)`, `Lines.Count() > 5`, `Lines.Sum(Amount) > 1000`, `Tags.Contains("Premium")` |

Supported value types: `decimal`, `int`, `string`, `bool`, `DateTime`, `Guid`, `byte[]`, `null`, nested `ExpandoObject`, custom classes with public properties, collections of items that share a unifiable shape (see Limitations on heterogeneity).

### When to use this vs ScriptedRowTransformation

`ScriptedRowTransformation` runs the body through Roslyn and gives you the full C# language: method calls, async, custom helper types, anything an inline lambda can do. The cost is that each new row shape compiles and loads a separate assembly via `Assembly.Load(bytes)`, and those assemblies are not unloadable.

`ExpressionRowFiltration` covers the common predicate cases (comparisons, arithmetic, member access, null checks, LINQ-style methods on collections) without that footprint. If you need richer behaviour — a custom method call, a string transformation, conversion of a complex type to another representation — `ScriptedRowTransformation` is still the right pick.

The two are complementary, not interchangeable. A future iteration may consolidate them; see [`docs/tech-debt/TECH-DEBT-Expression-Engine-Unification.md`](../tech-debt/TECH-DEBT-Expression-Engine-Unification.md).

### Calling user-type methods

`System.Linq.Dynamic.Core` resolves method calls on framework types (`string`, `DateTime`, `int`, `byte[]`, `List<T>` and other BCL) out of the box. **User-defined types** (your DTOs, domain objects) are not visible to the parser by default - an expression like `MyDto.SomeMethod() == 1` fails with a parse error.

To make instance methods on a user type callable from `FilterExpression`, register the type via `RegisterCustomTypes`:

```csharp
public sealed class Customer
{
    public bool IsPremium() => /* business rule */;
}

var filtration = new ExpressionRowFiltration<Order>(
    "Customer.IsPremium() && Total > 100");
filtration.RegisterCustomTypes(typeof(Customer));
```

The same pattern handles the `JsonNode -> string` case and any other "call a method on my type" scenario:

```csharp
filtration.RegisterCustomTypes(typeof(JsonNode));
filtration.FilterExpression = "Payload.ToJsonString().Length > 100";
```

For finer control over the parser - including extension methods, alternative type resolution, parser flags - mutate `ParsingConfig` directly:

```csharp
filtration.ParsingConfig.CustomTypeProvider = new MyCustomTypeProvider();
filtration.ParsingConfig.AllowEqualsAndToStringMethodsOnObject = true;
```

`RegisterCustomTypes` is a convenience layer over `ParsingConfig.CustomTypeProvider` for the common case; using `ParsingConfig` directly remains supported for advanced configuration.

### Bulk registration via `AdditionalAssemblyNames` and `AdditionalImports`

`RegisterCustomTypes(params Type[])` is per-type and works well from C# code where types are referenced symbolically. For XML-defined flows where users do not have type references at hand, two collection properties cover the bulk case:

- **`AdditionalAssemblyNames`** — names (or file paths) of assemblies to load. All public types from each assembly are added to the type provider, making them resolvable from `FilterExpression` without per-type registration.
- **`AdditionalImports`** — namespace prefixes used as imports when resolving short type names. With `AdditionalImports = ["MyCompany.Domain"]`, an expression like `MyType.Method()` resolves against `MyCompany.Domain.MyType` first.

Both are symmetric with `ScriptedRowTransformation.AdditionalAssemblyNames` / `AdditionalImports` (added in MR !115 on the Roslyn side, MLRSSL-1510), so users can switch between Roslyn-based and Dynamic-LINQ-based row filtration without changing the configuration shape.

XML form:

```xml
<ExpressionRowFiltration>
    <FilterExpression>Customer.IsPremium() &amp;&amp; Total &gt; 100</FilterExpression>
    <AdditionalAssemblyNames>
        <string>MyCompany.Domain</string>
    </AdditionalAssemblyNames>
    <AdditionalImports>
        <string>MyCompany.Domain</string>
    </AdditionalImports>
</ExpressionRowFiltration>
```

Programmatic form:

```csharp
filtration.AdditionalAssemblyNames = new[] { "MyCompany.Domain" };
filtration.AdditionalImports = new[] { "MyCompany.Domain" };
```

Assembly resolution tries three strategies in order: (1) already loaded in the current `AppDomain` by short or full name, (2) `Assembly.Load(AssemblyName)`, (3) `Assembly.LoadFrom(path)` as a fallback. An assembly that fails all three throws `InvalidOperationException` from the property setter — configuration errors surface at flow build time, not at evaluation time. The Roslyn-side `ScriptedRowTransformation` (see MR !115) uses a two-step variant (`Assembly.Load` -> `Assembly.LoadFrom` on `FileNotFoundException`/`FileLoadException`); the AppDomain pre-check we add as step 1 avoids double-loading already-resident assemblies in long-running processes.

`AdditionalAssemblyNames`, `AdditionalImports`, and `RegisterCustomTypes` compose: types from all three sources are unioned in the parser's custom-type set. Setters invalidate the compiled-predicate cache; the type provider is rebuilt on the next row evaluation, before the predicate is parsed. This avoids transient intermediate state during XML deserialization, where setter ordering is not guaranteed - the final provider is built once from whatever the user set, regardless of the order in which the fields arrived. If none of the three are set, a manually-assigned `ParsingConfig.CustomTypeProvider` is preserved untouched. Clearing every registration after having set one drops our installed provider and restores the framework default, so stale types or imports do not survive into subsequent evaluations.

### Thread safety

`ExpressionRowFiltration` is not thread-safe. Configure all of its public surface (`FilterExpression`, `AdditionalAssemblyNames`, `AdditionalImports`, `RegisterCustomTypes`, `ParsingConfig`) once before the dataflow starts. EtlKit runs each dataflow step on a single thread, so concurrent calls to `EvaluateExpression` from the same instance do not happen in normal usage. Sharing one instance across multiple parallel pipelines is unsupported.

### Limitations

- **Heterogeneous collections** — items with different field sets or with optional value-type fields (some items have `null`, others a concrete value) are unified automatically: missing fields are treated as `null`, and value-type fields that appear as both `null` and non-null are widened to `Nullable<T>`. Items with conflicting non-null types for the same field name (e.g. `Reserve = 1` in one item and `Reserve = "text"` in another) still throw `InvalidOperationException` with a pointer to the offending field. Mixing dictionary items and scalar items in the same collection also throws.
- **Empty collection with `.Any(predicate)`** — when the source `ExpandoObject` collection is empty its element type cannot be inferred, so it falls back to `List<object>`. `.Count()` and `.Any()` (no predicate) work; `.Any(predicate)` does not because there are no element properties to bind to.
- **Cyclic references** — runtime types are emitted by `DynamicClassFactory` and cannot reference themselves. ETL row data does not normally contain cycles, but if it does the mapping fails.
- **`byte[]` and `string`** — treated as scalar values, not as enumerable collections. `Tags.Contains("Premium")` works on `List<string>`; it does not work on a `string` field treated as a sequence of characters.
- **User-type method calls without registration** — calling a method on a user-defined class (not a framework type) requires `RegisterCustomTypes` (see "Calling user-type methods" above). Built-in types work without registration.

---

## Internals

This section describes the moving parts inside `ExpressionRowFiltration` for contributors who extend or maintain the component. Users of the public API can stop here.

### Generic path (typed `TInput`)

`new[] { row }.AsQueryable().Any(ParsingConfig, FilterExpression)`

The compile-time element type of the array is `TInput`, so `AsQueryable()` returns `IQueryable<TInput>` and `System.Linq.Dynamic.Core` resolves field names through `PropertyInfo` of `TInput`. No runtime type generation.

### ExpandoObject path

`ExpressionRowFiltration` (non-generic) overrides `EvaluateExpression` to map the row to a runtime DynamicClass and then invoke a cached compiled predicate. The predicate is compiled once per `(FilterExpression, ParsingConfig, mapped DynamicClass type)` triple and reused across rows.

`ExpandoTypeMapper.Map(row)` chooses one of two paths per row based on the shape:

- **Fast path** (flat shapes — scalars, nullables, strings, byte arrays, custom classes): a per-shape `Func<IDictionary<string, object?>, object>` is compiled once via Expression Trees and cached by `(field name, runtime value type)` signature. Per-row cost is one dictionary walk + cache lookup + delegate invoke. No reflection in the hot path. This covers the typical XML-defined flow case (DB row -> ExpandoObject with scalar fields).
- **Slow path** (shapes containing nested `IDictionary<string, object>` or item collections): recursive reflection-based mapping. For nested `IDictionary<string, object>` it generates a nested DynamicClass; for a collection it walks the items once to build a unified element shape and generates a `List<TElement>`; for a scalar or custom class it keeps the `GetType()` of the value. Recursion handles arbitrary nesting depth so predicates like `Order.Total > 100` and `Items.Any(Sum > 100)` keep working.

Both paths feed into `DynamicClassFactory.CreateType(properties)` which emits the runtime type into a persistent `AssemblyBuilder` (one shared builder per process) and caches by property signature. The cached compiled predicate then operates on the mapped instance directly via a `Func<object, bool>` wrapper - no per-row `AsQueryable()` wrap.

`ParsingConfig.ConvertObjectToSupportComparison = true` lets comparison operators work on properties typed as `object` — needed for null-valued fields and for mixed numeric literals such as `Reserve > 0` when `Reserve` is `decimal`.

### Memory characteristics

Types are emitted into one shared persistent `AssemblyBuilder` (Reflection.Emit). There is no `Assembly.Load(bytes)` per shape, so the assembly is not duplicated and not pinned in memory in the way Roslyn-compiled scripts are.

### File layout

Implementation in `EtlKit.DynamicLinq/`:

- `ExpressionRowFiltration.cs` — public surface. Generic `ExpressionRowFiltration<TInput>` and the non-generic `ExpressionRowFiltration : ExpressionRowFiltration<ExpandoObject>`. Holds the cached compiled predicate, the `EvaluateExpression` method, and the public properties (`FilterExpression`, `ParsingConfig`, `AdditionalAssemblyNames`, `AdditionalImports`).
- `ExpandoTypeMapper.cs` — internal static class. Handles the fast/slow path routing for `Map(ExpandoObject)` plus the reflection-based recursive mapping for nested shapes.
- `AssemblyResolver.cs` — internal static helper. `Load(string)` with the three-step resolution fallback (AppDomain → `Assembly.Load(AssemblyName)` → `Assembly.LoadFrom(path)`) and `GetExportedTypesSafe(Assembly)` for partial-load resilience.
- `DynamicLinqTypeProvider.cs` — internal `IDynamicLinqCustomTypeProvider` implementation. Holds the registered custom types and the namespace imports, used as `ParsingConfig.CustomTypeProvider`.
- `EtlBoxDynamicLinqServiceCollectionExtensions.cs` — DI registration helpers.
