using System.Text.Json;
using Techne.Loom.Common.Mcp;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpWorkflowToolBoundaryTests
{
    [Fact]
    public async Task Server_RejectsInvalidInitializeAndNotificationIds()
    {
        var registry = WorkflowMcpToolSet.Create("so");
        using var input = new StringReader(string.Join(
            Environment.NewLine,
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"notifications/cancelled\"}"));
        using var output = new StringWriter();

        await new McpStdioServer(
            registry,
            new McpStdioServerOptions("test-server", "1.0.0"),
            input,
            output).RunAsync();

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        using var initializeError = JsonDocument.Parse(lines[0]);
        Assert.Equal(-32602, initializeError.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        using var listError = JsonDocument.Parse(lines[1]);
        Assert.Equal(-32002, listError.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        using var cancelledError = JsonDocument.Parse(lines[2]);
        Assert.Equal(-32600, cancelledError.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Server_RejectsInlineJsonWithoutReturningTheSubmittedValue()
    {
        var registry = WorkflowMcpToolSet.Create("so");
        using var input = new StringReader(string.Join(
            Environment.NewLine,
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test-client\",\"version\":\"1.0.0\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"so_get_workflow_status\",\"arguments\":{\"workflow_file\":\"{\\\"secret\\\":\\\"value\\\"}\"}}}"));
        using var output = new StringWriter();

        await new McpStdioServer(
            registry,
            new McpStdioServerOptions("test-server", "1.0.0"),
            input,
            output).RunAsync();

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var response = JsonDocument.Parse(lines[1]);
        var result = response.RootElement.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        var text = result.GetProperty("content")[0].GetProperty("text").GetString();
        Assert.Contains("file path only", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
        Assert.DoesNotContain("value", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InspectWorkflowTool_ReturnsSummaryWithoutContextValuesByDefault()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-mcp-fragment-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(
                workflowFile,
                "{\"instanceId\":\"fragment-test\",\"status\":\"readyToStart\",\"version\":1,\"nodes\":{},\"context\":{\"secret\":\"large-context-value\"}}");
            var registry = WorkflowMcpToolSet.Create("so");
            Assert.True(registry.TryGet("so_inspect_workflow_fragment", out var tool));
            var escapedPath = workflowFile.Replace("\\", "\\\\", StringComparison.Ordinal);
            using var argumentsDocument = JsonDocument.Parse($"{{\"workflow_file\":\"{escapedPath}\"}}");

            var result = await tool!.InvokeAsync(argumentsDocument.RootElement.Clone());

            var text = result.Content[0].Text;
            Assert.False(result.IsError);
            Assert.Contains("fragment-test", text, StringComparison.Ordinal);
            Assert.Contains("secret", text, StringComparison.Ordinal);
            Assert.DoesNotContain("large-context-value", text, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(workflowFile))
            {
                File.Delete(workflowFile);
            }
        }
    }

    [Fact]
    public async Task Server_StopsCleanlyWhenCancellationIsRequested()
    {
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await new McpStdioServer(
            new McpToolRegistry(),
            new McpStdioServerOptions("test-server", "1.0.0"),
            input,
            output).RunAsync(cancellation.Token);

        Assert.Empty(output.ToString());
    }
}
