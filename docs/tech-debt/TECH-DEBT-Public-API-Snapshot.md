# Tech Debt: Public API Snapshot and Review

**Status:** Open
**Created:** 2026-09-07
**Priority:** Medium

## Problem

The public API surface of the published NuGet packages is not tracked in any machine-checked way.

- The `[PublicAPI]` attribute (JetBrains.Annotations, ~200 usages) is only an IDE hint for
  ReSharper/Rider. It does not detect or prevent breaking changes.
- There is no `Microsoft.CodeAnalysis.PublicApiAnalyzers`, no package validation
  (`EnablePackageValidation` / ApiCompat), and no API snapshot test.
- The GitLab pipeline only builds, packs, tests and publishes. An accidental change of a public
  signature, removal of a member, or a type that leaks into the public surface goes unnoticed until
  a consumer breaks.
- 15 packable libraries are published to nuget.org, so the audience for breaking changes is
  external and cannot be fixed by a coordinated internal update.

Related: [XML Documentation Coverage](TECH-DEBT-XML-Documentation-Coverage.md) already notes that
some of the 249 public types are probably not intended to be public. A committed API snapshot gives
a concrete list to review.

## Proposed Solution

Adopt the approach described in
[Generate and review the public API of a .NET library](https://www.meziantou.net/generate-and-review-the-public-api-of-a-dotnet-library.htm):
generate a C# snapshot of each library's public API at build time, commit it next to the sources,
and fail CI if the snapshot is out of date.

Tooling: `Meziantou.Framework.PublicApiGenerator.MSBuild` (NuGet, 2.0.x). It produces a
`PublicApi.g.cs` file with all public types and members, nullable annotations and consumer-facing
attributes, method bodies replaced by `throw null`. The file is ordinary C# and shows up in merge
request diffs like any other code change.

### Why this instead of PublicApiAnalyzers

- No hand-maintained `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` per project. The
  snapshot is fully generated; the developer only re-runs the build and commits the result.
- One-line CI verification (`-p:PublicApiGeneratorVerifyNoChangeOnBuild=true`), no extra job.
- Output is readable C#, easier to review than the analyzer's line-per-symbol text format.

### Limitations

- Detects *any* API change, not whether a change is breaking. Semantic compatibility with the
  previously published package is a separate concern (see Phase 3).
- Today each library builds a single TFM (`netstandard2.0`, `netstandard2.1` for ClickHouse,
  `net6.0` for MongoDB), so multi-TFM merging is not needed yet. If
  [Multi-Targeting](TECH-DEBT-Multi-Targeting.md) lands first, the generator merges per-TFM
  assemblies into one file with `#if NETx` blocks, so the plan still applies.

## Implementation Plan

### Phase 1: Generate snapshots

- [ ] Add `Meziantou.Framework.PublicApiGenerator.MSBuild` as a `PackageReference` with
      `PrivateAssets="all"` for non-test projects in `Directory.Build.props`.
- [ ] In `Directory.Build.targets` for non-test projects set:
  - `PublicApiGeneratorOutputPath` = `$(MSBuildProjectDirectory)/ref/PublicApi.g.cs`
  - `PublicApiGeneratorGenerateOnBuild` = `true` when `IsCI` is not `true`
  - `PublicApiGeneratorVerifyNoChangeOnBuild` = `true` when `IsCI` is `true`
- [ ] Build the solution once and commit `ref/PublicApi.g.cs` for all 15 packable projects:
      EtlKit, EtlKit.AI, EtlKit.ClickHouse, EtlKit.Common, EtlKit.DynamicLinq, EtlKit.Json,
      EtlKit.Kafka, EtlKit.Logging.Database, EtlKit.MongoDB, EtlKit.PostgresStreaming,
      EtlKit.Primitives, EtlKit.RabbitMq, EtlKit.Rest, EtlKit.Scripting, EtlKit.Serialization.
- [ ] Exclude `ref/**` from CSharpier formatting and from spell checking (`WeCantSpell.Roslyn`) so
      generated files do not produce warnings.
- [ ] Confirm `build_job` and `test_job` in `.gitlab-ci.yml` fail when a snapshot is stale.

### Phase 2: Audit the surface

- [ ] Review the generated snapshots for types that should not be public (helpers, internal
      models, test hooks). Make them `internal` or mark with
      `[EditorBrowsable(EditorBrowsableState.Never)]`.
- [ ] Reconcile with the `[PublicAPI]` attribute usage; decide whether the attribute is still
      needed once snapshots exist.
- [ ] Document the workflow for contributors in `CONTRIBUTING`/README: "if your MR changes
      `ref/PublicApi.g.cs`, explain the API change in the description".

### Phase 3 (optional): Package validation

- [ ] Enable `EnablePackageValidation` with `PackageValidationBaselineVersion` set to the last
      published stable version, so the SDK ApiCompat check reports actual breaking changes (not just
      any change) at `dotnet pack` time.
- [ ] Decide which breaking-change categories are suppressed for the 1.x line and record them in
      `CompatibilitySuppressions.xml`.

## Acceptance Criteria

- Every packable project has a committed `ref/PublicApi.g.cs`.
- A local `dotnet build` regenerates the snapshot; the CI build fails if the committed snapshot
  differs from the built assembly.
- Merge requests that change the public API show the change in the diff of `ref/PublicApi.g.cs`.

## References

- https://www.meziantou.net/generate-and-review-the-public-api-of-a-dotnet-library.htm
- https://www.nuget.org/packages/Meziantou.Framework.PublicApiGenerator.MSBuild
- https://learn.microsoft.com/dotnet/fundamentals/apicompat/package-validation/overview
