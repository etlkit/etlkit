# Tech Debt: three copies of `ExpandoObjectConverter` — consolidate into `EtlKit.Common`

## Context

Three packages each carry their own copy-pasted `System.Text.Json` converter that reads a JSON
object into an `ExpandoObject` tree:

| Copy | Visibility | Namespace |
|---|---|---|
| `EtlKit.Kafka/ExpandoObjectConverter.cs` | `public` | `EtlKit.DataFlow` |
| `EtlKit.AI/ExpandoObjectConverter.cs` | `public sealed` | `EtlKit.AI` |
| `EtlKit.Rest/ExpandoObjectConverter.cs` | `internal` | `EtlKit.Rest` |

All three implement the same recursive `Read`/`ReadValue`/`ReadArray` shape over
`JsonConverter<ExpandoObject>`. Note the Kafka copy is a *public* type squatting in the
`EtlKit.DataFlow` namespace — if the main library ever adds a converter there, the names collide.

## Problem

The copies have already drifted behaviorally, silently:

- **Property naming policy** — only the Kafka copy applies `options.PropertyNamingPolicy` to
  property names on read. The AI and Rest copies ignore the caller's policy.
- **Null array elements** — the AI copy preserves them (`[1, null, 2]` → `[1, null, 2]`); the
  Kafka and Rest copies silently drop them (`[1, null, 2]` → `[1, 2]`). For an ETL library,
  dropping elements shifts array indices and mutates data shape without any signal to the caller.

The XML docs have drifted too: PR [#4](https://github.com/etlkit/etlkit/pull/4) documented the
Kafka and AI copies independently, so the same `options` parameter is described as "used for the
property naming policy" in one file and "passed through to nested reads" in the other. The review
flagged the duplication ([thread](https://github.com/etlkit/etlkit/pull/4#discussion_r3639195921));
consolidation was agreed to be real but out of scope for a docs-only PR — hence this debt.

Every future fix (and doc improvement) currently has to be made three times or the copies drift
further.

## Direction

1. **One public converter in `EtlKit.Common`.** All three consumers can reach it: `EtlKit.Kafka`
   references `EtlKit.Common` directly; `EtlKit.AI` and `EtlKit.Rest` get it transitively through
   their `EtlKit` project reference.
2. **Decide the unified behavior explicitly** (both deltas are user-visible, so the changelog entry
   must call them out):
   - Apply `options.PropertyNamingPolicy` (the Kafka behavior). Ignoring options the caller
     explicitly passed is the surprising choice; callers that want raw names simply don't set a
     policy.
   - Preserve null array elements (the AI behavior). It is lossless; the dropping variant
     changes array length and indices with no opt-out.
3. **Migrate call sites** — `KafkaJsonSource` (`EtlKit.Kafka/KafkaJsonSource.cs:18`),
   `AIBatchTransformation` (`EtlKit.AI/AIBatchTransformation.cs:36`),
   `RestTransformation` (`EtlKit.Rest/RestTransformation.cs:31`) — and delete the copies. The Rest
   copy is `internal`, so deleting it is free. The Kafka and AI copies are public API: either keep
   `[Obsolete]` forwarders for one release or take the break consciously per versioning policy.
4. **Port the tests** — `EtlKit.AI.Tests/ExpandoObjectConverterTests.cs` becomes the test suite of
   the shared converter (moved to `EtlKit.Common.Tests`), extended with cases pinning the two
   behavior decisions above (naming policy applied, nulls preserved).
5. The consolidated type gets one authoritative XML doc block, resolving the drift from PR #4.

## Sites

| File | Line | Note |
|---|---|---|
| `EtlKit.Kafka/ExpandoObjectConverter.cs` | 9 | Public, `EtlKit.DataFlow` namespace, applies naming policy, drops null array elements |
| `EtlKit.AI/ExpandoObjectConverter.cs` | 9 | Public sealed, keeps null array elements |
| `EtlKit.Rest/ExpandoObjectConverter.cs` | 9 | Internal, drops null array elements |
| `EtlKit.Kafka/KafkaJsonSource.cs` | 18 | Consumer |
| `EtlKit.AI/AIBatchTransformation.cs` | 36 | Consumer |
| `EtlKit.Rest/RestTransformation.cs` | 31 | Consumer |
| `EtlKit.AI.Tests/ExpandoObjectConverterTests.cs` | — | Only existing test coverage; move + extend |

## Related

Surfaced by the PR [#4](https://github.com/etlkit/etlkit/pull/4) review
([discussion](https://github.com/etlkit/etlkit/pull/4#discussion_r3639195921)). The drifted XML
docs on the Kafka/AI copies stay as-is until this consolidation lands.
