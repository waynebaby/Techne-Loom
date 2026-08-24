using System.Text.Json;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class SoRuntimePolicyBehaviorTests
{
    [Fact]
    public void PackageLock_RequiresExactCacheFirstThreePackageBundle()
    {
        var repoRoot = FindRepositoryRoot();
        var lockPath = Path.Combine(repoRoot, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "so-package-lock.json");
        var lockText = File.ReadAllText(lockPath);
        using var lockDocument = JsonDocument.Parse(lockText);
        var root = lockDocument.RootElement;
        var version = root.GetProperty("resolved_version").GetString();
        var restore = root.GetProperty("runtime_restore");
        var bundle = root.GetProperty("runtime_bundle").EnumerateArray().ToArray();

        Assert.Equal("0.3.231-beta", version);
        Assert.Equal("exact-version-first", restore.GetProperty("cache_policy").GetString());
        Assert.True(restore.GetProperty("reuse_exact_local_bundle_when_valid").GetBoolean());
        Assert.True(restore.GetProperty("download_exact_locked_version_when_missing_or_invalid").GetBoolean());
        Assert.True(restore.GetProperty("never_float_to_latest").GetBoolean());
        Assert.Equal(3, bundle.Length);
        Assert.All(bundle, member => Assert.Equal(version, member.GetProperty("resolved_version").GetString()));
        Assert.DoesNotContain("fresh_download", lockText, StringComparison.Ordinal);
        Assert.DoesNotContain("allow_local_cache_when_exact_version_matches", lockText, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeRestoreHelper_UsesExactVersionAndPowerShellZipSafeProbe()
    {
        var repoRoot = FindRepositoryRoot();
        var scriptPath = Path.Combine(repoRoot, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "restore-so-runtime.ps1");
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("resolved_version", script, StringComparison.Ordinal);
        Assert.Contains("runtime_bundle", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-WebRequest -UseBasicParsing", script, StringComparison.Ordinal);
        Assert.Contains("ZipArchive]::new", script, StringComparison.Ordinal);
        Assert.DoesNotContain("latest.nupkg", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api/v3/index.json", script, StringComparison.OrdinalIgnoreCase);
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
