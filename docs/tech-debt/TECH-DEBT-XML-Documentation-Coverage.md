# Tech Debt: XML Documentation Coverage

**Status:** Open
**Created:** 2026-04-08
**Priority:** Medium-High

## Problem

XML documentation (`/// <summary>`) covers only ~59% of the public API surface (148 of 249 public
types). This results in poor API reference output on the hosted DocFx site and missing IntelliSense
tooltips for consumers of the NuGet packages.

## Current Coverage

| Project                  | Types | Documented | Missing | Coverage |
|--------------------------|------:|----------:|---------:|----------|
| EtlKit (main)            |   166 |       117 |       49 | 70%      |
| EtlKit.Common            |    21 |         6 |       15 | 28%      |
| EtlKit.Primitives        |    19 |         5 |       14 | 26%      |
| EtlKit.Kafka             |     7 |         4 |        3 | 57%      |
| EtlKit.Rest              |     3 |         2 |        1 | 66%      |
| EtlKit.Scripting         |     7 |         5 |        2 | 71%      |
| EtlKit.DynamicLinq        |     3 |         3 |        0 | 100%     |
| EtlKit.AI                |     8 |         6 |        2 | 75%      |
| EtlKit.RabbitMq          |     5 |         4 |        1 | 80%      |
| EtlKit.Json              |     2 |         2 |        0 | 100%     |
| EtlKit.Serialization     |     7 |         7 |        0 | 100%     |
| EtlKit.ClickHouse        |     3 |         0 |        3 | 0%       |
| EtlKit.Logging.Database  |     2 |         0 |        2 | 0%       |
| **Total**                | **249** | **148** | **101** | **59%** |

## Implementation Plan

Work is organized into 4 phases by priority. Each phase can be done independently. Within each
phase, items are listed by project.

### Phase 1: Core Interfaces (EtlKit.Primitives) — 14 types

These interfaces define the entire framework contract. Every user and every component depends on
them. Documenting these has the highest impact on API reference quality.

**Interfaces:**
- [ ] `ITask` — base interface for all tasks
- [ ] `IConnectionManager` — database connection abstraction
- [ ] `IDataFlowSource<TOutput>` — source component contract
- [ ] `IDataFlowDestination<TInput>` — destination component contract
- [ ] `IDataFlowBatchDestination<TInput>` — batch destination contract
- [ ] `IDataFlowLinkSource<TOutput>` — linking source-side contract
- [ ] `IDataFlowLinkTarget<TInput>` — linking target-side contract
- [ ] `IDataFlowTransformation<TInput, TOutput>` — transformation contract
- [ ] `ILinkErrorSource` — error linking contract
- [ ] `IHttpClient` — HTTP abstraction for web sources
- [ ] `IQueryParameter` — SQL query parameter contract
- [ ] `ITableData` — table data abstraction

**Enums:**
- [ ] `ChangeAction` — merge change type enum
- [ ] `ConnectionManagerType` — database type enum

### Phase 2: Abstract Base Classes (EtlKit.Common + main EtlKit) — 13 types

These are the classes users inherit from or interact with directly. They form the runtime backbone.

**EtlKit.Common (6):**
- [ ] `DataFlowSource<TOutput>` — base class for all sources
- [ ] `DataFlowDestination<TInput>` — base class for all destinations
- [ ] `DataFlowBatchDestination<TInput>` — base class for batch destinations
- [ ] `DataFlowTransformation<TInput, TOutput>` — base class for transformations
- [ ] `DataFlowTask` — base class for dataflow tasks
- [ ] `GenericTask` — base class for control flow tasks

**EtlKit.Common utilities (9):**
- [ ] `DataFlowLinker<TOutput>` — linking helper
- [ ] `ErrorHandler` — error routing
- [ ] `HashHelper` — hashing utility
- [ ] `LoadProcess` — load process model
- [ ] `MyLogEvent` — custom log event
- [ ] `ObjectNameDescriptor` — SQL object name parsing
- [ ] `RowTransformation` (non-generic variant)
- [ ] `RowTransformation<TInput>` (single-type variant)
- [ ] `CustomDestination` (non-generic variant)

**EtlKit main base classes (7):**
- [ ] `DataFlowStreamSource<TOutput>` — base for file/stream sources
- [ ] `DataFlowStreamDestination<TInput>` — base for file/stream destinations
- [ ] `DbConnectionManager<TConnection>` — base for DB connection managers
- [ ] `DbTask` — base for database tasks
- [ ] `DropTask<T>` — base for drop tasks
- [ ] `IfExistsTask` — base for existence checks
- [ ] `OdbcConnectionManager` — base for ODBC connections

### Phase 3: Fully Undocumented Projects — 5 types

Small scope, quick wins — brings two projects from 0% to 100%.

**EtlKit.ClickHouse (3):**
- [ ] `ClickHouseConnectionManager` — ClickHouse connection manager
- [ ] `ClickHouseConnectionString` — connection string wrapper
- [ ] `ClickHouseConnectionStringBuilder` — connection string builder

**EtlKit.Logging.Database (2):**
- [ ] `DatabaseLoggingConfiguration` — database logging setup
- [ ] `ETLLogLayoutRenderer` — NLog layout renderer for ETL logs

### Phase 4: Main EtlKit Library Gaps — 42 types

Remaining gaps in the main library, grouped by category.

**Enums (5):**
- [ ] `AggregationMethod` — aggregation function type
- [ ] `DeltaMode` — merge delta mode
- [ ] `ReadOptions` — load process read options
- [ ] `RecoveryModel` — database recovery model
- [ ] `ResourceType` — source resource type (file vs. HTTP)

**Attribute classes (5):**
- [ ] `CompareColumnAttribute` — marks columns for merge comparison
- [ ] `DeleteColumnAttribute` — marks deletion flag column
- [ ] `ExcelColumnAttribute` — maps Excel columns to properties
- [ ] `MatchColumnAttribute` — marks columns for merge matching
- [ ] `RetrieveColumnAttribute` — marks columns for lookup retrieval

**Data model classes (9):**
- [ ] `ExcelRange` — Excel cell range definition
- [ ] `LogEntry` — log table entry
- [ ] `LogHierarchyEntry` — hierarchical log entry
- [ ] `MergeProperties` — merge operation configuration
- [ ] `ProcedureDefinition` — stored procedure metadata
- [ ] `ProcedureParameter` — stored procedure parameter
- [ ] `QueryParameter` — SQL query parameter
- [ ] `TableColumn` — table column definition
- [ ] `TableData` — in-memory table data
- [ ] `TableDefinition` — table structure metadata

**Transformation/destination classes (8):**
- [ ] `BlockTransformation` — non-generic blocking transform
- [ ] `DbRowTransformation` — database row transform
- [ ] `DbTransformation` — database transform base
- [ ] `DynamicAggregationTypeInfo` — dynamic aggregation metadata
- [ ] `ErrorLogDestination` — error logging destination
- [ ] `MergeJoinTarget` — merge join target wrapper
- [ ] `Sequence<T>` — sequence generator source
- [ ] `SampleHttpClient` — default HTTP client implementation

**Connection classes (2):**
- [ ] `AccessOdbcConnectionManager` — MS Access via ODBC
- [ ] `SqlOdbcConnectionManager` — SQL Server via ODBC

**Utility/extension classes (6):**
- [ ] `ConnectionManagerExtensions` — connection manager helpers
- [ ] `DataTypeConverter` — SQL/CLR type conversion
- [ ] `JsonPathConverter` — JSON path utility
- [ ] `JsonProperty2JsonPath` — JSON property mapping
- [ ] `PropertyInfoExtension` — reflection helpers
- [ ] `SqlParser` — SQL parsing utility
- [ ] `StringExtension` — string helpers
- [ ] `TableColumnExtensions` — table column helpers

**Extension library gaps (7):**
- [ ] `KafkaTransformation` — Kafka produce transformation (EtlKit.Kafka)
- [ ] `KafkaStringTransformation<TInput>` — string variant (EtlKit.Kafka)
- [ ] `ExpandoObjectConverter` — JSON converter (EtlKit.Kafka)
- [ ] `RestMethodInfo` — REST method metadata (EtlKit.Rest)
- [ ] `PublicationAddress` — RabbitMQ address (EtlKit.RabbitMq)
- [ ] `ExpandoObjectConverter` — JSON converter (EtlKit.AI)
- [ ] `CustomLiquidFilters` — Liquid template filters (EtlKit.AI)

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
