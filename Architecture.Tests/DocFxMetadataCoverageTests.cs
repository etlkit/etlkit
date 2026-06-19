using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace ETLBox.Architecture.Tests;

/// <summary>
/// Regression tests for <c>docfx/docfx.json</c> and the GitHub Actions workflow
/// that publishes the API reference to <c>https://rpsft.github.io/etlbox/</c>.
///
/// These tests would have caught the 1.19.0 release gap where
/// <c>ETLBox.MongoDB</c>, <c>ETLBox.DynamicLinq</c>, and
/// <c>ETLBox.PostgresStreaming</c> were missing from <c>docfx.json</c> — the
/// public docs site had no pages for them — and the multi-TFM mismatch where
/// <c>ETLBox.ClickHouse</c> (netstandard2.1) referenced <c>ETLBox.Common</c> /
/// <c>ETLBox.Primitives</c> (netstandard2.0) without a matching metadata
/// reference, emitting DocFx warnings on every build.
/// </summary>
public class DocFxMetadataCoverageTests
{
    private static readonly string RepoRoot = LocateRepoRoot();
    private static readonly string DocFxConfigPath = Path.Combine(RepoRoot, "docfx", "docfx.json");
    private static readonly string DocsWorkflowPath = Path.Combine(
        RepoRoot,
        ".github",
        "workflows",
        "docs.yml"
    );

    [Fact]
    public void Every_ETLBox_source_project_is_documented_in_docfx_metadata()
    {
        var documented = LoadDocumentedProjects();
        var actual = EnumerateSourceProjects();

        var missing = actual.Where(p => !documented.Contains(p)).ToList();

        Assert.True(
            missing.Count == 0,
            $"Source projects missing from docfx/docfx.json metadata:{Environment.NewLine}"
                + $"  {string.Join(Environment.NewLine + "  ", missing)}{Environment.NewLine}"
                + "Add each missing project to docfx/docfx.json under a metadata block whose "
                + "TargetFramework matches the project's actual <TargetFramework>."
        );
    }

    [Fact]
    public void Every_documented_project_is_compatible_with_its_metadata_block_TargetFramework()
    {
        // Why this isn't strict equality: a project may sit in a block whose TFM differs from its
        // own as long as its own TFM is consumable by the block's (netstandard2.0 is consumable by
        // netstandard2.1 and net5.0+, etc.). This check verifies consumability only. Note that
        // DocFx additionally *compiles* each project under the block's forced TargetFramework, so a
        // netstandard2.0-only project still cannot live in a net6.0 / netstandard2.1 block without
        // breaking the metadata build — that buildability rule is enforced by
        // Common_and_Primitives_are_present_in_every_compatible_metadata_block.
        var blocks = LoadMetadataBlocks();
        var mismatches = new List<string>();

        foreach (var (tfm, projects) in blocks)
        {
            foreach (var projectPath in projects)
            {
                var absPath = Path.Combine(
                    RepoRoot,
                    projectPath.Replace('/', Path.DirectorySeparatorChar)
                );
                if (!File.Exists(absPath))
                {
                    mismatches.Add(
                        $"{projectPath} (referenced in '{tfm}' block) does not exist on disk"
                    );
                    continue;
                }

                var projectTfms = LoadProjectTargetFrameworks(absPath);
                if (!projectTfms.Any(p => IsTfmConsumableBy(p, tfm)))
                {
                    mismatches.Add(
                        $"{projectPath} is in the '{tfm}' metadata block but none of its "
                            + $"TargetFramework(s) [{string.Join(", ", projectTfms)}] are consumable by '{tfm}'"
                    );
                }
            }
        }

        Assert.True(
            mismatches.Count == 0,
            $"docfx/docfx.json TargetFramework compatibility mismatches:{Environment.NewLine}"
                + $"  {string.Join(Environment.NewLine + "  ", mismatches)}"
        );
    }

    /// <summary>
    /// True if a project targeting <paramref name="projectTfm"/> can be consumed by a
    /// project targeting <paramref name="blockTfm"/>. Mirrors the (subset of) .NET TFM
    /// compatibility matrix that's relevant for this repo.
    /// </summary>
    private static bool IsTfmConsumableBy(string projectTfm, string blockTfm)
    {
        if (projectTfm.Equals(blockTfm, StringComparison.OrdinalIgnoreCase))
            return true;

        // netstandard2.0 is consumed by netstandard2.1 and net5.0+
        if (projectTfm.Equals("netstandard2.0", StringComparison.OrdinalIgnoreCase))
            return blockTfm.Equals("netstandard2.1", StringComparison.OrdinalIgnoreCase)
                || IsNet5OrHigher(blockTfm);

        // netstandard2.1 is consumed by net5.0+
        if (projectTfm.Equals("netstandard2.1", StringComparison.OrdinalIgnoreCase))
            return IsNet5OrHigher(blockTfm);

        // netN.0 → netM.0 for M >= N
        var projMajor = ParseNetMajor(projectTfm);
        var blockMajor = ParseNetMajor(blockTfm);
        return projMajor.HasValue && blockMajor.HasValue && blockMajor >= projMajor;
    }

    private static bool IsNet5OrHigher(string tfm) => ParseNetMajor(tfm) is >= 5;

    private static int? ParseNetMajor(string tfm)
    {
        var match = Regex.Match(tfm, @"^net(\d+)\.\d+$", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    [Fact]
    public void Common_and_Primitives_are_present_in_every_compatible_metadata_block()
    {
        // ETLBox.Common and ETLBox.Primitives are transitive dependencies of nearly every
        // ETLBox package via <ProjectReference>. Ideally they'd appear in every block so DocFx
        // never emits `Found project reference without a matching metadata reference`. But DocFx
        // forces the block's TargetFramework onto *every* project in it, and these two are
        // netstandard2.0-only — listing them in the net6.0 / netstandard2.1 blocks makes DocFx
        // compile them under a TFM they don't declare, which fails the whole metadata build (the
        // 1.20.x pages breakage). So we only require them in a block whose TargetFramework is one
        // of their own TFMs (one DocFx can actually build them under); the cross-TFM reference
        // warnings for ClickHouse / MongoDB are accepted as the lesser evil.
        var blocks = LoadMetadataBlocks();
        var commonTfms = LoadProjectTargetFrameworks(
            Path.Combine(RepoRoot, "ETLBox.Common", "ETLBox.Common.csproj")
        );
        var primitivesTfms = LoadProjectTargetFrameworks(
            Path.Combine(RepoRoot, "ETLBox.Primitives", "ETLBox.Primitives.csproj")
        );
        var offenders = new List<string>();

        foreach (var (tfm, projects) in blocks)
        {
            var referencers = projects.Where(p => ProjectReferencesCommonOrPrimitives(p)).ToList();
            if (referencers.Count == 0)
                continue;

            if (
                commonTfms.Contains(tfm) && !projects.Contains("ETLBox.Common/ETLBox.Common.csproj")
            )
                offenders.Add(
                    $"'{tfm}' block: ETLBox.Common is missing but is referenced by [{string.Join(", ", referencers)}]"
                );
            if (
                primitivesTfms.Contains(tfm)
                && !projects.Contains("ETLBox.Primitives/ETLBox.Primitives.csproj")
            )
                offenders.Add(
                    $"'{tfm}' block: ETLBox.Primitives is missing but is referenced by [{string.Join(", ", referencers)}]"
                );
        }

        Assert.True(
            offenders.Count == 0,
            $"docfx/docfx.json: Common/Primitives missing from a block whose TargetFramework "
                + $"they natively build under:{Environment.NewLine}"
                + $"  {string.Join(Environment.NewLine + "  ", offenders)}"
        );
    }

    [Fact]
    public void GitHub_Pages_workflow_path_filter_covers_every_documented_project()
    {
        if (!File.Exists(DocsWorkflowPath))
        {
            // If the workflow doesn't exist on this branch, skip — separate concern.
            return;
        }

        var workflow = File.ReadAllText(DocsWorkflowPath);
        var pathFilter = ExtractPathFilter(workflow);

        var documented = LoadDocumentedProjects();
        var documentedDirs = documented.Select(p => p.Split('/')[0]).Distinct().ToList();

        var missing = documentedDirs.Where(d => !pathFilter.Contains($"{d}/**")).ToList();

        Assert.True(
            missing.Count == 0,
            $"Projects documented in docfx.json but missing from .github/workflows/docs.yml "
                + $"path filter (master pushes touching only these projects won't trigger a docs "
                + $"rebuild):{Environment.NewLine}"
                + $"  {string.Join(Environment.NewLine + "  ", missing.Select(d => $"'{d}/**'"))}"
        );
    }

    // --- helpers ---

    private static HashSet<string> LoadDocumentedProjects() =>
        LoadMetadataBlocks().SelectMany(b => b.Projects).ToHashSet();

    private static List<(string Tfm, List<string> Projects)> LoadMetadataBlocks()
    {
        var json =
            JsonNode.Parse(File.ReadAllText(DocFxConfigPath))
            ?? throw new InvalidOperationException("docfx.json is not valid JSON");
        var blocks = new List<(string Tfm, List<string> Projects)>();

        foreach (var block in json["metadata"]!.AsArray())
        {
            var tfm = block!["properties"]?["TargetFramework"]?.GetValue<string>() ?? "";
            var files = block["src"]!
                .AsArray()
                .SelectMany(src => src!["files"]!.AsArray())
                .Select(f => f!.GetValue<string>())
                .ToList();
            blocks.Add((tfm, files));
        }
        return blocks;
    }

    private static List<string> EnumerateSourceProjects()
    {
        return Directory
            .GetDirectories(RepoRoot)
            .Select(Path.GetFileName)
            .Where(d => d is not null && d.StartsWith("ETLBox", StringComparison.Ordinal))
            .Where(d => !d!.EndsWith(".Tests", StringComparison.Ordinal))
            .Where(d => !d!.EndsWith(".Benchmarks", StringComparison.Ordinal))
            // sanity check the directory actually contains a matching .csproj
            .Where(d => File.Exists(Path.Combine(RepoRoot, d!, $"{d}.csproj")))
            .Select(d => $"{d}/{d}.csproj")
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }

    private static HashSet<string> LoadProjectTargetFrameworks(string absoluteCsprojPath)
    {
        var doc = XDocument.Load(absoluteCsprojPath);
        var values = doc.Descendants("TargetFramework")
            .Concat(doc.Descendants("TargetFrameworks"))
            .Select(e => e.Value);

        return values
            .SelectMany(v =>
                v.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            )
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool ProjectReferencesCommonOrPrimitives(string projectRepoRelativePath)
    {
        var absPath = Path.Combine(
            RepoRoot,
            projectRepoRelativePath.Replace('/', Path.DirectorySeparatorChar)
        );
        if (!File.Exists(absPath))
            return false;

        // Common and Primitives don't reference themselves — skip.
        var dir = Path.GetFileName(Path.GetDirectoryName(absPath));
        if (dir is "ETLBox.Common" or "ETLBox.Primitives")
            return false;

        var doc = XDocument.Load(absPath);
        foreach (var pr in doc.Descendants("ProjectReference"))
        {
            var include = pr.Attribute("Include")?.Value ?? "";
            if (
                include.Contains("ETLBox.Common", StringComparison.Ordinal)
                || include.Contains("ETLBox.Primitives", StringComparison.Ordinal)
            )
                return true;
        }
        return false;
    }

    private static HashSet<string> ExtractPathFilter(string workflowYaml)
    {
        // Cheap YAML peek — we only need the `paths:` list under the `push:` trigger.
        // Look for the block of indented `- '<glob>'` lines following `paths:`.
        var match = Regex.Match(
            workflowYaml,
            @"paths:\s*\n((?:\s*-\s*'[^']+'\s*\n)+)",
            RegexOptions.Multiline
        );
        if (!match.Success)
            return [];

        return Regex
            .Matches(match.Groups[1].Value, @"'([^']+)'")
            .Select(m => m.Groups[1].Value)
            .ToHashSet();
    }

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ETLBox.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            $"Could not locate repo root (looking for ETLBox.sln) walking up from {AppContext.BaseDirectory}"
        );
    }
}
