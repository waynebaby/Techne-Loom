using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class ContractContextProviderTests
{
    [Fact]
    public async Task ReadAsyncReturnsBoundedFragmentsAndReusesUnchangedHash()
    {
        var root = CreateTempDirectory();
        var contractPath = Path.Combine(root, "assets", "so-workflow", "contract.json");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        await File.WriteAllTextAsync(contractPath, """
{
  "name": "test-contract",
  "inputs": { "request": { "type": "object" } },
  "outputs": { "result": { "type": "object" } },
  "default_assumptions": { "safe": true },
  "rules": { "request": { "required": true } }
}
""");

        try
        {
            var provider = new ContractContextProvider();
            var first = await provider.ReadAsync(
                new ContractBinding(),
                ["/rules/request"],
                root);
            var second = await provider.ReadAsync(
                new ContractBinding(),
                ["/rules/request"],
                root);

            Assert.False(first.CacheHit);
            Assert.True(second.CacheHit);
            Assert.Equal(first.ContractSha256, second.ContractSha256);
            Assert.Equal("true", first.Fragments["/rules/request"].GetProperty("required").GetRawText());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ReadAsyncRefreshesWhenContractBytesChange()
    {
        var root = CreateTempDirectory();
        var contractPath = Path.Combine(root, "assets", "so-workflow", "contract.json");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        await File.WriteAllTextAsync(contractPath, CreateContractJson("one"));

        try
        {
            var provider = new ContractContextProvider();
            var first = await provider.ReadAsync(new ContractBinding(), ["/rules/value"], root);
            await File.WriteAllTextAsync(contractPath, CreateContractJson("two"));
            var second = await provider.ReadAsync(new ContractBinding(), ["/rules/value"], root);

            Assert.False(second.CacheHit);
            Assert.NotEqual(first.ContractSha256, second.ContractSha256);
            Assert.Equal("two", second.Fragments["/rules/value"].GetString());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ReadAsyncRejectsMissingRequiredContractSurfaces()
    {
        var root = CreateTempDirectory();
        var contractPath = Path.Combine(root, "assets", "so-workflow", "contract.json");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        await File.WriteAllTextAsync(contractPath, "{\"name\":\"bad\"}");

        try
        {
            var provider = new ContractContextProvider();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ReadAsync(new ContractBinding(), [""], root));
            Assert.Contains("inputs", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ReadAsyncRejectsContractPathOutsideAssetRoot()
    {
        var root = CreateTempDirectory();
        var outside = Path.Combine(Path.GetTempPath(), $"contract-outside-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(outside, CreateContractJson("outside"));

        try
        {
            var provider = new ContractContextProvider();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ReadAsync(new ContractBinding { Path = outside }, [], root));
            Assert.Contains("escapes", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
            DeleteFile(outside);
        }
    }

    private static string CreateContractJson(string value)
        => $$"""
{
  "name": "test-contract",
  "inputs": { "request": { "type": "object" } },
  "outputs": { "result": { "type": "object" } },
  "default_assumptions": { "safe": true },
  "rules": { "value": "{{value}}" }
}
""";

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"techne-loom-contract-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var lockFile = path + ".lock";
        if (File.Exists(lockFile))
        {
            File.Delete(lockFile);
        }
    }
}
