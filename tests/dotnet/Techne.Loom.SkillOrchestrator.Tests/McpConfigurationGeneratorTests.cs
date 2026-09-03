using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Techne.Loom.Common.Runtime;
using Techne.Loom.SkillOrchestrator.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpConfigurationGeneratorTests
{
    [Fact]
    public void Generate_UsesResolverSelectedSelfContainedExecutable()
    {
        var root = CreateRuntimeRoot();
        try
        {
            var launchFile = Path.Combine(root, "so.exe");
            File.WriteAllText(launchFile, "self-contained");
            var descriptorFile = WriteDescriptor(root, CreateSelfContainedDescriptor(root, launchFile));
            var outputFile = Path.Combine(root, "mcp.json");

            var result = McpConfigurationGenerator.Generate(new McpConfigurationGenerationOptions(
                outputFile,
                "vscode",
                "loom-so",
                false,
                descriptorFile));

            Assert.Equal("self-contained", result.RuntimeMode);
            Assert.Equal(Path.GetFullPath(launchFile), result.Command);
            Assert.Equal(["mcp", "stdio"], result.Arguments);
            Assert.Equal(Path.GetFullPath(launchFile), result.LaunchFile);
            Assert.True(File.Exists(outputFile));
            using var document = JsonDocument.Parse(File.ReadAllText(outputFile));
            var server = document.RootElement.GetProperty("servers").GetProperty("loom-so");
            Assert.Equal(Path.GetFullPath(launchFile), server.GetProperty("command").GetString());
            Assert.Equal(["mcp", "stdio"], server.GetProperty("args").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray());
        }
        finally
        {
            DeleteRuntimeRoot(root);
        }
    }

    [Fact]
    public void Generate_UsesResolverSelectedFrameworkDllAndPrefix()
    {
        var root = CreateRuntimeRoot();
        try
        {
            var launchFile = Path.Combine(root, "so.dll");
            var depsFile = Path.Combine(root, "so.deps.json");
            var runtimeConfigFile = Path.Combine(root, "so.runtimeconfig.json");
            File.WriteAllText(launchFile, "framework-dependent");
            File.WriteAllText(depsFile, "{}");
            File.WriteAllText(runtimeConfigFile, "{}");
            var descriptorFile = WriteDescriptor(root, CreateFrameworkDescriptor(root, launchFile, depsFile, runtimeConfigFile));
            var outputFile = Path.Combine(root, ".mcp.json");

            var result = McpConfigurationGenerator.Generate(new McpConfigurationGenerationOptions(
                outputFile,
                "claude",
                "loom-so",
                false,
                descriptorFile));

            Assert.Equal("framework-dependent", result.RuntimeMode);
            Assert.Equal("dotnet", Path.GetFileNameWithoutExtension(result.Command));
            if (Path.IsPathFullyQualified(result.Command))
            {
                Assert.True(File.Exists(result.Command), $"Resolved .NET host does not exist: {result.Command}");
            }
            Assert.Equal(Path.GetFullPath(launchFile), result.LaunchFile);
            Assert.Equal(["exec", "--depsfile", depsFile, "--runtimeconfig", runtimeConfigFile, launchFile, "mcp", "stdio"], result.Arguments);
            using var document = JsonDocument.Parse(File.ReadAllText(outputFile));
            var server = document.RootElement.GetProperty("mcpServers").GetProperty("loom-so");
            Assert.Equal(result.Command, server.GetProperty("command").GetString());
            Assert.Equal(result.Arguments.ToArray(), server.GetProperty("args").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray());
        }
        finally
        {
            DeleteRuntimeRoot(root);
        }
    }

    private static string CreateRuntimeRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "techne-loom-mcp-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "docs", "en", "guides"));
        File.WriteAllText(Path.Combine(root, "docs", "en", "guides", "so-guide.md"), "guide");
        return root;
    }

    private static string WriteDescriptor(string root, LoomLaunchDescriptor descriptor)
    {
        var path = Path.Combine(root, "runtime-launch-descriptor.json");
        LoomPreparationDiagnostics.WriteToFile(descriptor, path);
        return path;
    }

    private static LoomLaunchDescriptor CreateSelfContainedDescriptor(string root, string launchFile)
    {
        var packageHash = Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes("package")));
        var guideHash = Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes("guide")));
        var runtimeRoot = Path.GetFullPath(root);
        return new LoomLaunchDescriptor(
            LoomRuntimeMode.SelfContained,
            LoomRuntimeProduct.SkillOrchestrator,
            "0.3.270",
            "released",
            "win-x64",
            "Techne.Loom.SkillOrchestrator.Runtime.win-x64",
            ["Techne.Loom.SkillOrchestrator.Runtime.win-x64"],
            "https://example.invalid/so.nupkg",
            packageHash,
            runtimeRoot,
            runtimeRoot,
            Path.GetFullPath(launchFile),
            [],
            "self-contained-single-file-package",
            Path.Combine(runtimeRoot, "docs", "en", "guides", "so-guide.md"),
            Path.Combine(runtimeRoot, "docs", "en"),
            guideHash,
            Path.Combine(runtimeRoot, ".extraction"),
            LoomPreparationDiagnostics.CreatePreparationId(LoomRuntimeMode.SelfContained, LoomRuntimeProduct.SkillOrchestrator, "0.3.270", "win-x64", runtimeRoot, packageHash),
            "https://example.invalid/so.nupkg.sha512");
    }

    private static LoomLaunchDescriptor CreateFrameworkDescriptor(string root, string launchFile, string depsFile, string runtimeConfigFile)
    {
        var runtimeRoot = Path.GetFullPath(root);
        return new LoomLaunchDescriptor(
            LoomRuntimeMode.FrameworkDependent,
            LoomRuntimeProduct.SkillOrchestrator,
            "0.3.270",
            "released",
            "win-x64",
            "Techne.Loom.SkillOrchestrator",
            ["Techne.Loom.SkillOrchestrator", "Techne.Loom.Common", "Techne.Loom.Abstractions"],
            null,
            null,
            runtimeRoot,
            runtimeRoot,
            Path.GetFullPath(launchFile),
            ["exec", "--depsfile", Path.GetFullPath(depsFile), "--runtimeconfig", Path.GetFullPath(runtimeConfigFile)],
            "framework-dependent-net9-host",
            Path.Combine(runtimeRoot, "docs", "en", "guides", "so-guide.md"),
            Path.Combine(runtimeRoot, "docs", "en"),
            Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes("guide"))),
            null,
            LoomPreparationDiagnostics.CreatePreparationId(LoomRuntimeMode.FrameworkDependent, LoomRuntimeProduct.SkillOrchestrator, "0.3.270", "win-x64", runtimeRoot, null));
    }

    private static void DeleteRuntimeRoot(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
