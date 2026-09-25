using System.Text.Json;
using Techne.Loom.Common.Runtime;
using Techne.Loom.SkillOrchestrator.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpConfigurationGeneratorTests
{
    [Fact]
    public void RuntimeIdentity_CreateRequiresExactRidApphost()
    {
        var root = CreateRuntimeRoot();
        try
        {
            var launchFile = Path.Combine(root, "so.exe");
            File.WriteAllText(launchFile, "self-contained");
            var identity = LoomRuntimeIdentity.Create(LoomRuntimeProduct.SkillOrchestrator, "0.3.270-BETA", "win-x64", launchFile);

            Assert.Equal("0.3.270-beta", identity.Version);
            Assert.Equal("win-x64", identity.RuntimeIdentifier);
            Assert.Equal("Techne.Loom.SkillOrchestrator.Runtime.win-x64", identity.PackageId);
            Assert.Equal(Path.GetFullPath(launchFile), identity.LaunchFile);
        }
        finally
        {
            DeleteRuntimeRoot(root);
        }
    }

    [Fact]
    public void RuntimeIdentity_RejectsDllLaunch()
    {
        var root = CreateRuntimeRoot();
        try
        {
            var launchFile = Path.Combine(root, "so.dll");
            File.WriteAllText(launchFile, "not an apphost");

            var exception = Assert.Throws<LoomRuntimeHostStartupException>(() =>
                LoomRuntimeIdentity.Create(LoomRuntimeProduct.SkillOrchestrator, "0.3.270-beta", "win-x64", launchFile));

            Assert.Contains("self-contained", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteRuntimeRoot(root);
        }
    }

    [Fact]
    public void RuntimeIdentity_FromCurrentProcessRejectsNonApphost()
    {
        var exception = Assert.Throws<LoomRuntimeHostStartupException>(() =>
            LoomRuntimeIdentity.FromCurrentProcess(LoomRuntimeProduct.SkillOrchestrator, typeof(McpConfigurationGeneratorTests).Assembly));

        Assert.Contains("self-contained", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateForApphost_WritesDirectApphostConfiguration()
    {
        var root = CreateRuntimeRoot();
        try
        {
            var launchFile = Path.Combine(root, "so.exe");
            File.WriteAllText(launchFile, "self-contained");
            var identity = LoomRuntimeIdentity.Create(LoomRuntimeProduct.SkillOrchestrator, "0.3.270-beta", "win-x64", launchFile);
            var outputFile = Path.Combine(root, "mcp.json");

            var result = McpConfigurationGenerator.GenerateForApphost(identity, outputFile, "vscode", serverName: null, force: false);

            Assert.Equal(identity.PackageId, result.PackageId);
            Assert.Equal(identity.Version, result.RuntimeVersion);
            Assert.Equal(identity.RuntimeIdentifier, result.Rid);
            Assert.Equal(Path.GetFullPath(launchFile), result.Command);
            Assert.Equal(["mcp", "stdio"], result.Arguments);
            using var document = JsonDocument.Parse(File.ReadAllText(outputFile));
            var server = document.RootElement.GetProperty("servers").GetProperty(result.ServerName);
            Assert.Equal(Path.GetFullPath(launchFile), server.GetProperty("command").GetString());
            Assert.Equal(["mcp", "stdio"], server.GetProperty("args").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray());
            var environment = server.GetProperty("env");
            Assert.Equal("true", environment.GetProperty("TECHNE_LOOM_MCP_BINDING_REQUIRED").GetString());
            Assert.Equal(identity.Version, environment.GetProperty("TECHNE_LOOM_MCP_BINDING_VERSION").GetString());
            Assert.Equal(identity.RuntimeIdentifier, environment.GetProperty("TECHNE_LOOM_MCP_BINDING_RID").GetString());
            Assert.Equal(result.AppHostSha256, environment.GetProperty("TECHNE_LOOM_MCP_BINDING_EXECUTABLE_SHA256").GetString());
            Assert.False(environment.TryGetProperty("TECHNE_LOOM_MCP_BINDING_DESCRIPTOR_SHA256", out _));
            Assert.DoesNotContain("runtime_descriptor_file", File.ReadAllText(outputFile), StringComparison.Ordinal);
        }
        finally
        {
            DeleteRuntimeRoot(root);
        }
    }

    [Fact]
    public void GenerateForApphost_PreservesOtherServersAndForceRewrites()
    {
        var root = CreateRuntimeRoot();
        try
        {
            var launchFile = Path.Combine(root, "so.exe");
            File.WriteAllText(launchFile, "self-contained");
            var identity = LoomRuntimeIdentity.Create(LoomRuntimeProduct.SkillOrchestrator, "0.3.270-beta", "win-x64", launchFile);
            var outputFile = Path.Combine(root, "mcp.json");
            File.WriteAllText(outputFile, "{\"servers\":{\"other-server\":{\"command\":\"other\"}}}");

            var result = McpConfigurationGenerator.GenerateForApphost(identity, outputFile, "vscode", serverName: null, force: false);
            using var document = JsonDocument.Parse(File.ReadAllText(outputFile));
            Assert.Equal("updated", result.Status);
            Assert.Equal("other", document.RootElement.GetProperty("servers").GetProperty("other-server").GetProperty("command").GetString());
            Assert.True(document.RootElement.GetProperty("servers").TryGetProperty("loom-so-0.3.270-beta", out _));

            var forced = McpConfigurationGenerator.GenerateForApphost(identity, outputFile, "vscode", serverName: null, force: true);
            Assert.Equal("updated", forced.Status);
        }
        finally
        {
            DeleteRuntimeRoot(root);
        }
    }

    private static string CreateRuntimeRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "techne-loom-mcp-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRuntimeRoot(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
