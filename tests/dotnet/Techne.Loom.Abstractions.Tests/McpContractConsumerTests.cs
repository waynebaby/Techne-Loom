using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.Mcp;
using Techne.Loom.Common.Runtime;
using Techne.Loom.SkillOrchestrator.Analysis;
using Techne.Loom.SkillOrchestrator.Models;
using Techne.Loom.SkillOrchestrator.Runtime;

namespace Techne.Loom.Abstractions.Tests;

public sealed class McpContractConsumerTests
{
    [Fact]
    public void MovedContractsAreDefinedByAbstractionsAssembly()
    {
        var assembly = typeof(UserInputContract).Assembly;
        Assert.All(new[]
        {
            typeof(IMcpTool), typeof(McpToolDefinition), typeof(McpTextContent), typeof(McpToolResult),
            typeof(McpWorkflowResumeEnvelope), typeof(McpStdioServerOptions), typeof(McpRuntimeBinding),
            typeof(LoomRuntimeProduct), typeof(SkillResumeEnvelope), typeof(SkillStatusPayload),
            typeof(SkillProgressPayload), typeof(SkillAskUserPayload), typeof(SkillBoundaryPayload),
            typeof(SkillResultPayload), typeof(SkillErrorPayload), typeof(SkillWorkflowAnalysisReport),
            typeof(SkillWorkflowDataflowReport), typeof(CommandStreamChunk), typeof(McpAppHostConfigurationGenerationResult),
        }, type => Assert.Same(assembly, type.Assembly));
    }

    [Fact]
    public async Task ExternalConsumerCanImplementMcpToolContract()
    {
        var tool = new ConsumerTool();
        using var argsDocument = JsonDocument.Parse("{\"request_id\":\"req-1\"}");
        var result = await tool.InvokeAsync(argsDocument.RootElement);

        Assert.Equal("consumer.echo", tool.Definition.Name);
        Assert.False(result.IsError);
        Assert.Equal("req-1", Assert.Single(result.Content).Text);
    }

    [Fact]
    public void McpAndSoEnvelopesKeepTheirWireFields()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mcp = new McpWorkflowResumeEnvelope("continue", "corr-1", new Dictionary<string, object?> { ["ok"] = true }, "result-1");
        using var mcpJson = JsonDocument.Parse(JsonSerializer.Serialize(mcp, options));
        Assert.Equal("continue", mcpJson.RootElement.GetProperty("transition_id").GetString());
        Assert.Equal("corr-1", mcpJson.RootElement.GetProperty("correlation_key").GetString());
        Assert.Equal("result-1", mcpJson.RootElement.GetProperty("result_id").GetString());

        var so = new SkillResumeEnvelope("continue", "corr-2", null, "result-2", "failed", "wait-1");
        using var soJson = JsonDocument.Parse(JsonSerializer.Serialize(so, options));
        Assert.Equal("failed", soJson.RootElement.GetProperty("execution_error").GetString());
        Assert.Equal("wait-1", soJson.RootElement.GetProperty("wait_id").GetString());
        Assert.False(soJson.RootElement.TryGetProperty("transitionId", out _));
    }

    private sealed class ConsumerTool : IMcpTool
    {
        public McpToolDefinition Definition { get; } = new(
            "consumer.echo",
            "Echo the request id.",
            JsonDocument.Parse("{\"type\":\"object\"}").RootElement.Clone());

        public Task<McpToolResult> InvokeAsync(JsonElement arguments, CancellationToken ct = default)
        {
            var requestId = arguments.GetProperty("request_id").GetString() ?? string.Empty;
            return Task.FromResult(new McpToolResult([new McpTextContent("text", requestId)]));
        }
    }
}
