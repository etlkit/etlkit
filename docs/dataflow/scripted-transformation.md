# Scripted row transformation

`ScriptedRowTransformation<TInput, TOutput>` (in `EtlKit.Scripting`) transforms each row by
evaluating a C# expression per output field. Expressions are compiled at runtime by the Roslyn
scripting engine, so every field mapping can use the full C# language: string interpolation,
LINQ, method calls, null-conditional operators, custom types. The component is non-blocking — it
processes rows one at a time and forwards results immediately.

The non-generic alias `ScriptedTransformation` is equivalent to
`ScriptedRowTransformation<ExpandoObject, ExpandoObject>` and is the form used by the XML reader.

---

## Basic usage

### ExpandoObject (dynamic rows)

Input fields are accessible by name inside each expression. The example below adds two computed
fields to each row:

```csharp
var source = new MemorySource();
source.DataAsList.Add(new ExpandoObject());
((IDictionary<string, object?>)source.DataAsList[0])["FirstName"] = "Jane";
((IDictionary<string, object?>)source.DataAsList[0])["LastName"] = "Doe";
((IDictionary<string, object?>)source.DataAsList[0])["Amount"] = 100m;

var transform = new ScriptedTransformation();
transform.Mappings["FullName"] = "$\"{FirstName} {LastName}\"";
transform.Mappings["TaxAmount"] = "Amount * 0.2m";

var dest = new MemoryDestination();
source.LinkTo(transform).LinkTo(dest);
source.Execute();
dest.Wait();
// Each output row has FullName = "Jane Doe" and TaxAmount = 20.0
```

### Typed objects

When both types are CLR classes, field names in expressions resolve to the public properties of
`TInput`:

```csharp
public class OrderRow
{
    public decimal Amount { get; set; }
    public string Category { get; set; } = "";
}

public class OutputRow
{
    public decimal TaxAmount { get; set; }
    public string Label { get; set; } = "";
}

var transform = new ScriptedRowTransformation<OrderRow, OutputRow>();
transform.Mappings["TaxAmount"] = "Amount * 0.2m";
transform.Mappings["Label"] = "$\"{Category}: {Amount}\"";
```

### XML

```xml
<ScriptedTransformation>
    <Mappings>
        <FullName>$"{FirstName} {LastName}"</FullName>
        <TaxAmount>Amount * 0.2m</TaxAmount>
    </Mappings>
    <LinkTo>
        <MemoryDestination />
    </LinkTo>
</ScriptedTransformation>
```

XML special characters in expressions must be escaped: `&quot;` for `"`, `&amp;` for `&`,
`&lt;` / `&gt;` for `<` / `>`.

---

## Properties

### `Mappings`

`Dictionary<string, string>`. Each key is an output field name; each value is a C# expression
whose result is assigned to that field. Inside an expression, input fields are available as
local variables with their exact field names. Standard C# syntax applies including string
interpolation (`$"..."`), LINQ, null-conditional (`?.`), and type casts.

```csharp
transform.Mappings["FullName"] = "$\"{FirstName} {LastName}\"";
transform.Mappings["Year"] = "((DateTime)CreatedAt).Year";
transform.Mappings["Items"] = "Items?.Count ?? 0";
```

### `PassThrough`

Default `false`. When `true`, all input fields are copied to the output before `Mappings` are
applied. Mappings can add new fields or override copied ones; fields not listed in `Mappings` are
preserved unchanged.

For typed transformations, `PassThrough = true` requires `TInput` to be the same type as or a
subtype of `TOutput`; an incompatible pair throws `InvalidOperationException` at runtime.

```xml
<ScriptedTransformation>
    <PassThrough>true</PassThrough>
    <Mappings>
        <!-- Adds FullName; original FirstName and LastName are preserved -->
        <FullName>$"{FirstName} {LastName}"</FullName>
    </Mappings>
    ...
</ScriptedTransformation>
```

### `FailOnMissingField`

Default `true`. When `true`, a script that references an absent input field or fails to compile
throws `ArgumentException` and stops the flow. When `false`, the affected output field is set to
`null` instead.

```csharp
transform.FailOnMissingField = false; // tolerate missing or uncompilable fields
```

### `AdditionalAssemblyNames`

Names (or file paths) of assemblies to load before compiling scripts. Accepts:

- A runtime assembly identity such as `"System.Text.Json"` — resolved via `Assembly.Load`.
- A file path such as `"libs/MyLib.dll"` — resolved via `Assembly.LoadFrom` when the name form
  fails.

```csharp
transform.AdditionalAssemblyNames = new[] { "System.Text.Json" };
```

XML form:

```xml
<AdditionalAssemblyNames>
    <string>System.Text.Json</string>
</AdditionalAssemblyNames>
```

### `AdditionalImports`

Namespace strings added as implicit `using` directives for every expression. Without this, types
must be fully qualified inside scripts.

```csharp
transform.AdditionalAssemblyNames = new[] { "System.Text.Json" };
transform.AdditionalImports = new[] { "System.Text.Json" };
// Now scripts can write JsonSerializer.Serialize(Id) instead of the full name.
transform.Mappings["Json"] = "JsonSerializer.Serialize(Id)";
```

XML form:

```xml
<AdditionalImports>
    <string>System.Text.Json</string>
</AdditionalImports>
```

### `NullableContextOptions`

Default `NullableContextOptions.Disable`. Set to `NullableContextOptions.Enable` to compile
expressions with a nullable-enabled context, which allows `string?` annotations and null-safe
operators such as `?.` and `??`.

```csharp
transform.NullableContextOptions = NullableContextOptions.Enable;
transform.Mappings["Name"] = "FirstName?.Trim() ?? \"Unknown\"";
```

`TypedScriptBuilder.WithNullableContextOptions(NullableContextOptions)` exposes the same setting
for users of the low-level scripting API.

---

## When to use this vs ExpressionRowFiltration

`ScriptedRowTransformation` compiles each mapping through Roslyn, giving access to the full C#
language — method calls, generics, custom types, async, any library. The cost is that each
distinct row shape (for `ExpandoObject`) or input type (for typed pairs) causes a Roslyn
compilation and `Assembly.Load(bytes)`. Those assemblies are pinned in memory for the lifetime of
the process.

`ExpressionRowFiltration` (in `EtlKit.DynamicLinq`) covers the filter-only case with a lighter
footprint: no Roslyn, no per-shape assembly, expression trees compiled via `Expression.Compile()`.
It supports comparisons, arithmetic, null checks, member access, and common LINQ-style collection
methods — but not arbitrary C# code.

Choose `ScriptedRowTransformation` when you need to produce new field values, call custom
methods, or use libraries. Choose `ExpressionRowFiltration` when you need a configurable
predicate and want to avoid the Roslyn dependency. See
[row-filtration.md](row-filtration.md) for details.

---

## Low-level API

`ScriptBuilder`, `TypedScriptBuilder`, and `ScriptRunner<T>` are the building blocks behind
`ScriptedRowTransformation`. They are public and can be used directly when you need finer
control — for example, to share a compiled runner across multiple pipeline stages, or to provide
a custom type hash to force cache isolation.

```csharp
// Build a runner from a typed input class
var builder = ScriptBuilder
    .Default.ForType<MyInputRow>()
    .WithReferences(new[] { Assembly.Load("System.Text.Json") })
    .WithImports(new[] { "System.Text.Json" })
    .WithNullableContextOptions(NullableContextOptions.Enable);

var runner = builder.CreateRunner<string>("JsonSerializer.Serialize(Id)");

// runner.Script.Compile() to check diagnostics before executing
var state = await runner.RunAsync(new MyInputRow { Id = 42 });
Console.WriteLine(state.ReturnValue); // "42"
```

For `ExpandoObject` shapes use `ScriptBuilder.Default.ForType(expandoInstance)`. The hash code
is computed from the set of field names and their runtime types; pass an explicit `hashCode` to
override it.

---

## Memory characteristics

Each distinct `ExpandoObject` shape (field names + types) or `TInput` type causes one Roslyn
compilation and `Assembly.Load(bytes)`. The resulting assembly is pinned in memory and cannot be
unloaded. Compilations are cached inside the `ScriptedRowTransformation` instance, so repeated
rows of the same shape incur only a dictionary lookup. Sharing one instance across the entire
pipeline (rather than creating one per row) is essential for performance.

If memory growth from accumulated assemblies is a concern, `ExpressionRowFiltration` (Dynamic
LINQ path) emits types into a single shared `AssemblyBuilder` without per-shape
`Assembly.Load` and is better suited to long-running processes with many distinct row shapes.
