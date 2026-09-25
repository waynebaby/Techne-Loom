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
            var workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", workflowName));
            Assert.Contains("published_root=\"artifacts/published-docs\"", workflow, StringComparison.Ordinal);
            Assert.Contains("unzip -q \"$package_path\" -d \"$product_root\"", workflow, StringComparison.Ordinal);
            Assert.Contains("source_path = package[\"docs_root\"] / \"guides\" / source_file", workflow, StringComparison.Ordinal);
            var extractStepStart = workflow.IndexOf("- name: Extract published guide sources for target-local refresh", StringComparison.Ordinal);
            Assert.True(extractStepStart >= 0);
            var packageGuideRefresh = workflow[extractStepStart..];
            var copyBackCommands = packageGuideRefresh.Split('\n')
                .Where(line => line.Contains("docs/en/guides", StringComparison.Ordinal)
                    && (line.Contains("cp ", StringComparison.Ordinal)
                        || line.Contains("mv ", StringComparison.Ordinal)
                        || line.Contains("rsync ", StringComparison.Ordinal)
                        || line.Contains("install ", StringComparison.Ordinal)))
                .ToArray();
            Assert.Empty(copyBackCommands);
            Assert.Contains("runtime-packages:", workflow, StringComparison.Ordinal);
            Assert.Contains("needs: [version, runtime-packages]", workflow, StringComparison.Ordinal);
            Assert.Contains("actions/upload-artifact@v4", workflow, StringComparison.Ordinal);
            Assert.Contains("actions/download-artifact@v4", workflow, StringComparison.Ordinal);
            Assert.Contains("test -f \"$package_path.sha512\"", workflow, StringComparison.Ordinal);
            Assert.Contains("for package_path in artifacts/nuget/*.nupkg; do", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("Techne.Loom.SkillOrchestrator.$PACKAGE_VERSION.nupkg", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("Techne.Loom.Common.$PACKAGE_VERSION.nupkg", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("Techne.Loom.Abstractions.$PACKAGE_VERSION.nupkg", workflow, StringComparison.Ordinal);
            Assert.Contains("Techne.Loom.*.Runtime.*.nupkg", workflow, StringComparison.Ordinal);
            var releaseAssetCommand = workflow.Split('\n').Single(line => line.TrimStart().StartsWith("gh release create nuget-", StringComparison.Ordinal));
            Assert.Contains("artifacts/github-release/*.nupkg", releaseAssetCommand, StringComparison.Ordinal);
            Assert.Contains("artifacts/github-release/*.nupkg.sha512", releaseAssetCommand, StringComparison.Ordinal);
            Assert.DoesNotContain("artifacts/nuget/*.nupkg", releaseAssetCommand, StringComparison.Ordinal);
            Assert.Contains("tools/${{ matrix.rid }}/docs/en/", workflow, StringComparison.Ordinal);
            Assert.Contains("docs_root", workflow, StringComparison.Ordinal);
            Assert.Contains("guide_path", workflow, StringComparison.Ordinal);
            Assert.Contains("nuget-stable-latest", workflow, StringComparison.Ordinal);
            Assert.All(LoomRuntimeCatalog.SupportedRuntimeIdentifiers, rid =>
                Assert.Contains($"- {rid}", workflow, StringComparison.Ordinal));
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
