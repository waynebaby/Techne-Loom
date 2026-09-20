using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class ContractContextProviderBoundaryTests
{
    [Fact]
    public async Task ReadAsyncAllowsLargeContractWhenRequestedFragmentIsBounded()
    {
        var root = Path.Combine(Path.GetTempPath(), $"techne-loom-contract-large-{Guid.NewGuid():N}");
        var contractPath = Path.Combine(root, "assets", "so-workflow", "contract.json");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        var largeValue = new string('x', 10_000);
        await File.WriteAllTextAsync(contractPath, $$"""
{
  "name": "large-contract",
  "inputs": { "request": { "type": "object" } },
  "outputs": { "result": { "type": "object" } },
  "default_assumptions": { "safe": true },
  "small": { "value": "ok" },
  "large": "{{largeValue}}"
}
""");

        try
        {
            var result = await new ContractContextProvider().ReadAsync(
                new ContractBinding(),
                ["/small"],
                root,
                new WorkflowFragmentLimits(MaxBytes: 128, MaxArrayItems: 4, MaxDepth: 4));

            Assert.Equal("ok", result.Fragments["/small"].GetProperty("value").GetString());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ReadAsyncRejectsInvalidJsonPointerEscape()
    {
        var root = Path.Combine(Path.GetTempPath(), $"techne-loom-contract-pointer-{Guid.NewGuid():N}");
        var contractPath = Path.Combine(root, "assets", "so-workflow", "contract.json");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        await File.WriteAllTextAsync(contractPath, """
{
  "name": "pointer-contract",
  "inputs": {},
  "outputs": {},
  "default_assumptions": {},
  "rules": { "value": true }
}
""");

        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new ContractContextProvider().ReadAsync(new ContractBinding(), ["/rules/~2~0value"], root));
            Assert.Contains("invalid escape", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ReadAsyncRejectsNonCanonicalArrayIndex()
    {
        var root = Path.Combine(Path.GetTempPath(), $"techne-loom-contract-array-index-{Guid.NewGuid():N}");
        var contractPath = Path.Combine(root, "assets", "so-workflow", "contract.json");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        await File.WriteAllTextAsync(contractPath, """
{
  "name": "array-contract",
  "inputs": {},
  "outputs": {},
  "default_assumptions": {},
  "items": [{ "value": "zero" }, { "value": "one" }]
}
""");

        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new ContractContextProvider().ReadAsync(new ContractBinding(), ["/items/01"], root));
            Assert.Contains("invalid array index", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
