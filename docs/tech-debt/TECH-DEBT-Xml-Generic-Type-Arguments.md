# Tech Debt: Generic Type Arguments in XML Pipeline Notation

## Problem

The XML pipeline notation read by `DataFlowXmlReader` cannot express generic type arguments.
A tag name is a plain CLR type name (`<MemorySource/>`, `<RowTransformation/>`), and
`GetTypeByName` (`EtlKit.Serialization/DataFlow/DataFlowXmlReader.cs`) resolves it as follows:

1. Find a non-generic type with the exact name.
2. Otherwise find a generic type definition named `{name}` with **exactly one** type parameter
   and close it with a **hardcoded** `typeof(ExpandoObject)`:

   ```csharp
   // GetTypeByName, DataFlowXmlReader.cs
   type = Array.Find(
       types,
       t =>
           t.Name.StartsWith($"{name}`")
           && t.IsGenericTypeDefinition
           && t.GetGenericArguments().Length == 1
   );
   ...
   return type.MakeGenericType(typeof(ExpandoObject));
   ```

Consequences:

- **Components with generic arity ≥ 2 are unreachable from XML.** `RowTransformation<TInput, TOutput>`,
  `Aggregation<,>`, `LookupTransformation<,>`, `MergeJoin<,,>` fail the
  `GetGenericArguments().Length == 1` filter; only their non-generic facades
  (e.g. `RowTransformation` = `RowTransformation<ExpandoObject, ExpandoObject>`) work.
- **Typed POCO pipelines are inexpressible.** There is no way to write `DbSource<Order>` — the
  `DbSource` tag always yields `DbSource<ExpandoObject>`.
- **Auxiliary generic properties cannot be configured.** Even where the row type stays
  `ExpandoObject`, a component property typed as e.g. `List<CustomerLookup>` or a
  generic strategy type cannot be named in the `type` attribute
  (`GetPropertyType`, `DataFlowXmlReader.cs` — simple names plus a `[]` array suffix only).
- **Name collisions are resolved silently.** `GetTypeByName` takes the first match across all
  loaded assemblies (`Array.Find`), `GetType` takes the last (`LastOrDefault`); two types with
  the same simple name in different assemblies produce an arbitrary, order-dependent winner.

## Constraints

XML 1.0 element names permit only `NameStartChar`/`NameChar` (letters, digits, `_`, `-`, `.`,
`:`); none of `` ` ``, `<`, `>`, `[`, `]`, `,` or spaces used by CLR/C# generic notation are
allowed in a tag name. Attribute **values**, by contrast, permit any character (with `&lt;`/`&amp;`
entities where needed). Any design therefore either mangles the tag name, escapes it, or moves the
type arguments out of the tag name.

## Prior Art

| Approach | Precedent | Verdict for EtlKit |
|---|---|---|
| `Of`-mangling in the name (`ArrayOfString`, `ResponseOfCustomer`) | .NET `XmlSerializer` | Rejected: not reversible without a lookup table (`OfOrderOrderDto` — `<Order, OrderDto>` or `<OrderOrder, Dto>`?), inflates the open set of tag names (breaks XSD), and worsens the already threefold overload of tag names (property / method / type). |
| `Of` + namespace hash (`MyTypeOfCustomerP5binz7X`) | .NET `DataContractSerializer` | Rejected: solves the ambiguity of plain `Of` at the cost of human readability; wrong trade-off for a hand-written pipeline configuration format. |
| Character escaping (`List_x0060_1`) | `XmlConvert.EncodeName` | Rejected: unreadable and still encodes only arity, not the arguments. |
| Type arguments in an attribute (`<scg:List x:TypeArguments="sys:String">`) | XAML 2006/2009 (WPF, Workflow Foundation) | **Chosen.** Tag stays the generic definition's simple name (exactly the current convention), arguments go where the character set is unrestricted, nested generics supported, XSD-friendly, battle-tested at scale in WF. |
| Full CLR type name in a `type`-like attribute | Spring XML, MSBuild, `xsi:type` | Partially adopted: the existing `type` attribute should share one type-name grammar with the new attribute. |

## Proposed Design

### Notation

Add an optional `typeArguments` attribute on any component element. The tag name keeps naming the
generic type definition; the attribute closes it:

```xml
<EtlDataFlowStep>
  <DbSource typeArguments="Order">
    ...
    <LinkTo>
      <RowTransformation typeArguments="Order, OrderDto">
        ...
        <LinkTo>
          <MemoryDestination typeArguments="OrderDto" />
        </LinkTo>
      </RowTransformation>
    </LinkTo>
  </DbSource>
</EtlDataFlowStep>
```

Type-name grammar (shared by `typeArguments` and the existing `type` attribute):

```
type-list  := type ("," type)*
type       := name ( "(" type-list ")" )? ( "[]" )*
name       := [alias ":"] simple-or-full CLR name
```

- Nested generics use parentheses, following the XAML precedent, because `(`/`)` and `,` are legal
  in attribute values without escaping: `typeArguments="Order, List(CustomerKey)"`.
- `[]` keeps its current meaning (array), now composable: `Order[]`, `List(Order)[]`.
- Whitespace around commas is insignificant.

### Resolution rules

1. **No attribute → current behavior, unchanged.** Non-generic exact match first; otherwise the
   arity-1 definition closed with `ExpandoObject`. Every existing XML document deserializes
   identically (backward compatible).
2. **Attribute present → arity must match exactly.** The generic definition is selected by simple
   name plus `GetGenericArguments().Length == <argument count>`. No match, or a non-generic type
   with a `typeArguments` attribute, is an `InvalidDataException` with the tag name, expected
   arities found, and supplied count — never a silent fallback.
3. **Argument name resolution order:**
   1. explicit alias registry (see below);
   2. the reader's existing `_types` cache (data-flow component types);
   3. scan of loaded assemblies (current `GetType` behavior), extended to report an error on
      ambiguity instead of silently taking the last match.
4. **Alias registry.** A `IReadOnlyDictionary<string, Type>` accepted by the `DataFlowXmlReader`
   constructor (and populated from DI via `ServiceProviderActivator` setup) maps short names to
   row/POCO types: `{"Order", typeof(MyApp.Rows.Order)}`. This is the supported way to reference
   application types; assembly scanning remains a fallback. A XAML-style `xmlns` →
   `clr-namespace` mapping is explicitly out of scope for the first iteration (it would require
   namespace-aware parsing of the whole document) but the `alias ":"` slot in the grammar reserves
   the syntax.

### Touch points

- `GetTypeByName(Type[] types, string name)` → gains the parsed argument list (or an overload
  `GetTypeByName(Type[] types, string name, IReadOnlyList<TypeName> args)`); the hardcoded
  `MakeGenericType(typeof(ExpandoObject))` becomes the empty-argument default branch.
- Call sites that resolve tag names must pass the element so the attribute is readable:
  `IDataFlowXmlContext.CreateObject(string typeName, XElement element)` already has the element;
  `AddDestinationAndInvokeMethod` and `InitializeRootPropertiesFromXml` read it from the
  `XElement`/`XmlReader` they already hold.
- New internal `TypeNameParser` for the grammar above, unit-tested in isolation; reused by
  `GetPropertyType` so the `type` attribute accepts the same syntax.
- `IDataFlowXmlContext.ResolveType(string typeName)` gains an overload taking the element (or the
  parsed arguments) so `IDataFlowXmlSerializable` implementors (e.g. `Pipeline`) resolve child
  tags consistently.

### Interaction with the `ExpandoObject` boundary (scoping decision)

The `IDataFlow` contract is monomorphic: `Source` is `IDataFlowSource<ExpandoObject>`,
`Destinations` is `List<IDataFlowDestination<ExpandoObject>>`. Until that boundary is generalized,
`typeArguments` on **flow-edge components** (root source, linked destinations) is limited to
`ExpandoObject`-compatible closures, and the reader must validate this and fail with a clear
message rather than at a later cast.

Phase the work accordingly:

- **Phase 1 (this document):** grammar, parser, resolution, alias registry; `typeArguments`
  usable for interior/auxiliary types — multi-arity transformations inside a `Pipeline` segment
  with explicit conversion at the edges, lookup source types, generic property types via the
  `type` attribute.
- **Phase 2 (separate decision, separate document):** typed end-to-end flows — either generalize
  `IDataFlow`/`EtlDataFlowStep` or add typed boundary adapters. Do not start Phase 2 before
  deciding whether typed row flows in XML are a real requirement; Phase 1 is useful on its own
  and does not prejudge the answer.

### Non-goals

- No `Of`-style mangled tag names, no `XmlConvert.EncodeName` escaping.
- No XML *writing* support (the subsystem is read-only today); the attribute notation is chosen
  to round-trip trivially if a writer appears.
- No XAML `xmlns`/`clr-namespace` support in Phase 1.
- No change to how non-generic tags, properties, `LinkTo`/`LinkErrorTo` method elements resolve.

## Testing

- `TypeNameParser` unit tests: flat lists, nesting, arrays, whitespace, malformed input
  (unbalanced parentheses, trailing comma, empty argument).
- `GetTypeByName` resolution: arity matching, `ExpandoObject` default, exact-arity error, alias
  registry precedence over assembly scan, ambiguity error.
- End-to-end deserialization in `EtlKit.Serialization.Tests`: a two-arity transformation inside a
  `Pipeline`, a `typeArguments`-closed source property, and a regression suite proving every
  existing test XML (no attribute) is byte-for-byte behavior-compatible.
- Robustness tests (`DataFlowXmlReaderRobustnessTests`): attribute on a non-generic type, unknown
  argument name, arity mismatch — all must produce descriptive `InvalidDataException`s.

## References

- `EtlKit.Serialization/DataFlow/DataFlowXmlReader.cs` — `GetTypeByName`, `GetPropertyType`,
  `CreateObject`
- `EtlKit.Serialization/DataFlow/IDataFlowXmlContext.cs` — context contract used by
  `IDataFlowXmlSerializable` components
- XAML `x:TypeArguments` directive — [MS-XAML] and WPF/WF usage (prior art for the attribute
  notation and the parentheses syntax for nested generics)
- `DataContractSerializer` generic-name mangling (`{Name}Of{Args}{hash}`) — considered and
  rejected alternative
