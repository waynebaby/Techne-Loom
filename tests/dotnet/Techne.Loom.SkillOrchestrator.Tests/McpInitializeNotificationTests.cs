using System.Text.Json;
using Techne.Loom.Common.Mcp;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpInitializeNotificationTests
{
    [Fact]
    public async Task InitializeNotificationDoesNotUnlockTools()
    {
        using var input = new StringReader(string.Join(
            Environment.NewLine,
            "{\"jsonrpc\":\"2.0\",\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test-client\",\"version\":\"1.0.0\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}"));
        using var output = new StringWriter();
        var registry = WorkflowMcpToolSet.Create("so");

        await new McpStdioServer(
            registry,
            new McpStdioServerOptions("test-server", "1.0.0"),
            input,
            output).RunAsync();

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        using var response = JsonDocument.Parse(lines[0]);
        Assert.Equal(-32002, response.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }
}
