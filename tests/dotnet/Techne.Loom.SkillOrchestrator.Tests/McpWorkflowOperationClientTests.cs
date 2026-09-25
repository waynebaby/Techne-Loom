using System.Text.Json;
using Techne.Loom.Common.Mcp;
using Techne.Loom.Common.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpWorkflowOperationClientTests
{
    [Fact]
    public async Task CallAsync_RequiresOperationIdBeforeStartingHost()
    {
        var root = Path.Combine(Path.GetTempPath(), "loom-mcp-client-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var identity = CreateIdentity(root);
            using var arguments = JsonDocument.Parse("{}");

            await Assert.ThrowsAsync<McpToolInputException>(() => McpWorkflowOperationClient.CallAsync(
                identity,
                "so_inspect_workflow_fragment",
                arguments.RootElement.Clone(),
                ["--guide"]));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Fallback_UsesDirectSelfContainedApphost()
    {
        var root = Path.Combine(Path.GetTempPath(), "loom-mcp-fallback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var identity = CreateIdentity(root);
            var launch = McpCliFallbackPolicy.CreateCommand(
                identity,
                ["--guide"],
                McpDispatchStage.PreDispatch,
                "mcp_transport_unavailable");

            Assert.Equal(identity.LaunchFile, launch.Command);
            Assert.Equal(["--guide"], launch.Arguments);
            Assert.Equal(root, launch.WorkingDirectory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Fallback_RejectsFailuresAfterDispatch()
    {
        var root = Path.Combine(Path.GetTempPath(), "loom-mcp-no-fallback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var identity = CreateIdentity(root);
            Assert.Throws<InvalidOperationException>(() => McpCliFallbackPolicy.CreateCommand(
                identity,
                ["--guide"],
                McpDispatchStage.Dispatched,
                "mcp_application_failed"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static LoomRuntimeIdentity CreateIdentity(string root)
    {
        var launchFile = Path.Combine(root, "so.exe");
        File.WriteAllText(launchFile, "apphost");
        return LoomRuntimeIdentity.Create(LoomRuntimeProduct.SkillOrchestrator, "0.3.282-beta", "win-x64", launchFile);
    }
}
