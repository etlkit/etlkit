# Tech Debt: `DbConnectionString.ToString()` bypasses the `GetConnectionString()` normalization

## Context

`DbConnectionString<T,TBuilder>` exposes the connection string through two paths
(`EtlKit/src/Definitions/ConnectionStrings/DbConnectionString.cs`):

```csharp
public string Value
{
    get => GetConnectionString();   // virtual — derived classes may normalize the output
    set => Builder.ConnectionString = value;
}

protected virtual string GetConnectionString() => Builder.ConnectionString;

public override string ToString() => Builder.ConnectionString;  // bypasses the virtual
```

`SqlConnectionString` is the only overrider today: it rewrites
`Integrated Security=true` → `Integrated Security=SSPI`
(`EtlKit/src/Definitions/ConnectionStrings/SqlConnectionString.cs`), so the `Value` it reports is
deliberately *not* the raw builder string.

## Problem

For a `SqlConnectionString` built from `"...;Integrated Security=true;..."`, one instance reports
two different strings:

| Call | Returns |
|---|---|
| `connStr.Value` | `...;Integrated Security=SSPI;...` (normalized) |
| `connStr.ToString()` | `...;Integrated Security=true;...` (raw builder output) |

`ToString()` is the path consumers reach for incidentally — string interpolation, `string.Concat`,
logging, debugger display — and it silently hands back the exact form the override exists to avoid.
Every future `GetConnectionString()` override inherits the same trap.

Nothing inside this repository hits the divergence: `DbConnectionManager` always assigns
`ConnectionString.Value` to `DbConnection.ConnectionString`
(`EtlKit/src/Definitions/ConnectionManager/DbConnectionManager.cs`, three sites), and no test reads
`ToString()` on a connection string. The debt is the public API surface contradicting itself — two
members that both mean "the connection string" can disagree.

PR [#4](https://github.com/etlkit/etlkit/pull/4) documented the divergence in a `<remarks>` on
`ToString()` rather than changing base-class behavior in a docs-only PR, and flagged it for a
decision
([thread](https://github.com/etlkit/etlkit/pull/4#discussion_r3639195895)).

## Direction

1. **Fix (one line, near-non-breaking):** `public override string ToString() => GetConnectionString();`
   in `DbConnectionString<T,TBuilder>`. The only behavior change is for callers who depend on
   `ToString()` returning the *un*-normalized form — a form the library itself never uses
   internally. (`=> Value;` has the identical effect; calling `GetConnectionString()` states the
   intent directly.)
2. **Add a regression test:** construct
   `new SqlConnectionString("Data Source=.;Integrated Security=true;Initial Catalog=x")` and assert
   `ToString() == Value` (both contain `SSPI`, neither contains `true` for that key).
3. **If the divergence is ever judged intentional** (raw builder round-trip vs. normalized output),
   `ToString()` cannot be renamed away from `object.ToString()`, so the `<remarks>` added in PR #4
   would stay — but read today, the split is accidental, not designed.
4. Once fixed, drop the divergence `<remarks>` on `ToString()` added in PR #4 so the doc describes
   behavior, not the bug.

## Sites

| File | Line | Note |
|---|---|---|
| `EtlKit/src/Definitions/ConnectionStrings/DbConnectionString.cs` | 42 | `ToString()` returns `Builder.ConnectionString` directly |
| `EtlKit/src/Definitions/ConnectionStrings/DbConnectionString.cs` | 24-30 | `Value` routes through the virtual `GetConnectionString()` |
| `EtlKit/src/Definitions/ConnectionStrings/SqlConnectionString.cs` | 18-22 | Sole override — the `Integrated Security=true` → `SSPI` rewrite |
| `EtlKit/src/Definitions/ConnectionManager/DbConnectionManager.cs` | 62, 67, 113 | Internal consumers all use `.Value` — the trap only fires for external callers |

## Related

Surfaced during the PR [#4](https://github.com/etlkit/etlkit/pull/4) review
([discussion](https://github.com/etlkit/etlkit/pull/4#discussion_r3639195895)) while documenting the
previously-undocumented `DbConnectionString<T,TBuilder>` base class. The `<remarks>` on `ToString()`
added in that PR intentionally documents the current behavior until this debt is paid.
