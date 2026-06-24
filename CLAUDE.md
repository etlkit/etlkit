# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this
repository.

## Project Overview

EtlKit (NuGet: `EtlKit`) is an open-source .NET ETL library for building data
integration pipelines. Fork of original ETLBox by Andreas Lennartz. Targets
`netstandard2.0` (libraries) and `net8.0` (tests).

## Build & Test Commands

```bash
# Build entire solution
dotnet build EtlKit.sln

# Build a specific project
dotnet build EtlKit/EtlKit.csproj

# Run all tests (requires Docker databases running)
dotnet test EtlKit.sln

# Run a specific test project
dotnet test TestTransformations/TestTransformations.csproj

# Run a single test by fully qualified name
dotnet test TestTransformations/TestTransformations.csproj --filter "FullyQualifiedName~RowTransformationTests"

# Run tests excluding performance
dotnet test --filter "Category!=Performance"

# Setup test environment (Windows)
pwsh ./test/Set-Configuration.ps1 -configEnvironment localhost-win
pwsh ./test/Run-Containers.ps1
```

Test databases (Docker): SQL Server (:1433, sa/YourStrong@Passw0rd), PostgreSQL (:5432,
postgres/etlkitpassword), MySQL (:3306, root/etlkitpassword), ClickHouse (:9000).

## Architecture

Two main subsystems inside `EtlKit/src/`:

- **Data Flow** (`Toolbox/DataFlow/`) - Sources, transformations, destinations connected via
  `LinkTo()` pattern. Built on TPL Dataflow (`ActionBlock`, `BatchBlock`). Async-first with
  `Task`-based APIs.
- **Control Flow** (`Toolbox/ControlFlow/`) - DDL/DML tasks for database management (e.g.,
  `CreateTableTask.Create()`).

Key abstractions in `Definitions/`:

- `DataFlowSource<T>` / `DataFlowTransformation<T>` / `DataFlowDestination<T>` - base classes for
  pipeline components
- `IConnectionManager` - database connection abstraction (`Toolbox/ConnectionManager/`)
- `ITask` / `GenericTask` - base for all tasks

Root namespace: `EtlKit` with sub-namespaces (`EtlKit.DataFlow`, `EtlKit.ControlFlow`,
etc.).

Extension libraries follow the pattern `EtlKit.<Feature>/` (e.g., EtlKit.Kafka, EtlKit.Json,
EtlKit.Rest, EtlKit.Serialization, EtlKit.Scripting, EtlKit.DynamicLinq) with corresponding
`EtlKit.<Feature>.Tests/` projects.

Shared test utilities live in `TestShared/`. Tests that cannot run in parallel are in
`TestNonParallel/`.

## Commit Conventions

Conventional Commits enforced by Husky pre-commit hook. Format: `type(scope): subject`

Types: `build|feat|ci|chore|docs|fix|perf|refactor|revert|style|test`

Rules:

- 1-90 characters total
- Subject at least 4 characters after type/scope
- No trailing period or whitespace
- Hook auto-appends `Changelog:` trailer (feat->added, fix->fixed, perf->performance, other->other)

## Language

All written content in this project must be in **English**: documentation, changelog entries, commit
messages, and code comments.

## Testing

- **Do NOT use FluentAssertions.** Use xUnit's built-in `Assert.*` methods instead.

## Code Quality

- Warnings treated as errors (except CS0618, CS1574)
- SonarAnalyzer.CSharp and WeCantSpell.Roslyn spell-checking enabled
- XML documentation required for non-test projects
- C# language version 12
- `[PublicAPI]` attribute (JetBrains.Annotations) marks public API surface
- Custom dictionary at `.directory.dic` for spell checker

## Project Tracking

- `TODO.md` tracks open work items only. Completed items must be **moved** to `docs/changelog/`, not
  left in TODO with strikethrough.
- Tech debt plans live in `docs/tech-debt/` as detailed markdown documents, referenced from TODO.md.

## Versioning

Version comes from `.version.yml` (`PACKAGE_RELEASE` + `PACKAGE_POSTFIX`). Local builds without this
file default to `1.0.0-development`.
