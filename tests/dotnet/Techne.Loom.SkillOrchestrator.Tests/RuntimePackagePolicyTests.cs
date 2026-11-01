using Techne.Loom.Common.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class RuntimePackagePolicyTests
{
    [Fact]
    public void RuntimeProjects_UseRidSpecificSelfContainedSingleFilePackaging()
    {
        var repoRoot = FindRepositoryRoot();
        var target = File.ReadAllText(Path.Combine(repoRoot, "src", "dotnet", "Techne.Loom.RuntimePackage.targets"));
        var commonProject = File.ReadAllText(Path.Combine(repoRoot, "src", "dotnet", "Techne.Loom.Common", "Techne.Loom.Common.csproj"));
        var aoProject = File.ReadAllText(Path.Combine(repoRoot, "src", "dotnet", "Techne.Loom.AgentOrchestrator.Runtime", "Techne.Loom.AgentOrchestrator.Runtime.csproj"));
        var soProject = File.ReadAllText(Path.Combine(repoRoot, "src", "dotnet", "Techne.Loom.SkillOrchestrator.Runtime", "Techne.Loom.SkillOrchestrator.Runtime.csproj"));

        Assert.Contains("PublishSingleFile=true", target, StringComparison.Ordinal);
        Assert.Contains("RuntimeDocsSourceRoot", target, StringComparison.Ordinal);
        Assert.Contains("TechneLoomDirectDocsForPackage", commonProject, StringComparison.Ordinal);
        Assert.Contains("Techne.Loom.DirectDocs.targets", commonProject, StringComparison.Ordinal);
        Assert.Contains("docs_root", target, StringComparison.Ordinal);
        Assert.Contains("guide_path", target, StringComparison.Ordinal);
        Assert.Contains("--self-contained true", target, StringComparison.Ordinal);
        Assert.Contains("IncludeAllContentForSelfExtract=true", target, StringComparison.Ordinal);
        Assert.Contains("TargetsForTfmSpecificContentInPackage", target, StringComparison.Ordinal);
        Assert.Contains("Techne.Loom.AgentOrchestrator.Runtime.$(RuntimeIdentifier)", aoProject, StringComparison.Ordinal);
        Assert.Contains("Techne.Loom.SkillOrchestrator.Runtime.$(RuntimeIdentifier)", soProject, StringComparison.Ordinal);
        Assert.Contains("RuntimeSourceProject", aoProject, StringComparison.Ordinal);
        Assert.Contains("RuntimeSourceProject", soProject, StringComparison.Ordinal);
    }

    [Fact]

    public void RuntimeProjects_DoNotEmbedGuidePages()

    {

        var repoRoot = FindRepositoryRoot();

        var aoProject = File.ReadAllText(Path.Combine(repoRoot, "src", "dotnet", "Techne.Loom.AgentOrchestrator", "Techne.Loom.AgentOrchestrator.csproj"));

        var soProject = File.ReadAllText(Path.Combine(repoRoot, "src", "dotnet", "Techne.Loom.SkillOrchestrator", "Techne.Loom.SkillOrchestrator.csproj"));

        var docsTarget = File.ReadAllText(Path.Combine(repoRoot, "src", "dotnet", "Techne.Loom.DirectDocs.targets"));



        Assert.DoesNotContain("Techne.Loom.DocsBundle.targets", aoProject, StringComparison.Ordinal);

        Assert.DoesNotContain("Techne.Loom.DocsBundle.targets", soProject, StringComparison.Ordinal);

        Assert.Contains("Techne.Loom.DirectDocs.targets", aoProject, StringComparison.Ordinal);

        Assert.Contains("Techne.Loom.DirectDocs.targets", soProject, StringComparison.Ordinal);

        Assert.Contains("TechneLoomDirectDocsForPackage", aoProject, StringComparison.Ordinal);

        Assert.Contains("TechneLoomDirectDocsForPackage", soProject, StringComparison.Ordinal);

        Assert.Contains("guides/(?:ao|so)-guide", docsTarget, StringComparison.Ordinal);

        Assert.False(File.Exists(Path.Combine(repoRoot, "src", "dotnet", "Techne.Loom.DocsBundle.targets")));

    }

    [Fact]
    public void PublishWorkflows_DeclareTheCompleteRuntimeMatrixAndAssetFlow()
    {
        var repoRoot = FindRepositoryRoot();
        foreach (var workflowName in new[] { "publish-main.yml", "publish-development.yml" })
        {
            var workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", workflowName)).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            Assert.Contains("paths-ignore:", workflow, StringComparison.Ordinal);
            Assert.Contains("'docs/en/articles/**'", workflow, StringComparison.Ordinal);
            Assert.Contains("'docs/zh-cn/articles/**'", workflow, StringComparison.Ordinal);
            Assert.Contains("'docs/assets/images/**'", workflow, StringComparison.Ordinal);
            Assert.Contains("'docs/assets/attachments/**'", workflow, StringComparison.Ordinal);
            Assert.Contains("  workflow_dispatch:", workflow, StringComparison.Ordinal);

            var runtimeJobStart = workflow.IndexOf("  runtime-packages:", StringComparison.Ordinal);
            var abstractionsJobStart = workflow.IndexOf("  abstractions-package:", StringComparison.Ordinal);
            var publishJobStart = workflow.IndexOf("  publish:", StringComparison.Ordinal);
            Assert.True(runtimeJobStart >= 0 && abstractionsJobStart > runtimeJobStart && publishJobStart > abstractionsJobStart);
            var runtimeJob = workflow[runtimeJobStart..abstractionsJobStart];
            var abstractionsJob = workflow[abstractionsJobStart..publishJobStart];
            Assert.Contains("strategy:\n      fail-fast: false\n      matrix:\n        rid:", runtimeJob, StringComparison.Ordinal);
            Assert.Contains("Techne.Loom.$product.Runtime.${{ matrix.rid }}.$PACKAGE_VERSION.nupkg", runtimeJob, StringComparison.Ordinal);
            Assert.Equal(8, LoomRuntimeCatalog.SupportedRuntimeIdentifiers.Count);
            Assert.All(LoomRuntimeCatalog.SupportedRuntimeIdentifiers, rid =>
                Assert.Contains($"- {rid}", runtimeJob, StringComparison.Ordinal));

            Assert.Contains("needs: version", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("PACKAGE_VERSION: ${{ needs.version.outputs.package_version }}", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("dotnet pack src/dotnet/Techne.Loom.Abstractions/Techne.Loom.Abstractions.csproj", abstractionsJob, StringComparison.Ordinal);
            Assert.Equal(1, abstractionsJob.Split('\n').Count(line => line.Contains("dotnet pack ", StringComparison.Ordinal)));
            Assert.DoesNotContain("RuntimeIdentifier", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("Techne.Loom.Abstractions.$PACKAGE_VERSION.nupkg", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("test -f \"$package_path.sha512\"", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("lib/net9.0/Techne.Loom.Abstractions.dll", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("tools/", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("runtimes/", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("implementation dependencies", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("child(dependencies, \"dependency\") is not None", abstractionsJob, StringComparison.Ordinal);
            Assert.DoesNotContain("list(dependencies)", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("name: abstractions-package", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("Prepare external Abstractions SDK consumer project", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("Build external consumer against the local Abstractions package", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("--source artifacts/nuget /p:AbstractionsPackageVersion=\"$PACKAGE_VERSION\"", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("<PackageReference Include=\"Techne.Loom.Abstractions\" Version=\"$(AbstractionsPackageVersion)\" />", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("var contract = new UserInputContract", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("JsonSerializer.Deserialize<UserInputContract>(JsonSerializer.Serialize(contract))", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("IStandaloneAskSessionService askService = new ConsumerAskService();", abstractionsJob, StringComparison.Ordinal);
            Assert.Contains("sealed class ConsumerAskService : IStandaloneAskSessionService", abstractionsJob, StringComparison.Ordinal);
            Assert.DoesNotContain("PackageReference Include=\"Techne.Loom.Common\"", abstractionsJob, StringComparison.Ordinal);
            Assert.DoesNotContain("PackageReference Include=\"Techne.Loom.AgentOrchestrator.Runtime", abstractionsJob, StringComparison.Ordinal);
            Assert.DoesNotContain("PackageReference Include=\"Techne.Loom.SkillOrchestrator.Runtime", abstractionsJob, StringComparison.Ordinal);

            Assert.Contains("needs: [version, runtime-packages, abstractions-package]", workflow, StringComparison.Ordinal);
            Assert.Contains("pattern: runtime-packages-*", workflow, StringComparison.Ordinal);
            Assert.Contains("pattern: abstractions-package", workflow, StringComparison.Ordinal);
            Assert.Contains("merge-multiple: true", workflow, StringComparison.Ordinal);
            Assert.Contains("Verify exact 17-package release artifact closure", workflow, StringComparison.Ordinal);
            Assert.Contains("Expected exactly 17 package files", workflow, StringComparison.Ordinal);
            Assert.Contains("expected.add(\"Techne.Loom.Abstractions\")", workflow, StringComparison.Ordinal);
            Assert.Contains("for package_path in artifacts/nuget/*.nupkg; do", workflow, StringComparison.Ordinal);
            Assert.Contains("Verify all exact-version packages on NuGet.org", workflow, StringComparison.Ordinal);
            Assert.Contains("no-RID Abstractions SDK", workflow, StringComparison.Ordinal);
            Assert.Contains("Techne.Loom.Abstractions {version}", workflow, StringComparison.Ordinal);
            Assert.Contains("GitHub releases contain the 16 exact-version AO/SO RID runtime packages plus the no-RID Abstractions SDK", workflow, StringComparison.Ordinal);
            Assert.Contains("| SDK | `—` | `Techne.Loom.Abstractions` | `dotnet add package Techne.Loom.Abstractions --version {version}`", workflow, StringComparison.Ordinal);
            Assert.Contains("abstractions-sdk-version:start", workflow, StringComparison.Ordinal);
            Assert.Contains("abstractions-sdk-version:end", workflow, StringComparison.Ordinal);
            Assert.Contains("`dotnet add package Techne.Loom.Abstractions --version {version}`", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("dotnet add package Techne.Loom.Abstractions --version 0.", workflow, StringComparison.Ordinal);
            var channelIndexName = workflowName == "publish-main.yml" ? "released" : "beta";
            Assert.Contains($"Path(\"packages.{channelIndexName}.md\")", workflow, StringComparison.Ordinal);
            Assert.Contains($"Path(\"packages.{channelIndexName}.zh-CN.md\")", workflow, StringComparison.Ordinal);
            Assert.Contains($"Path(\".agents/skills/loom-plan-execution/reference/packages.{channelIndexName}.md\")", workflow, StringComparison.Ordinal);
            Assert.Contains($"Path(\".agents/skills/loom-skill-enhancement/reference/packages.{channelIndexName}.md\")", workflow, StringComparison.Ordinal);

            var extractStepStart = workflow.IndexOf("- name: Extract published guide sources for target-local refresh", StringComparison.Ordinal);
            var closureStepStart = workflow.IndexOf("- name: Validate local runtime package closure", extractStepStart, StringComparison.Ordinal);
            Assert.True(extractStepStart >= 0 && closureStepStart > extractStepStart);
            var packageGuideRefresh = workflow[extractStepStart..closureStepStart];
            Assert.Contains("published_root=\"artifacts/published-docs\"", packageGuideRefresh, StringComparison.Ordinal);
            Assert.Contains("unzip -q \"$package_path\" -d \"$product_root\"", packageGuideRefresh, StringComparison.Ordinal);
            Assert.Contains("source_path = package[\"docs_root\"] / \"guides\" / source_file", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("Abstractions", packageGuideRefresh, StringComparison.Ordinal);
            var copyBackCommands = packageGuideRefresh.Split('\n')
                .Where(line => line.Contains("docs/en/guides", StringComparison.Ordinal)
                    && (line.Contains("cp ", StringComparison.Ordinal)
                        || line.Contains("mv ", StringComparison.Ordinal)
                        || line.Contains("rsync ", StringComparison.Ordinal)
                        || line.Contains("install ", StringComparison.Ordinal)))
                .ToArray();
            Assert.Empty(copyBackCommands);

            var releaseAssetCommand = workflow.Split('\n').Single(line => line.TrimStart().StartsWith("gh release create nuget-", StringComparison.Ordinal));
            Assert.Contains("artifacts/github-release/*.nupkg", releaseAssetCommand, StringComparison.Ordinal);
            Assert.Contains("artifacts/github-release/*.nupkg.sha512", releaseAssetCommand, StringComparison.Ordinal);
            Assert.DoesNotContain("artifacts/nuget/*.nupkg", releaseAssetCommand, StringComparison.Ordinal);
            Assert.Contains("Scope: sixteen self-contained AO/SO RID packages plus one no-RID Abstractions SDK package.", workflow, StringComparison.Ordinal);
            Assert.Contains("tools/${{ matrix.rid }}/docs/en/", workflow, StringComparison.Ordinal);
            Assert.Contains("docs_root", workflow, StringComparison.Ordinal);
            Assert.Contains("guide_path", workflow, StringComparison.Ordinal);
            Assert.Contains("nuget-stable-latest", workflow, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PublishWorkflows_CanonicalizeDocumentHashesAndPreserveBadIndexExamples()
    {
        var repoRoot = FindRepositoryRoot();
        foreach (var workflowName in new[] { "publish-main.yml", "publish-development.yml" })
        {
            var workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", workflowName))
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            var importIndex = workflow.IndexOf("          import re", StringComparison.Ordinal);
            var helperIndex = workflow.IndexOf("          def replace_active_version_literals", StringComparison.Ordinal);
            Assert.True(importIndex >= 0 && importIndex < helperIndex, "The package-index refresh helper must import re in its own Python heredoc.");
            Assert.Contains("source_content = source_path.read_text(encoding=\"utf-8\").replace(\"\\r\\n\", \"\\n\").replace(\"\\r\", \"\\n\")\n              source_content = \"\\n\".join(line.rstrip() for line in source_content.split(\"\\n\")).rstrip()\n              source_hash = hashlib.sha256", workflow, StringComparison.Ordinal);
            Assert.Contains("if stripped.startswith(\"- bad:\") or stripped.startswith(\"bad:\"):", workflow, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("publish-development.yml")]
    [InlineData("publish-main.yml")]
    public void PublishWorkflows_GuardSkillEvidenceLineCoordinates(string workflowName)
    {
        var repoRoot = FindRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", workflowName))
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var start = workflow.IndexOf("- name: Refresh skill package version blocks", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = workflow.IndexOf("\n          PY", start, StringComparison.Ordinal);
        Assert.True(end > start);
        var refresh = workflow[start..end];
        Assert.Contains("return text[:start] + block.rstrip(\"\\r\\n\") + text[end:]", refresh, StringComparison.Ordinal);
        Assert.Contains("if updated.count(\"\\n\") != text.count(\"\\n\"):", refresh, StringComparison.Ordinal);
        Assert.Contains("if replace_block(updated, path, block) != updated:", refresh, StringComparison.Ordinal);
    }

    [Fact]
    public void GitHubPackageUrl_UsesExactVersionAsset()
    {
        var packageUrl = LoomRuntimeCatalog.GetGitHubPackageUrl("Techne.Loom.SkillOrchestrator.Runtime.linux-x64", "0.3.320-beta", "beta");

        Assert.Equal("https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-x64.0.3.320-beta.nupkg", packageUrl);
    }

    [Fact]
    public void GitHubPackageUrl_RejectsLatestAlias()
    {
        Assert.Throws<ArgumentException>(() =>
            LoomRuntimeCatalog.GetGitHubPackageUrl("Techne.Loom.SkillOrchestrator.Runtime.linux-x64", "0.3.320-beta", "beta", latestAlias: true));
    }

    [Fact]
    public void PackageIndexesExposePublisherManagedRuntimeCommandBlocks()
    {
        var repositoryRoot = FindRepositoryRoot();
        var indexPaths = new[]
        {
            "packages.released.md",
            "packages.released.zh-CN.md",
            "packages.beta.md",
            "packages.beta.zh-CN.md",
        };

        foreach (var relativePath in indexPaths)
        {
            var index = File.ReadAllText(Path.Combine(repositoryRoot, relativePath));
            var start = index.IndexOf("<!-- package-dotnet-block:start -->", StringComparison.Ordinal);
            var end = index.IndexOf("<!-- package-dotnet-block:end -->", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start, $"Missing publisher-managed package command block in {relativePath}.");

            var block = index[start..end];
            var packageRows = block.Split('\n').Count(line =>
                line.StartsWith("| AO |", StringComparison.Ordinal) || line.StartsWith("| SO |", StringComparison.Ordinal));
            Assert.Equal(16, packageRows);
            foreach (var rid in LoomRuntimeCatalog.SupportedRuntimeIdentifiers)
            {
                Assert.Contains(rid, block, StringComparison.Ordinal);
            }
        }
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
