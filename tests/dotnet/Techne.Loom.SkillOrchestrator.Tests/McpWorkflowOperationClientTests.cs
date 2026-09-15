using System.Reflection;
using System.Text.Json;
using Techne.Loom.Common.Mcp;
using Techne.Loom.Common.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpWorkflowOperationClientTests
{
    [Fact]
    public async Task CallAsync_HandshakeVersionMismatch_UsesCliFallbackBeforeDispatch()
    {
        var descriptor = CreateDescriptor("0.3.282-beta");
        using var arguments = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["operation_id"] = "mcp-client-fallback",
        }));

        var result = await McpWorkflowOperationClient.CallAsync(
            descriptor,
            "so_inspect_workflow_fragment",
            arguments.RootElement.Clone(),
            ["--guide"],
            TimeSpan.FromSeconds(30));

        Assert.Equal("cli", result.Transport);
        Assert.Equal(McpDispatchStage.PreDispatch, result.DispatchStage);
        Assert.Equal("mcp_handshake_unsupported", result.FallbackReason);
        Assert.NotEmpty(result.Result.Content);
        Assert.Contains("version", result.Result.Content[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallAsync_ApplicationErrorAfterToolDispatch_DoesNotUseCliFallback()
    {
        var descriptor = CreateDescriptor(GetRuntimeVersion());
        var missingWorkflowFile = Path.Combine(Path.GetTempPath(), $"loom-mcp-missing-{Guid.NewGuid():N}.json");
        using var arguments = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["operation_id"] = "mcp-client-application-error",
            ["workflow_file"] = missingWorkflowFile,
        }));

        var exception = await Assert.ThrowsAsync<McpApplicationException>(() => McpWorkflowOperationClient.CallAsync(
            descriptor,
            "so_inspect_workflow_fragment",
            arguments.RootElement.Clone(),
            ["--guide"],
            TimeSpan.FromSeconds(30)));

        Assert.Equal("mcp_application_failed", exception.Reason);
        Assert.Equal(McpDispatchStage.Dispatched, exception.Stage);
        Assert.False(McpCliFallbackPolicy.CanFallback(exception.Stage, exception.Reason));
    }

    private static LoomLaunchDescriptor CreateDescriptor(string version)
    {
        var assembly = typeof(DefaultWorkflowTaskTrackingService).Assembly.Location;
        var runtimeRoot = Path.GetDirectoryName(assembly) ?? throw new InvalidOperationException("SO runtime root was not found.");
        var runtimeConfig = Path.Combine(runtimeRoot, Path.GetFileNameWithoutExtension(assembly) + ".runtimeconfig.json");
        Assert.True(File.Exists(runtimeConfig));
        var packageIds = new[]
        {
            LoomRuntimeCatalog.GetProductPackageId(LoomRuntimeProduct.SkillOrchestrator),
            "Techne.Loom.Common",
            "Techne.Loom.Abstractions",
        };
        return new LoomLaunchDescriptor(
            LoomRuntimeMode.FrameworkDependent,
            LoomRuntimeProduct.SkillOrchestrator,
            version,
            "beta",
            OperatingSystem.IsWindows() ? "win-x64" : "linux-x64",
            null,
            packageIds,
            null,
            null,
            Path.GetTempPath(),
            runtimeRoot,
            assembly,
            ["exec", "--runtimeconfig", runtimeConfig],
            "mcp-client-test",
            Path.Combine(runtimeRoot, "guide.md"),
            runtimeRoot,
            Convert.ToBase64String(new byte[64]),
            null,
            "prep-mcp-client-test");
    }

    private static string GetRuntimeVersion()
        => typeof(DefaultWorkflowTaskTrackingService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?.Split('+', 2)[0]
            ?? throw new InvalidOperationException("SO runtime version was not found.");
}
