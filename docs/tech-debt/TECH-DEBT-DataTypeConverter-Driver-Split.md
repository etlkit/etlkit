# Tech Debt: split `DataTypeConverter` driver conventions before moving type-mapping to Common

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

## Why we did NOT move it (decision)

`DataTypeConverter` mixes two concerns:

- **Driver-independent type mapping** — pure string/`DbType`/`Type` conversions with no DB specifics:
  `GetNETObjectTypeString`, `GetDBType`, `GetTypeObject`, `GetNETDateTimeKind`,
  `IsCharTypeDefinition`, `GetStringLengthFromCharString`, the default-length constants.
- **Driver-dependent conventions** — `TryGetDBSpecificType(ITableColumn, ConnectionManagerType)` with
  a `switch` over `ConnectionManagerType`, plus `GetPostgreSqlType` and `GetClickHouseType`. These
  encode per-driver SQL-type rules.

Moving the whole class into `EtlKit.Common` would drag the **driver-dependent** conventions into the
shared package. That is exactly the wrong direction: the longer-term plan is to split drivers into
their own packages and resolve their specifics via DI. Centralising driver conventions in Common now
would only enlarge that later refactor.

So for now: `QueryParameter`, `DataTypeConverter`, and `ITableColumn` **stay in the main `EtlKit`
package**. `DbCheckpointStore` uses a small private `IQueryParameter` (checkpoint params are always
strings, so it needs no type mapping).

## Future plan

1. Extract the **driver-independent** type mapping out of `DataTypeConverter` into a shared home
   (`EtlKit.Common` or `EtlKit.Primitives`) — pure functions, no `ITableColumn`/driver switch.
2. Move the **driver-dependent** conventions (`TryGetDBSpecificType` and friends) into per-driver
   packages (e.g. `EtlKit.Postgres`, `EtlKit.ClickHouse`, …) behind an abstraction (e.g.
   `IDbTypeConventions` keyed by `ConnectionManagerType`) resolved via DI, removing the central
   `switch (ConnectionManagerType)`.
3. `ITableColumn` is a pure interface and can move to `EtlKit.Primitives`.
4. Once the pure mapping is in Common, move `QueryParameter` there and drop the private parameter in
   `DbCheckpointStore`.

This is deferred and should ride along with the broader driver-package / DI modularization, not be
done piecemeal.
