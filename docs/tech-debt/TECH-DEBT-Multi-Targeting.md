# Tech Debt: multi-target EtlKit packages instead of netstandard2.0-only

## Context

Every shipped EtlKit package (except `EtlKit.MongoDB`, retargeted to `net6.0` in 1.19.0) builds a
single `netstandard2.0` binary. That binary is compiled against the dependency graph NuGet resolves
**for netstandard2.0** — but consumers restore the graph **for their own TFM**. NuGet selects each
package's dependency group by the consuming application's target framework, uniformly across the
whole transitive graph; the TFMs of intermediate packages play no role.

The two graphs are not the same. Packages routinely declare TFM-conditional dependencies — for
example `Npgsql 8.0.3` requires `System.Collections.Immutable >= 8.0.0` in its
netstandard2.0/2.1 groups only, because on net6.0+ the assembly ships inside the shared framework.
So a compile-time reference baked into our netstandard2.0 binary can be a dangling edge in the
graph a net6.0+ consumer restores.

Nothing catches this at build or restore time on either side. It surfaces as a runtime
`FileNotFoundException` when the lazily-loaded code path finally executes.

## Problem

Concrete instance — RSSL-11885 (production, PSB dev):

- `EtlKit.Scripting` (then `EtlBox.Classic.Scripting 1.20.x`) was compiled against
  `System.Collections.Immutable, Version=8.0.0.0` — elevated transitively by Npgsql's
  netstandard2.0 dependency group via the `EtlKit` ProjectReference.
- Its only declared scripting dependency, `Microsoft.CodeAnalysis.CSharp.Scripting 4.8.0`,
  guarantees just `SCI >= 7.0.0`.
- A net6.0 consumer (`RapidSoft.Loyalty.BankConnector.Etl`) restored the same chain through
  Npgsql's **net6.0** group — no `SCI >= 8.0.0` edge anywhere — and deployed SCI 7.0.0.
- The .NET binder cannot satisfy a request for 8.0.0.0 with 7.0.0.0:
  `FileNotFoundException` inside `ScriptBuilder.CreateCore`, thrown only when a
  `ScriptedTransformation` actually runs, silently dropping the pipeline step's output.

The point fix (same MR that adds this document) bumps MSCA to 4.9.2, which declares
`System.Collections.Immutable >= 8.0.0` and `System.Reflection.Metadata >= 8.0.0` in **all** its
dependency groups, so the requirement reaches consumers on every TFM.

The class of problem remains. The 1.20.1 binary also carries elevated references to
`Microsoft.Extensions.Logging.Abstractions 8.0.0.0` and
`Microsoft.Extensions.DependencyInjection.Abstractions 8.0.0.0` while the csproj declares 7.0.1 /
8.0.0 — those happen to resolve compatibly today, but each dependency bump can mint a new mismatch
that no build catches.

## Direction

Multi-target the shipped packages: `netstandard2.0;net6.0;net8.0`.

- Each TFM's binary is then compiled against the same dependency graph its consumers restore —
  the net6.0 asset of `EtlKit.Scripting` would reference the SCI version the net6.0 graph
  actually provides, structurally eliminating the mismatch for modern consumers.
- `netstandard2.0` stays for .NET Framework / legacy consumers; the mismatch risk stays confined
  to that audience.
- Precedent in-repo: `EtlKit.MongoDB` is already net6.0-only (MongoDB.Driver 3.x dropped ns2.0).

Costs to weigh:

- CI build/test matrix grows (test each shipped TFM, not just net8.0 test projects).
- Some `PackageReference`s become TFM-conditional (`Condition="'$(TargetFramework)' == …"`).
- Package size roughly doubles per added TFM.

## Interim rule (until multi-targeting lands)

Any assembly reference the compiler bakes into the netstandard2.0 binary must be guaranteed by
declared dependencies **on every consumer TFM** — either as a direct `PackageReference` (it then
lands in the nuspec) or via a dependency that declares it for all TFMs (as MSCA 4.9.2 does for
SCI/SRM). When bumping a dependency of `EtlKit`/`EtlKit.Common`, re-check what the Scripting and
Serialization binaries actually reference (`System.Reflection.Metadata` `PEReader` one-liner or
`ildasm`) against what the nuspec chain guarantees.

Consumer-side, the Loyalty repo tracks a CI publish-output binding check (RSSL tech-debt task,
linked from RSSL-11885) that catches this class of error for any package, not just EtlKit.
