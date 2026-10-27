using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.Mcp;
using Techne.Loom.Common.Runtime;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpForwarderCompletenessTests
{
    [Fact]
    public void EveryMovedCommonMcpContractForwardsToAbstractions()
    {
        var common = typeof(AskScopedStandaloneSessionService).Assembly;
        var abstractions = typeof(UserInputContract).Assembly;
        var forwarded = common.GetForwardedTypes();
        var contracts = new[]
        {
            typeof(McpToolInputException), typeof(McpToolDefinition), typeof(McpTextContent), typeof(McpToolResult),
            typeof(IMcpTool), typeof(McpServerInfo), typeof(McpDispatchStage), typeof(McpDispatchException),
            typeof(McpTransportUnavailableException), typeof(McpHandshakeUnsupportedException),
            typeof(McpToolUnavailableException), typeof(McpApplicationException), typeof(McpStdioServerOptions),
            typeof(McpSessionRegistration), typeof(McpSessionRegistrationResult), typeof(McpWorkflowOperationResult),
            typeof(McpWorkflowResumeEnvelope), typeof(LoomRuntimeProduct), typeof(McpRuntimeBinding),
        };

        Assert.All(contracts, type =>
        {
            Assert.Same(abstractions, type.Assembly);
            Assert.Contains(type, forwarded);
            Assert.Same(type, Type.GetType($"{type.FullName}, {common.GetName().Name}", throwOnError: true));
        });
    }
}
