using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpFirstGovernedRouteTests
{
    [Fact]
    public void SelfBootstrapTemplate_RequiresMcpUseBeforeGuideCapture()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(repoRoot, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "so-template.json");
        var workflow = WorkflowJsonSerializer.Deserialize(File.ReadAllText(workflowFile));

        Assert.NotNull(workflow.Validation);
        Assert.Contains("gate.bootstrap_mcp_ready", workflow.Validation!.Gates.Keys);
        var runtime = Assert.IsType<CommandTransition>(workflow.Nodes["transition.reacquire_runtime"]);
        Assert.Equal("state.mcp_preflight", runtime.TargetNodeId);
        Assert.True(Convert.ToBoolean(runtime.Command.Parameters!["mcpPreflightExempt"]));
        Assert.True(Convert.ToBoolean(runtime.Command.Parameters["runtimePreflight"]));

        var mcp = Assert.IsType<CommandTransition>(workflow.Nodes["transition.start_mcp"]);
        Assert.Equal(WorkflowStepKind.McpCall, mcp.StepKind);
        Assert.Equal("state.capture_guide", mcp.TargetNodeId);
        Assert.Equal("so_inspect_workflow_fragment", mcp.Command.Name);
        var parameters = mcp.Command.Parameters!;
        Assert.Equal("dotnet so.dll mcp stdio", Convert.ToString(parameters["runtimeCommand"]));
        Assert.Equal("stdio", Convert.ToString(parameters["transport"]));
        Assert.Equal("so_inspect_workflow_fragment", Convert.ToString(parameters["requiredTool"]));
        Assert.Equal("current_external_workflow_copy", Convert.ToString(parameters["workflowFileInput"]));
        Assert.Equal("mcp_startup_evidence", Convert.ToString(parameters["resumeOutputKey"]));
        Assert.True(Convert.ToBoolean(parameters["mcpFirst"]));
        Assert.Equal("assets/agents/loom-skill-enhancement-mcp-startup.agent.md", Convert.ToString(parameters["subagentRelativePath"]));
        Assert.Contains("mcp_startup_evidence", mcp.PublishesOutputFamilies ?? []);
        var outputBindings = Assert.IsAssignableFrom<IDictionary<string, object?>>(parameters["outputBindings"]);
        Assert.Equal("$result", Convert.ToString(outputBindings["mcp_startup_evidence"]));
        Assert.Contains("so_inspect_workflow_fragment", Convert.ToString(parameters["skillHint"]));
        Assert.Contains("mcp_startup_evidence", workflow.Validation.Gates["gate.bootstrap_mcp_ready"].PassExpression!.Source);
        var captureGuide = Assert.IsType<CommandTransition>(workflow.Nodes["transition.capture_guide"]);
        Assert.Contains("mcp_startup_evidence", captureGuide.GuardExpression.Source);
        Assert.Contains("fragment_bounded", captureGuide.SucceedExpression.Source);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Techne.Loom.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
