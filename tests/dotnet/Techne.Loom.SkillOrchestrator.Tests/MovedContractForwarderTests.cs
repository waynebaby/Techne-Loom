using Techne.Loom.Abstractions.Ask;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.Mcp;
using Techne.Loom.Common.Runtime;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Analysis;
using Techne.Loom.SkillOrchestrator.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class MovedContractForwarderTests
{
    [Fact]
    public void CommonMcpContractsForwardToAbstractions()
    {
        var common = typeof(AskScopedStandaloneSessionService).Assembly;
        var abstractions = typeof(UserInputContract).Assembly;
        var forwarded = common.GetForwardedTypes();
        Assert.All(new[]
        {
            typeof(IMcpTool), typeof(McpToolDefinition), typeof(McpTextContent), typeof(McpToolResult),
            typeof(McpWorkflowResumeEnvelope), typeof(McpStdioServerOptions), typeof(McpRuntimeBinding),
            typeof(LoomRuntimeProduct),
        }, type =>
        {
            Assert.Same(abstractions, type.Assembly);
            Assert.Contains(type, forwarded);
        });
    }

    [Fact]
    public void SoPublicContractsForwardToAbstractions()
    {
        var so = typeof(SkillWorkflowDataflowAnalyzer).Assembly;
        var abstractions = typeof(UserInputContract).Assembly;
        var forwarded = so.GetForwardedTypes();
        Assert.All(new[]
        {
            typeof(SkillWorkflowAnalysisReport), typeof(WorkflowBranchAnalysis), typeof(WorkflowLoopAnalysis),
            typeof(WorkflowSeamAnalysis), typeof(WorkflowNodeArtifactMapping), typeof(SkillWorkflowDataflowReport),
            typeof(WorkflowEmitterKind), typeof(WorkflowTransitionDataflow), typeof(WorkflowDataflowIssue),
            typeof(CommandStreamChunk), typeof(CommandStreamStart), typeof(CommandStreamEnd),
            typeof(McpAppHostConfigurationGenerationResult),
        }, type =>
        {
            Assert.Same(abstractions, type.Assembly);
            Assert.Contains(type, forwarded);
            Assert.Same(type, Type.GetType($"{type.FullName}, {so.GetName().Name}", throwOnError: true));
        });
    }
}
