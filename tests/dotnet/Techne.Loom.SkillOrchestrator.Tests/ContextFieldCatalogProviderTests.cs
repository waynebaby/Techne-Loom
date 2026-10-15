using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class ContextFieldCatalogProviderTests
{
    [Fact]
    public async Task ReadAsyncLoadsTheOwningCatalogThroughBoundedContractProvider()
    {
        var root = CreateTempDirectory();
        var contractPath = Path.Combine(root, "assets", "so-workflow", "contract.json");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        await File.WriteAllTextAsync(contractPath, ContractJson("""
            [{"path":"result.status","type":"string","nullable":false,"owner":"runtime","description":"Result status.","allowed_values":["passed","failed"],"suggested_values":[]}]
            """));

        try
        {
            var result = await new ContextFieldCatalogProvider().ReadAsync(new ContractBinding(), root);

            Assert.Equal(Path.GetFullPath(contractPath), result.ContractPath);
            Assert.Equal(64, result.ContractSha256.Length);
            Assert.True(result.ReturnedBytes > 0);
            Assert.Equal("string", result.Catalog.Fields["result.status"].Type);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReadAsyncRejectsAnInvalidTypedSuggestionFromTheOwningContract()
    {
        var root = CreateTempDirectory();
        var contractPath = Path.Combine(root, "assets", "so-workflow", "contract.json");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        await File.WriteAllTextAsync(contractPath, ContractJson("""
            [{"path":"result.approved","type":"boolean","nullable":false,"owner":"user","description":"Approval.","suggested_values":[{"value":"true","label":"Approve","source":"test","kind":"choice"}]}]
            """));

        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new ContextFieldCatalogProvider().ReadAsync(new ContractBinding(), root));

            Assert.Contains("JSON type 'boolean'", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReadAsyncAllowsMoreCatalogFieldsThanGeneralFragmentArrayLimit()
    {
        var root = CreateTempDirectory();
        var contractPath = Path.Combine(root, "contract.json");
        var fields = new System.Text.Json.Nodes.JsonArray();
        for (var index = 0; index < 40; index++)
        {
            fields.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["path"] = $"field_{index}",
                ["type"] = "string",
                ["nullable"] = false,
                ["owner"] = "caller",
                ["description"] = "Test field.",
            });
        }

        var contract = new System.Text.Json.Nodes.JsonObject
        {
            ["name"] = "catalog-limit-test",
            ["inputs"] = new System.Text.Json.Nodes.JsonObject(),
            ["outputs"] = new System.Text.Json.Nodes.JsonObject(),
            ["default_assumptions"] = new System.Text.Json.Nodes.JsonObject(),
            ["context_fields"] = new System.Text.Json.Nodes.JsonObject
            {
                ["schemaVersion"] = "loom.context-fields.v1",
                ["fields"] = fields,
            },
        };
        await File.WriteAllTextAsync(contractPath, contract.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        try
        {
            var result = await new ContextFieldCatalogProvider().ReadAsync(new ContractBinding { Path = "contract.json" }, root);

            Assert.Equal(40, result.Catalog.Fields.Count);
            Assert.Equal(256, result.Limits.MaxArrayItems);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReadAsyncLoadsLargeSelfBootstrapOwnerCatalogWithinDedicatedLimit()
    {
        var repositoryRoot = FindRepositoryRoot();
        var ownerRoot = Path.Combine(repositoryRoot, ".agents", "skills", "loom-skill-enhancement");
        var result = await new ContextFieldCatalogProvider().ReadAsync(
            new ContractBinding
            {
                Path = "assets/so-workflow/contract.json",
                Format = "json",
                AssetRootPath = ownerRoot,
            },
            ownerRoot);

        Assert.True(result.Catalog.Fields.Count > 32);
        Assert.Equal(256, result.Limits.MaxArrayItems);
        Assert.Equal("user", result.Catalog.Fields["target_skill_path"].Owner);
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

    private static string ContractJson(string fields)
        => $$"""
        {
          "name": "catalog-provider-test",
          "inputs": {},
          "outputs": {},
          "default_assumptions": {},
          "context_fields": {
            "schemaVersion": "loom.context-fields.v1",
            "fields": {{fields}}
          }
        }
        """;

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"techne-loom-field-catalog-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
    [Fact]
    public async Task ReadAsyncRejectsContractReachedThroughEscapingDirectoryLink()
    {
        var ownerRoot = CreateTempDirectory();
        var outsideRoot = CreateTempDirectory();
        await File.WriteAllTextAsync(Path.Combine(outsideRoot, "contract.json"), ContractJson("""
            [{"path":"result.status","type":"string","nullable":false,"owner":"runtime","description":"Result status."}]
            """));
        var linkPath = Path.Combine(ownerRoot, "linked");
        try
        {
            Directory.CreateSymbolicLink(linkPath, outsideRoot);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Directory.Delete(ownerRoot, recursive: true);
            Directory.Delete(outsideRoot, recursive: true);
            return;
        }

        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new ContextFieldCatalogProvider().ReadAsync(
                    new ContractBinding { Path = Path.Combine("linked", "contract.json") },
                    ownerRoot));

            Assert.Contains("escapes the declared asset root", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(ownerRoot, recursive: true);
            Directory.Delete(outsideRoot, recursive: true);
        }
    }
}
