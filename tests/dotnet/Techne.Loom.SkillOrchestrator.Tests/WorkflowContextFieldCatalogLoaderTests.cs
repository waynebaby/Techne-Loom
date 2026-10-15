using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowContextFieldCatalogLoaderTests
{
    [Fact]
    public async Task ReadRequiredAsyncLoadsCatalogFromTheDeclaredOwningRoot()
    {
        var root = CreateTempDirectory();
        var contractPath = Path.Combine(root, "assets", "so-workflow", "contract.json");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        await File.WriteAllTextAsync(contractPath, ContractJson);
        var instance = new WorkflowInstance
        {
            InstanceId = "loader-test",
            ContractBinding = new ContractBinding
            {
                Path = "assets/so-workflow/contract.json",
                AssetRootPath = root,
            },
        };

        try
        {
            var result = await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(instance);

            Assert.Equal(Path.GetFullPath(contractPath), result.ContractPath);
            Assert.Equal("string", result.Catalog.Fields["result.status"].Type);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReadRequiredAsyncRequiresAnOwningRoot()
    {
        var instance = new WorkflowInstance
        {
            InstanceId = "missing-root",
            ContractBinding = new ContractBinding { Path = "assets/so-workflow/contract.json" },
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => WorkflowContextFieldCatalogLoader.ReadRequiredAsync(instance));

        Assert.Contains("owning skill root", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadRequiredAsyncRejectsConflictingRootDeclarations()
    {
        var instance = new WorkflowInstance
        {
            InstanceId = "conflicting-root",
            Context = new Dictionary<string, object?> { ["skill_root"] = Path.Combine(Path.GetTempPath(), "different-owner") },
            ContractBinding = new ContractBinding
            {
                Path = "assets/so-workflow/contract.json",
                AssetRootPath = Path.GetTempPath(),
                AssetRootInput = "skill_root",
            },
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => WorkflowContextFieldCatalogLoader.ReadRequiredAsync(instance));

        Assert.Contains("different owning skill roots", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadRequiredAsyncComparesRootsUsingFilesystemCaseSensitivity()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var rootPrefix = Path.Combine(Path.GetTempPath(), $"techne-loom-owner-case-{Guid.NewGuid():N}");
        var configuredRoot = rootPrefix + "A";
        var inputRoot = rootPrefix + "a";
        Directory.CreateDirectory(configuredRoot);
        Directory.CreateDirectory(inputRoot);
        var instance = new WorkflowInstance
        {
            InstanceId = "case-sensitive-roots",
            Context = new Dictionary<string, object?> { ["skill_root"] = inputRoot },
            ContractBinding = new ContractBinding
            {
                Path = "assets/so-workflow/contract.json",
                AssetRootPath = configuredRoot,
                AssetRootInput = "skill_root",
            },
        };

        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => WorkflowContextFieldCatalogLoader.ReadRequiredAsync(instance));

            Assert.Contains("different owning skill roots", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(configuredRoot, recursive: true);
            Directory.Delete(inputRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"techne-loom-owner-contract-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private const string ContractJson = """
        {
          "name": "loader-test",
          "inputs": {},
          "outputs": {},
          "default_assumptions": {},
          "context_fields": {
            "schemaVersion": "loom.context-fields.v1",
            "fields": [
              {"path":"result.status","type":"string","nullable":false,"owner":"runtime","description":"Status.","suggested_values":[]}
            ]
          }
        }
        """;
}
