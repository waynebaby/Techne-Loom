using System.Text.Json;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class SoRuntimePolicyBehaviorTests
{
    [Fact]
    public void PackageLock_RecordsOnlyExactRuntimeVersion()
    {
        var repoRoot = FindRepositoryRoot();
        var lockPath = Path.Combine(repoRoot, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "so-package-lock.json");
        var lockText = File.ReadAllText(lockPath);
        using var lockDocument = JsonDocument.Parse(lockText);
        var root = lockDocument.RootElement;
        var version = root.GetProperty("resolved_version").GetString();
        var restore = root.GetProperty("runtime_restore");

        var skillText = File.ReadAllText(Path.Combine(repoRoot, ".agents", "skills", "loom-skill-enhancement", "SKILL.md"));
        var versionBlockMatch = System.Text.RegularExpressions.Regex.Match(skillText, @"Current published SO package runtime version: `([^`]+)`");
        Assert.True(versionBlockMatch.Success, "The loom-skill-enhancement skill package version block was not found.");
        Assert.Equal(versionBlockMatch.Groups[1].Value, version);
        Assert.Matches(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$", version);
        Assert.Equal("exact-version-first", restore.GetProperty("cache_policy").GetString());
        Assert.True(restore.GetProperty("reuse_exact_local_bundle_when_valid").GetBoolean());
        Assert.True(restore.GetProperty("download_exact_locked_version_when_missing_or_invalid").GetBoolean());
        Assert.True(restore.GetProperty("never_float_to_latest").GetBoolean());
        Assert.False(root.TryGetProperty("package_id", out _));
        Assert.False(root.TryGetProperty("channel", out _));
        Assert.False(root.TryGetProperty("runtime_bundle", out _));
        Assert.False(root.TryGetProperty("self_contained_runtime_bundle", out _));
        Assert.DoesNotContain("fresh_download", lockText, StringComparison.Ordinal);
        Assert.DoesNotContain("allow_local_cache_when_exact_version_matches", lockText, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeRestoreHelper_UsesSelfContainedDefaultAndNodeZipValidation()
    {
        var repoRoot = FindRepositoryRoot();
        var scriptPath = Path.Combine(repoRoot, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "restore-so-runtime.ps1");
        var nodePath = Path.Combine(repoRoot, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "scripts", "restore-so-runtime.js");
        var script = File.ReadAllText(scriptPath);
        var nodeScript = File.ReadAllText(nodePath);

        Assert.Contains("scripts\\restore-so-runtime.js", script, StringComparison.Ordinal);
        Assert.Contains("--mode", script, StringComparison.Ordinal);
        Assert.Contains("self-contained", script, StringComparison.Ordinal);
        Assert.Contains("dotnet-cli", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Add-Type", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ZipArchive", script, StringComparison.Ordinal);
        Assert.Contains("--mode", nodeScript, StringComparison.Ordinal);
        Assert.Contains("self-contained", nodeScript, StringComparison.Ordinal);
        Assert.Contains("dotnet-cli", nodeScript, StringComparison.Ordinal);
        Assert.Contains("never_float_to_latest", File.ReadAllText(Path.Combine(repoRoot, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "so-package-lock.json")), StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Techne.Loom.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
