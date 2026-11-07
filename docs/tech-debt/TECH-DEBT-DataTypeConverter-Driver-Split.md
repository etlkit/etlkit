# Tech Debt: split `DataTypeConverter` driver conventions before moving type-mapping to Common

**Status:** PARTIALLY DONE (2026-10-13) — the shared type mapping, the conventions abstraction and the
`QueryParameter` move are implemented. Moving the remaining driver rules into driver packages, moving
`ITableColumn` to Primitives and removing the obsolete facade are still open, see
[Remaining work](#remaining-work).

## Context

The streaming abstractions (`ICheckpointStore`, `CheckpointWriter<T>`, `DbCheckpointStore`) live in
`EtlKit.Common`. While building `DbCheckpointStore` we wanted to reuse the public
`EtlKit.QueryParameter` instead of a private parameter type, which would mean moving
`QueryParameter` into `EtlKit.Common`.

`QueryParameter` is not self-contained:

```
QueryParameter  ->  DataTypeConverter.GetDBType(string)   (EtlKit.ConnectionManager, main EtlKit)
DataTypeConverter  ->  ITableColumn                       (EtlKit, main EtlKit)
```

So moving `QueryParameter` pulls `DataTypeConverter` (and `ITableColumn`) along with it.

## Original decision (why it was not moved at first)

`DataTypeConverter` mixes two concerns:

- **Driver-independent type mapping** — pure string/`DbType`/`Type` conversions with no DB specifics:
  `GetNETObjectTypeString`, `GetDBType`, `GetTypeObject`, `GetNETDateTimeKind`,
  `IsCharTypeDefinition`, `GetStringLengthFromCharString`, the default-length constants.
- **Driver-dependent conventions** — `TryGetDBSpecificType(ITableColumn, ConnectionManagerType)` with
  a `switch` over `ConnectionManagerType`, plus `GetPostgreSqlType` and `GetClickHouseType`. These
  encode per-driver SQL-type rules.

Moving the whole class into `EtlKit.Common` would drag the **driver-dependent** conventions into the
shared package. That is exactly the wrong direction: the longer-term plan is to split drivers into
their own packages and resolve their specifics via DI. So the two concerns had to be separated first.

## Outcome

### 1. Driver-independent mapping: `EtlKit.Common.SqlTypeMapper`

The pure functions and the default-length constants moved verbatim into the new static class
`EtlKit.Common.SqlTypeMapper`. `TableColumn`, `QueryParameter` and `ClickHouseConnectionManager` call
it directly.

### 2. Driver-dependent conventions behind an abstraction

- `IDbTypeConventions.GetDbSpecificType(ITableColumn)` (namespace `EtlKit.ConnectionManager`) returns
  the SQL type a column is created with on an engine.
- Every `DbConnectionManager<TConnection>` exposes its engine's rules as the virtual property
  `TypeConventions`. The built-in managers override it with their own; by default it follows the engine
  the manager reports as `ConnectionManagerType`, so a custom subclass that reports PostgreSQL keeps
  getting the PostgreSQL rules, while MySQL and ADOMD, which have no special rules, keep every data type
  unchanged. A subclass can override the property to supply its own rules.
- The SQL Server (native and ODBC), PostgreSQL, SQLite and Access rules are internal classes next to
  their connection managers. The ClickHouse rule lives in `EtlKit.ClickHouse`.
- `CreateTableTask` takes a column's SQL type from its connection manager's conventions, so the
  central `switch (ConnectionManagerType)` no longer decides column types.

The conventions come from the connection manager rather than from DI on purpose: control-flow tasks
are created with `new` and read the connection manager, so there is no service provider to resolve
from. A DI registry can be added later behind the same property.

The central `switch` survives only inside the internal `EngineKeyedTypeConventions`. It serves the
managers that supply no conventions of their own (a custom subclass of `DbConnectionManager<TConnection>`
or `OdbcConnectionManager` keeps the rules of the engine it reports, as it did when the rules were
picked by engine), a custom `IConnectionManager` that is not a `DbConnectionManager<TConnection>`, and
the obsolete `DataTypeConverter.TryGetDBSpecificType`. It keeps a second copy of the ClickHouse rule:
the ClickHouse package depends on the main package, not the other way round. A test keeps the two
copies from drifting apart.

### 3. `QueryParameter` moved to `EtlKit.Common`

`QueryParameter` keeps its name and namespace (`EtlKit.QueryParameter`), so source code is unaffected;
`EtlKit.dll` carries a `TypeForwardedTo` so assemblies compiled against an older version still resolve
it. It gained a constructor that takes a `DbType` directly, because the SQL-type-name constructor can
neither express every `DbType` (it never yields `DateTime2`) nor take a .NET type name (`"Int64"`
silently becomes `DbType.String`).

The two private copies are gone: the nested `Parameter` of `DbCheckpointStore` and the nested
`QueryParameter` of `PostgresXminTailSource`.

### What changed for users

- **Added:** `SqlTypeMapper`, `IDbTypeConventions`, `DbConnectionManager<TConnection>.TypeConventions`,
  `QueryParameter(string name, DbType dbType, object value)`.
- **Obsolete, still working:** every member of `DataTypeConverter`; each forwards to its replacement.
- **Moved:** `QueryParameter` to `EtlKit.Common` (same full name, forwarded from `EtlKit.dll`).
- **Unchanged:** the SQL that `CreateTableTask` emits for every engine, and the `DbType` of every
  `QueryParameter` built from a SQL type name.

## Remaining work

- [ ] Move `ITableColumn` and `IDbTypeConventions` to `EtlKit.Primitives` (with type forwarders). Only
  needed once driver packages must not reference the main package; today the abstraction sits next to
  `ITableColumn` in main `EtlKit`.
- [ ] Split the SQL Server, PostgreSQL, MySQL, SQLite and Access connection managers, together with
  their conventions, into their own packages, as ClickHouse already is; add DI registration where
  that modularization introduces it.
- [ ] Remove the obsolete `DataTypeConverter` and `EngineKeyedTypeConventions` (with the second copy of
  the ClickHouse rule) in the next major version, together with the tests of the obsolete members.
- [ ] The other `switch (ConnectionManagerType)` statements (identity, `NOT NULL`, comments, quoting
  helpers) in `CreateTableTask` and about thirty other files are a separate, larger refactoring.

## Behavior pinned by tests that is worth revisiting

The code moved as it was; its quirks are pinned by tests (see the comments there) so that the move
could be verified. Fixing any of them changes output and belongs in a separate change that updates
the matching expectation:

- `int8` maps to `Int32` although PostgreSQL's `int8` is 64-bit; `tinyint` maps to `UInt16`;
  `real`/`float4` map to `Double`; `time`/`timetz` map to `DateTime`.
- `datetime2` maps to `DbType.DateTime`, never to `DbType.DateTime2`.
- `GetNETDateTimeKind` is case-sensitive and exact, so `"DATETIME"` yields no kind, while
  `TableColumn.NETDateTimeKind` passes the column's data type unchanged.
- `GetStringLengthFromCharString("varchar(10) NOT NULL")` returns the default length (255): text
  after the type defeats the parse.
- `varchar(max)` is not recognized as a char type, because the length must be digits.
- Some rules return upper-cased text and others the column's original text: on PostgreSQL
  `nvarchar(50)` becomes `VARCHAR(50)` but `varchar(50)` stays `varchar(50)`. PostgreSQL leaves
  `datetime2` unchanged although it has no such type.
- `CreateTableTask` emits no `NOT NULL`/`NULL` for MySQL and Access columns at all.
- The type name handling uses culture-sensitive `ToLower()`, `ToUpper()` and `StartsWith(string)`;
  under a Turkish culture `"INT"` no longer matches `"int"`. No test covers this.

## Verification

- `EtlKit.Common.Tests` (no database): the mapping and the per-engine conventions are pinned with
  literal expectations, one check per obsolete `DataTypeConverter` member keeps the facade honest, and
  `CreateTableTask.Sql` is compared with recorded DDL for six engines built from real, never opened
  connection managers. Custom connection manager subclasses are checked to keep the rules of the
  engine they report, and a guard fails when a concrete connection manager is not covered.
- The integration tests for `CreateTableTask`, the ClickHouse connection manager and the database
  data-type tests were run against SQL Server, PostgreSQL, MySQL and ClickHouse containers before the
  first change and again after each step, with identical results. `PostgresXminTailSource` was run
  before and after the `QueryParameter` move, `SqlTask` (the other user of `QueryParameter`) after it.
