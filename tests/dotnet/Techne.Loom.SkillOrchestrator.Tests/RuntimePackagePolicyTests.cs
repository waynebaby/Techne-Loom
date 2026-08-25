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
    public void PublishWorkflows_DeclareTheCompleteRuntimeMatrixAndAssetFlow()
    {
        var repoRoot = FindRepositoryRoot();
        foreach (var workflowName in new[] { "publish-main.yml", "publish-development.yml" })
        {
            var workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", workflowName));
            Assert.Contains("runtime-packages:", workflow, StringComparison.Ordinal);
            Assert.Contains("needs: runtime-packages", workflow, StringComparison.Ordinal);
            Assert.Contains("actions/upload-artifact@v4", workflow, StringComparison.Ordinal);
            Assert.Contains("actions/download-artifact@v4", workflow, StringComparison.Ordinal);
            Assert.Contains("artifacts/nuget/*.nupkg.sha512", workflow, StringComparison.Ordinal);
            Assert.Contains("Techne.Loom.*.Runtime.*.nupkg", workflow, StringComparison.Ordinal);
            Assert.Contains("tools/${{ matrix.rid }}/docs/en/", workflow, StringComparison.Ordinal);
            Assert.Contains("docs_root", workflow, StringComparison.Ordinal);
            Assert.Contains("guide_path", workflow, StringComparison.Ordinal);
            Assert.Contains("nuget-stable-latest", workflow, StringComparison.Ordinal);
            Assert.All(LoomRuntimeCatalog.SupportedRuntimeIdentifiers, rid =>
                Assert.Contains($"- {rid}", workflow, StringComparison.Ordinal));
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
