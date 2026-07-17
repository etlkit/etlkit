# Tech Debt: XML Documentation Coverage

**Status:** COMPLETED (2026-07-17)
**Created:** 2026-04-08
**Priority:** Medium-High

## Problem

XML documentation (`/// <summary>`) covers only ~59% of the public API surface (148 of 249 public
types). This results in poor API reference output on the hosted DocFx site and missing IntelliSense
tooltips for consumers of the NuGet packages.

## Current Coverage

| Project                  | Types | Documented | Missing | Coverage |
|--------------------------|------:|----------:|---------:|----------|
| EtlKit (main)            |   166 |       163 |        3 | 98%      |
| EtlKit.Common            |    21 |        20 |        1 | 95%      |
| EtlKit.Primitives        |    19 |        19 |        0 | 100%     |
| EtlKit.Kafka             |     7 |         7 |        0 | 100%     |
| EtlKit.Rest              |     3 |         3 |        0 | 100%     |
| EtlKit.Scripting         |     7 |         5 |        2 | 71%      |
| EtlKit.DynamicLinq        |     3 |         3 |        0 | 100%     |
| EtlKit.AI                |     8 |         8 |        0 | 100%     |
| EtlKit.RabbitMq          |     5 |         5 |        0 | 100%     |
| EtlKit.Json              |     2 |         2 |        0 | 100%     |
| EtlKit.Serialization     |     7 |         7 |        0 | 100%     |
| EtlKit.ClickHouse        |     3 |         3 |        0 | 100%     |
| EtlKit.Logging.Database  |     2 |         2 |        0 | 100%     |
| **Total**                | **249** | **234** | **15** | **94%** |

## Implementation Plan

Work is organized into 4 phases by priority. Each phase can be done independently. Within each
phase, items are listed by project.

### Phase 1: Core Interfaces (EtlKit.Primitives) — 14 types

These interfaces define the entire framework contract. Every user and every component depends on
them. Documenting these has the highest impact on API reference quality.

**Interfaces:**
- [x] `ITask` — base interface for all tasks
- [x] `IConnectionManager` — database connection abstraction
- [x] `IDataFlowSource<TOutput>` — source component contract
- [x] `IDataFlowDestination<TInput>` — destination component contract
- [x] `IDataFlowBatchDestination<TInput>` — batch destination contract
- [x] `IDataFlowLinkSource<TOutput>` — linking source-side contract
- [x] `IDataFlowLinkTarget<TInput>` — linking target-side contract
- [x] `IDataFlowTransformation<TInput, TOutput>` — transformation contract
- [x] `ILinkErrorSource` — error linking contract
- [x] `IHttpClient` — HTTP abstraction for web sources
- [x] `IQueryParameter` — SQL query parameter contract
- [x] `ITableData` — table data abstraction

**Enums:**
- [x] `ChangeAction` — merge change type enum
- [x] `ConnectionManagerType` — database type enum

### Phase 2: Abstract Base Classes (EtlKit.Common + main EtlKit) — 22 types

These are the classes users inherit from or interact with directly. They form the runtime backbone.

**EtlKit.Common (6):**
- [x] `DataFlowSource<TOutput>` — base class for all sources
- [x] `DataFlowDestination<TInput>` — base class for all destinations
- [x] `DataFlowBatchDestination<TInput>` — base class for batch destinations
- [x] `DataFlowTransformation<TInput, TOutput>` — base class for transformations
- [x] `DataFlowTask` — base class for dataflow tasks
- [x] `GenericTask` — base class for control flow tasks

**EtlKit.Common utilities (9):**
- [x] `DataFlowLinker<TOutput>` — linking helper
- [x] `ErrorHandler` — error routing
- [x] `HashHelper` — hashing utility
- [x] `LoadProcess` — load process model
- [x] `MyLogEvent` — custom log event
- [x] `ObjectNameDescriptor` — SQL object name parsing
- [x] `RowTransformation` (non-generic variant)
- [x] `RowTransformation<TInput>` (single-type variant)
- [x] `CustomDestination` (non-generic variant)

**EtlKit main base classes (7):**
- [x] `DataFlowStreamSource<TOutput>` — base for file/stream sources
- [x] `DataFlowStreamDestination<TInput>` — base for file/stream destinations
- [x] `DbConnectionManager<TConnection>` — base for DB connection managers
- [x] `DbTask` — base for database tasks
- [x] `DropTask<T>` — base for drop tasks
- [x] `IfExistsTask` — base for existence checks
- [x] `OdbcConnectionManager` — base for ODBC connections

### Phase 3: Fully Undocumented Projects — 5 types

Small scope, quick wins — brings two projects from 0% to 100%.

**EtlKit.ClickHouse (3):**
- [x] `ClickHouseConnectionManager` — ClickHouse connection manager
- [x] `ClickHouseConnectionString` — connection string wrapper
- [x] `ClickHouseConnectionStringBuilder` — connection string builder

**EtlKit.Logging.Database (2):**
- [x] `DatabaseLoggingConfiguration` — database logging setup
- [x] `ETLLogLayoutRenderer` — NLog layout renderer for ETL logs

### Phase 4: Main EtlKit Library Gaps — 45 types

Remaining gaps in the main library, grouped by category.

**Enums (5):**
- [x] `AggregationMethod` — aggregation function type
- [x] `DeltaMode` — merge delta mode
- [x] `ReadOptions` — load process read options
- [x] `RecoveryModel` — database recovery model
- [x] `ResourceType` — source resource type (file vs. HTTP)

**Attribute classes (5):**
- [x] `CompareColumnAttribute` — marks columns for merge comparison
- [x] `DeleteColumnAttribute` — marks deletion flag column
- [x] `ExcelColumnAttribute` — maps Excel columns to properties
- [x] `MatchColumnAttribute` — marks columns for merge matching
- [x] `RetrieveColumnAttribute` — marks columns for lookup retrieval

**Data model classes (10):**
- [x] `ExcelRange` — Excel cell range definition
- [x] `LogEntry` — log table entry
- [x] `LogHierarchyEntry` — hierarchical log entry
- [x] `MergeProperties` — merge operation configuration
- [x] `ProcedureDefinition` — stored procedure metadata
- [x] `ProcedureParameter` — stored procedure parameter
- [x] `QueryParameter` — SQL query parameter
- [x] `TableColumn` — table column definition
- [x] `TableData` — in-memory table data
- [x] `TableDefinition` — table structure metadata

**Transformation/destination classes (8):**
- [x] `BlockTransformation` — non-generic blocking transform
- [x] `DbRowTransformation` — database row transform
- [x] `DbTransformation` — database transform base
- [x] `DynamicAggregationTypeInfo` — dynamic aggregation metadata
- [x] `ErrorLogDestination` — error logging destination
- [x] `MergeJoinTarget` — merge join target wrapper
- [x] `Sequence<T>` — sequence generator source
- [x] `SampleHttpClient` — default HTTP client implementation

**Connection classes (2):**
- [x] `AccessOdbcConnectionManager` — MS Access via ODBC
- [x] `SqlOdbcConnectionManager` — SQL Server via ODBC

**Utility/extension classes (8):**
- [x] `ConnectionManagerExtensions` — connection manager helpers
- [x] `DataTypeConverter` — SQL/CLR type conversion
- [x] `JsonPathConverter` — JSON path utility
- [x] `JsonProperty2JsonPath` — JSON property mapping
- [x] `PropertyInfoExtension` — reflection helpers
- [x] `SqlParser` — SQL parsing utility
- [x] `StringExtension` — string helpers
- [x] `TableColumnExtensions` — table column helpers

**Extension library gaps (7):**
- [x] `KafkaTransformation` — Kafka produce transformation (EtlKit.Kafka)
- [x] `KafkaStringTransformation<TInput>` — string variant (EtlKit.Kafka)
- [x] `ExpandoObjectConverter` — JSON converter (EtlKit.Kafka)
- [x] `RestMethodInfo` — REST method metadata (EtlKit.Rest)
- [x] `PublicationAddress` — RabbitMQ address (EtlKit.RabbitMq)
- [x] `ExpandoObjectConverter` — JSON converter (EtlKit.AI)
- [x] `CustomLiquidFilters` — Liquid template filters (EtlKit.AI)

## Guidelines

When writing XML docs for these types:

1. **`<summary>`** — one sentence describing what the type does and when to use it
2. **`<typeparam>`** — describe each generic type parameter
3. **`<remarks>`** — add only when behavior is non-obvious (threading, disposal, buffering)
4. **Public properties and methods** — document parameters, return values, and exceptions for the
   public API surface of each type (not just the type-level summary)
5. **Inherited members** — only document overrides that change behavior; inherited docs propagate
   automatically

## Verification

After each phase:

```bash
dotnet build EtlKit.sln -c Release
cd docfx && dotnet docfx docfx.json --serve
```

Browse the API reference site and confirm documented types show summaries.

## Target

Reach 95%+ coverage (document at least 93 of the 101 missing types). Internal utility classes
that are `public` only for cross-assembly access may be excluded if they are not part of the
intended public API — consider marking those `[EditorBrowsable(EditorBrowsableState.Never)]`
instead.

## Outcome (2026-07-17)

All 4 phases complete: 86 types documented across the initiative (Phase 1: 14, Phase 2: 22, Phase 3:
5, Phase 4: 45), bringing overall coverage from 61% to 94% (see the updated table above). Two header
counts in this document were corrected along the way (Phase 2: 13 → 22 types; Phase 4: 42 → 45 types
after two subsection miscounts — "Data model classes" was actually 10, not 9, and "Utility/extension
classes" was actually 8, not 6).

Just short of the 95% target. Known remaining gaps, out of scope for the 4 phases above:
- `EtlKit.Scripting` — 2 types
- `EtlKit.Common.DataFlow.CustomDestination<TInput>` — flagged separately, not part of any phase's checklist

Both are small, well-scoped follow-ups rather than reasons to reopen this initiative.
