using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpFirstGovernedRouteTests
{
    [Fact]
    public void SelfBootstrapTemplate_UsesReleased03282McpFirstShape()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(repoRoot, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "so-template.json");
        var workflow = WorkflowJsonSerializer.Deserialize(File.ReadAllText(workflowFile));

        var policy = Assert.IsType<WorkflowGovernanceEntryContract>(workflow.Validation?.GovernanceEntry);
        Assert.Equal("mcp_stdio", policy.PreferredTransport);
        Assert.Equal("mcp_startup_evidence", policy.EvidenceFamily);
        Assert.Contains("mcp_transport_unavailable", policy.CliFallbackReasons);
        Assert.Contains("gate.bootstrap_mcp_ready", workflow.Validation!.Gates.Keys);
        Assert.DoesNotContain("gate.bootstrap_governance_entry_ready", workflow.Validation.Gates.Keys);

        var runtime = Assert.IsType<CommandTransition>(workflow.Nodes["transition.reacquire_runtime"]);
        Assert.Equal("state.mcp_preflight", runtime.TargetNodeId);
        Assert.True(Convert.ToBoolean(runtime.Command.Parameters!["mcpPreflightExempt"]));
        Assert.True(Convert.ToBoolean(runtime.Command.Parameters["runtimePreflight"]));

        var mcp = Assert.IsType<CommandTransition>(workflow.Nodes["transition.start_mcp"]);
        Assert.Equal(WorkflowStepKind.McpCall, mcp.StepKind);
        Assert.Equal("mcp_startup_evidence", mcp.OutputPath);
        Assert.Equal("mcp_startup_evidence", Assert.Single(mcp.PublishesOutputFamilies!));
        Assert.Equal("gate.bootstrap_mcp_ready", Assert.Single(mcp.SatisfiesGateIds!));
        Assert.Equal("so_inspect_workflow_fragment", mcp.Command.Name);
        Assert.True(Convert.ToBoolean(mcp.Command.Parameters!["mcpFirst"]));
        Assert.Equal("stdio", Convert.ToString(mcp.Command.Parameters["transport"]));
        Assert.Equal("so_inspect_workflow_fragment", Convert.ToString(mcp.Command.Parameters["requiredTool"]));
        Assert.Equal("current_external_workflow_copy", Convert.ToString(mcp.Command.Parameters["workflowFileInput"]));
        Assert.Equal("mcp_startup_evidence", Convert.ToString(mcp.Command.Parameters["resumeOutputKey"]));
        Assert.Equal("canonical", Convert.ToString(mcp.Command.Parameters["projectionMode"]));
        Assert.Equal("dotnet so.dll mcp stdio", Convert.ToString(mcp.Command.Parameters["runtimeCommand"]));
        var bindings = Assert.IsAssignableFrom<IDictionary<string, object?>>(mcp.Command.Parameters["outputBindings"]);
        Assert.Equal("$result", Convert.ToString(bindings["mcp_startup_evidence"]));

        var captureGuide = Assert.IsType<CommandTransition>(workflow.Nodes["transition.capture_guide"]);
        Assert.Contains("mcp_startup_evidence", captureGuide.GuardExpression.Source);
        Assert.Contains("mcp_startup_evidence", captureGuide.SucceedExpression.Source);
        Assert.Contains("resolved_guide_surface_ref", captureGuide.SucceedExpression.Source);
        Assert.DoesNotContain(workflow.Nodes.Keys, id => id == "transition.inspect_governance_entry_cli");
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
