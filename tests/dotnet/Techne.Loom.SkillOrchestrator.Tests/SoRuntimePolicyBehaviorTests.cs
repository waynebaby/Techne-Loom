using System.Text.Json;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class SoRuntimePolicyBehaviorTests
{
    [Fact]
    public void PackageLock_RecordsExactRidPackageValidationPolicy()
    {
        var repoRoot = FindRepositoryRoot();
        var skillRoot = Path.Combine(repoRoot, ".agents", "skills", "loom-skill-enhancement");
        var lockPath = Path.Combine(skillRoot, "assets", "so-workflow", "so-package-lock.json");
        var lockText = File.ReadAllText(lockPath);
        using var lockDocument = JsonDocument.Parse(lockText);
        var root = lockDocument.RootElement;
        var version = root.GetProperty("resolved_version").GetString();
        var restore = root.GetProperty("runtime_restore");

        var skillText = File.ReadAllText(Path.Combine(skillRoot, "SKILL.md"));
        var versionBlockMatch = System.Text.RegularExpressions.Regex.Match(skillText, @"Current published SO package runtime version: `([^`]+)`");
        Assert.True(versionBlockMatch.Success, "The loom-skill-enhancement skill package version block was not found.");
        Assert.Equal(versionBlockMatch.Groups[1].Value, version);
        Assert.Matches(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$", version);
        Assert.Equal("nuget-registration", restore.GetProperty("source").GetString());
        Assert.Equal("same-version-github-release-asset", restore.GetProperty("fallback_source").GetString());
        Assert.Equal("standard-nuget-cache-exact-package-first", restore.GetProperty("cache_policy").GetString());
        Assert.True(restore.GetProperty("reuse_exact_package_when_valid").GetBoolean());
        Assert.True(restore.GetProperty("download_exact_locked_package_when_missing_or_invalid").GetBoolean());
        Assert.True(restore.GetProperty("never_float_to_latest").GetBoolean());
        Assert.False(restore.GetProperty("create_loom_specific_cache").GetBoolean());

        var requiredChecks = restore.GetProperty("required_package_validation").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains("package_id_matches", requiredChecks);
        Assert.Contains("rid_matches", requiredChecks);
        Assert.Contains("registration_sha512_or_github_sidecar_matches", requiredChecks);
        Assert.Contains("runtime_manifest_matches", requiredChecks);
        Assert.Contains("archive_paths_and_sizes_are_safe", requiredChecks);
        Assert.Contains("apphost_and_english_guide_are_present", requiredChecks);
        Assert.False(root.TryGetProperty("package_id", out _));
        Assert.False(root.TryGetProperty("channel", out _));
        Assert.False(root.TryGetProperty("runtime_bundle", out _));
        Assert.DoesNotContain("dotnet so.dll", lockText, StringComparison.Ordinal);
        Assert.DoesNotContain("complete_dotnet_cli_runtime_bundle", lockText, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeBootstrap_UsesDirectGuideAndNoCheckedInRestoreHelper()
    {
        var repoRoot = FindRepositoryRoot();
        var skillRoot = Path.Combine(repoRoot, ".agents", "skills", "loom-skill-enhancement");
        var workflowRoot = Path.Combine(skillRoot, "assets", "so-workflow");
        var skillText = File.ReadAllText(Path.Combine(skillRoot, "SKILL.md"));
        var templatePath = Path.Combine(workflowRoot, "so-template.json");
        var templateText = File.ReadAllText(templatePath);
        using var workflowDocument = JsonDocument.Parse(templateText);
        var nodes = workflowDocument.RootElement.GetProperty("nodes");

        Assert.False(File.Exists(Path.Combine(workflowRoot, "restore-so-runtime.ps1")));
        Assert.False(File.Exists(Path.Combine(workflowRoot, "scripts", "restore-so-runtime.js")));
        Assert.Contains("so.exe --guide", skillText, StringComparison.Ordinal);
        Assert.Contains("so --guide", skillText, StringComparison.Ordinal);
        Assert.False(nodes.TryGetProperty("state.mcp_preflight", out _));
        Assert.False(nodes.TryGetProperty("transition.start_mcp", out _));
        Assert.Equal("state.capture_guide", nodes.GetProperty("transition.reacquire_runtime").GetProperty("targetNodeId").GetString());
        Assert.Contains("so.exe --guide", nodes.GetProperty("transition.capture_guide").GetProperty("command").GetProperty("parameters").GetProperty("command").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("gate.bootstrap_mcp_ready", templateText, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime_launch_descriptor", templateText, StringComparison.Ordinal);
        var skillMarkdownReviewer = File.ReadAllText(Path.Combine(skillRoot, "assets", "agents", "loom-skill-enhancement-skill-markdown-gap-review.agent.md"));
        Assert.DoesNotContain("runtime descriptor", skillMarkdownReviewer, StringComparison.Ordinal);
        Assert.Contains("exact published SO apphost", skillMarkdownReviewer, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet so.dll", templateText, StringComparison.Ordinal);
        var buildContextTransition = nodes.GetProperty("transition.build_shared_review_context");
        Assert.Equal("context.Get<object?>(\"shared_review_context\") != null", buildContextTransition.GetProperty("succeedExpression").GetString());

        var contractPath = Path.Combine(workflowRoot, "contract.json");
        using var contractDocument = JsonDocument.Parse(File.ReadAllText(contractPath));
        var guideRefreshEvidence = contractDocument.RootElement.GetProperty("outputs").GetProperty("guide_refresh_evidence").GetString() ?? string.Empty;
        Assert.Contains("first runtime operation, --guide", guideRefreshEvidence, StringComparison.Ordinal);
        Assert.Contains("MCP registration is optional after guide capture", guideRefreshEvidence, StringComparison.Ordinal);
        Assert.DoesNotContain("governance-entry transport proof", guideRefreshEvidence, StringComparison.Ordinal);

        var sharedContextDescription = nodes.GetProperty("transition.enter_shared_review_context").GetProperty("description").GetString() ?? string.Empty;
        Assert.Contains("after fresh direct-apphost guide evidence exists", sharedContextDescription, StringComparison.Ordinal);
        Assert.DoesNotContain("MCP", sharedContextDescription, StringComparison.Ordinal);

        var reviewEvidenceContract = File.ReadAllText(Path.Combine(skillRoot, "reference", "review-and-evidence-contract.md"));
        Assert.Contains("after exact package validation and fresh direct-apphost guide capture", reviewEvidenceContract, StringComparison.Ordinal);
        Assert.DoesNotContain("governance-entry transport proof", reviewEvidenceContract, StringComparison.Ordinal);
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
