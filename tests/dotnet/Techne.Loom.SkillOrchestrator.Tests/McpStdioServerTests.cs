using System.Text.Json;
using Techne.Loom.Common.Mcp;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpStdioServerTests
{
    [Fact]
    public async Task Server_HandlesInitializeListCallAndNotificationsOverNewlineDelimitedJson()
    {
        var registry = new McpToolRegistry();
        registry.Register(new EchoTool());
        using var input = new StringReader(string.Join(
            Environment.NewLine,
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test-client\",\"version\":\"1.0.0\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"test_echo\",\"arguments\":{\"value\":\"ok\"}}}"));
        using var output = new StringWriter();
        var server = new McpStdioServer(
            registry,
            new McpStdioServerOptions("test-server", "1.0.0"),
            input,
            output);

        await server.RunAsync();

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        using var initialize = JsonDocument.Parse(lines[0]);
        Assert.Equal("2.0", initialize.RootElement.GetProperty("jsonrpc").GetString());
        Assert.Equal("2025-06-18", initialize.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString());
        Assert.Equal("test-server", initialize.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());

        using var list = JsonDocument.Parse(lines[1]);
        Assert.Equal("test_echo", list.RootElement.GetProperty("result").GetProperty("tools")[0].GetProperty("name").GetString());

        using var call = JsonDocument.Parse(lines[2]);
        Assert.False(call.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var text = call.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();
        using var toolPayload = JsonDocument.Parse(text!);
        Assert.Equal("ok", toolPayload.RootElement.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Server_RejectsToolAccessBeforeInitialize()
    {
        var registry = new McpToolRegistry();
        registry.Register(new EchoTool());
        using var input = new StringReader("{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/list\"}");
        using var output = new StringWriter();

        await new McpStdioServer(
            registry,
            new McpStdioServerOptions("test-server", "1.0.0"),
            input,
            output).RunAsync();

        using var response = JsonDocument.Parse(output.ToString());
        Assert.Equal(-32002, response.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Server_RejectsDuplicateInitializeAndInvalidArguments()
    {
        using var input = new StringReader(string.Join(
            Environment.NewLine,
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test-client\",\"version\":\"1.0.0\"}}}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test-client\",\"version\":\"1.0.0\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{\"name\":\"test_echo\",\"arguments\":[]}}"));
        using var output = new StringWriter();
        var registry = new McpToolRegistry();
        registry.Register(new EchoTool());

        await new McpStdioServer(registry, new McpStdioServerOptions("test-server", "1.0.0"), input, output).RunAsync();

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        using var duplicateInitialize = JsonDocument.Parse(lines[1]);
        Assert.Equal(-32600, duplicateInitialize.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        using var invalidArguments = JsonDocument.Parse(lines[2]);
        Assert.Equal(-32602, invalidArguments.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public void WorkflowToolSet_ExposesFragmentFirstSessionlessTools()
    {
        var definitions = WorkflowMcpToolSet.Create("so").ListDefinitions();
        var names = definitions.Select(static definition => definition.Name).ToArray();

        Assert.Contains("so_inspect_workflow_fragment", names);
        Assert.Contains("so_run_workflow", names);
        Assert.Contains("so_resume_workflow", names);
        Assert.Contains("so_get_workflow_status", names);
        Assert.DoesNotContain("so_inspect_workflow", names);
    }

    private sealed class EchoTool : IMcpTool
    {
        public McpToolDefinition Definition { get; } = new(
            "test_echo",
            "Return the supplied value.",
            JsonDocument.Parse("{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\"}},\"required\":[\"value\"]}").RootElement.Clone());

        public Task<McpToolResult> InvokeAsync(JsonElement arguments, CancellationToken ct = default)
        {
            var value = arguments.TryGetProperty("value", out var property) ? property.GetString() : null;
            return Task.FromResult(McpToolResults.Json(new { value }));
        }
    }
}
