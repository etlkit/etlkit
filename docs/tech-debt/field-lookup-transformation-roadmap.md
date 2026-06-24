# Roadmap: FieldLookupTransformation — declarative lookup with XML serialization support

## Problem Description

`LookupTransformation<TInput, TSourceOutput>` (`EtlKit/src/Toolbox/DataFlow/LookupTransformation.cs`)
already supports loading a dictionary from any `IDataFlowSource<TSourceOutput>`, but the match and
enrichment configuration **cannot be XML-serialized**:

- **Attribute mode** (`[MatchColumn]` / `[RetrieveColumn]`) requires decorating C# types at compile
  time — not applicable in dynamic or configuration-driven scenarios.
- **Func mode** (`TransformationFunc: Func<TInput, TInput>`) — a delegate that cannot be serialized
  to XML or JSON.

This makes `LookupTransformation` unsuitable for use in serialized DataFlow via
`DataFlowXmlReader` (`EtlKit.Serialization/DataFlow/DataFlowXmlReader.cs`).

---

## Proposed Solution

A new component `FieldLookupTransformation` that:

1. Match/retrieve configuration — plain POCO lists with string field names (serializable).
2. `DictionarySource` property of type `IDataFlowSource<TSourceOutput>` — deserialized through the
   existing `DataFlowXmlReader.SetInterfaceProperty` mechanism with a `type="ConcreteType"` attribute in XML.
3. Optional: Roslyn script as a string (`EnrichmentScript`) for complex enrichment — in the
   `EtlKit.Scripting` project.

---

## Architecture

### Phase 1 — Core (EtlKit project)

**New files:**

- `EtlKit/src/Definitions/DataFlow/Type/LookupMatchColumn.cs`
- `EtlKit/src/Definitions/DataFlow/Type/LookupRetrieveColumn.cs`
- `EtlKit/src/Toolbox/DataFlow/FieldLookupTransformation.cs`

**Key class signature:**

```csharp
public class FieldLookupTransformation<TInput, TSourceOutput>
    : DataFlowTransformation<TInput, TInput>
{
    // Configuration — serializable POCOs
    public List<LookupMatchColumn>    MatchColumns    { get; set; } = new();
    public List<LookupRetrieveColumn> RetrieveColumns { get; set; } = new();

    // Cache of loaded dictionary rows
    public List<TSourceOutput> LookupData { get; set; } = new();

    // Interface property — DataFlowXmlReader deserializes via type="MemorySource" etc.
    // setter calls DictionarySource.LinkTo(LookupBuffer) — like LookupTransformation.Source
    public IDataFlowSource<TSourceOutput> DictionarySource { get; set; }
}

// Non-generic: ExpandoObject, ExpandoObject
public class FieldLookupTransformation : FieldLookupTransformation<ExpandoObject, ExpandoObject> { }
```

**POCOs for configuration:**

```csharp
public class LookupMatchColumn
{
    public string InputField  { get; set; }  // field on the input row (TInput)
    public string LookupField { get; set; }  // field in the dictionary row (TSourceOutput)
}

public class LookupRetrieveColumn
{
    public string LookupField { get; set; }  // dictionary field — value source
    public string OutputField { get; set; }  // input row field — write target
}
```

**Internal mechanics** (mirroring `LookupTransformation`):
- `CustomDestination<TSourceOutput> LookupBuffer` — collects rows from `DictionarySource` into `LookupData`.
- `RowTransformation<TInput, TInput>` with `InitAction = LoadLookupData` — lazy load on first row.
- `protected virtual TInput EnrichRow(TInput row)` — virtual for overriding in the scripted variant.
- Two paths: `EnrichTyped` (reflection, `PropertyInfo` cache) and `EnrichDynamic` (`IDictionary<string,object?>`,
  comparison via `.ToString()` — values from XML are always strings).
- `protected TSourceOutput? FindMatch(TInput row)` — extracted as a helper for reuse in subclasses.

### Phase 2 — Scripted variant (EtlKit.Scripting project)

**New file:** `EtlKit.Scripting/ScriptedFieldLookupTransformation.cs`

```csharp
public class ScriptedFieldLookupTransformation
    : FieldLookupTransformation<ExpandoObject, ExpandoObject>
{
    // String C# Roslyn script. Globals: dynamic row (mutable), dynamic? lookup (null if no match)
    public string? EnrichmentScript { get; set; }

    protected override ExpandoObject EnrichRow(ExpandoObject row)
    {
        if (string.IsNullOrWhiteSpace(EnrichmentScript))
            return base.EnrichRow(row);  // fallback to field-mapping
        // compilation and execution via ScriptBuilder/ScriptRunner (as in ScriptedRowTransformation)
        ...
    }
}
```

---

## XML Serialization

### Changes to DataFlowXmlReader

**Not required.** The `SetInterfaceProperty` mechanism (lines 437–468) already handles interface
properties: reads the `type="..."` attribute, resolves the concrete type via `GetTypeByName`, creates an instance
and calls the property setter. `MatchColumns` / `RetrieveColumns` — concrete `List<T>` — go through
`CreateList` → `CreateInstance` without additional logic.

### Example XML

```xml
<FieldLookupTransformation>
  <DictionarySource type="MemorySource">
    <Data>
      <ExpandoObject>
        <CustomerId>1</CustomerId>
        <CustomerName>Acme Corp</CustomerName>
      </ExpandoObject>
    </Data>
  </DictionarySource>
  <MatchColumns>
    <LookupMatchColumn>
      <InputField>CustomerId</InputField>
      <LookupField>CustomerId</LookupField>
    </LookupMatchColumn>
  </MatchColumns>
  <RetrieveColumns>
    <LookupRetrieveColumn>
      <LookupField>CustomerName</LookupField>
      <OutputField>CustomerName</OutputField>
    </LookupRetrieveColumn>
  </RetrieveColumns>
</FieldLookupTransformation>
```

For the scripted variant:

```xml
<ScriptedFieldLookupTransformation>
  <DictionarySource type="CsvSource"><Uri>products.csv</Uri></DictionarySource>
  <MatchColumns>...</MatchColumns>
  <EnrichmentScript>row.Name = lookup?.Name ?? "Unknown";</EnrichmentScript>
</ScriptedFieldLookupTransformation>
```

---

## Known Limitations

| Situation | Behavior |
|---|---|
| `<DictionarySource>` without `type` attribute | `InvalidDataException` (same as for other interface properties) |
| Typed source (`MemorySource<MyPoco>`) from XML | Not supported — reader resolves to `ExpandoObject`; API code only |
| Comparison in dynamic mode | `ToString()` on both sides — values from XML are always strings |
| Element order in XML | `<DictionarySource>` can appear before `<MatchColumns>` — MatchColumns are read only at stream execution time |

---

## Test Plan

### Phase 1 — `TestTransformations/src/FieldLookupTransformation/`

- `FieldLookupTypedPocoTests.cs` — typed POCO: single column, composite key, no match (pass-through unchanged)
- `FieldLookupDynamicTests.cs` — ExpandoObject: basic case, string type comparison, no `DictionarySource`
- `EtlKit.Serialization.Tests/FieldLookupSerializationTests.cs` — XML round-trip with MemorySource and CsvSource

### Phase 2 — `EtlKit.Scripting.Tests/`

- `ScriptedFieldLookupTransformationTests.cs` — enrichment script, fallback to field-mapping, null-lookup, XML round-trip

---

## Critical Files for Implementation

| File | Role |
|---|---|
| `EtlKit/src/Toolbox/DataFlow/LookupTransformation.cs` | Template for copying patterns |
| `EtlKit.Common/DataFlow/RowTransformation.cs` | Base class for enrichment function wrapper |
| `EtlKit.Common/DataFlow/CustomDestination.cs` | LookupBuffer pattern |
| `EtlKit.Serialization/DataFlow/DataFlowXmlReader.cs` lines 343–491 | Deserialization paths (no changes required) |
| `EtlKit.Scripting/ScriptedRowTransformation.cs` | ScriptBuilder/Runner pattern for Phase 2 |
| `EtlKit.Scripting/ScriptBuilder.cs` | Roslyn compilation infrastructure |
