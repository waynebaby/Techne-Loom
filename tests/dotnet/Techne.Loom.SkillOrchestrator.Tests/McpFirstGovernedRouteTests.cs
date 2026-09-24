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
        Assert.Equal(WorkflowStepKind.WaitResume, mcp.StepKind);
        Assert.Equal("mcp_startup_evidence", mcp.OutputPath);
        Assert.Equal("mcp_startup_evidence", Assert.Single(mcp.PublishesOutputFamilies!));
        Assert.Equal("gate.bootstrap_mcp_ready", Assert.Single(mcp.SatisfiesGateIds!));
        Assert.Equal("workflow.inspectGovernanceEntry", mcp.Command.Name);
        Assert.True(Convert.ToBoolean(mcp.Command.Parameters!["mcpFirst"]));
        Assert.False(Convert.ToBoolean(mcp.Command.Parameters["mcpRequired"]));
        Assert.Equal("current_external_workflow_copy", Convert.ToString(mcp.Command.Parameters["workflowFileInput"]));
        Assert.Equal("mcp_startup_evidence", Convert.ToString(mcp.Command.Parameters["resumeOutputKey"]));
        Assert.Equal("canonical", Convert.ToString(mcp.Command.Parameters["projectionMode"]));
        Assert.Equal("descriptor_owned_mcp_or_cli", Convert.ToString(mcp.Command.Parameters["runtimeCommand"]));
        Assert.Equal("reuse_matching_mcp_then_agent_adhoc_mcp_then_cli", Convert.ToString(mcp.Command.Parameters["transportSelectionPolicy"]));
        Assert.Equal("operation_id", Convert.ToString(mcp.Command.Parameters["operationIdInput"]));
        var requiredInputs = Assert.IsAssignableFrom<IEnumerable<object?>>(mcp.Command.Parameters["requiredInputs"]);
        Assert.Contains("operation_id", requiredInputs);
        Assert.Contains("mcp_startup_evidence.operation_id", requiredInputs);
        var payloadMatches = Assert.IsAssignableFrom<IDictionary<string, object?>>(mcp.Command.Parameters["mustMatchPayloadInputs"]);
        Assert.Equal("mcp_startup_evidence.operation_id", Convert.ToString(payloadMatches["operation_id"]));
        var bindings = Assert.IsAssignableFrom<IDictionary<string, object?>>(mcp.Command.Parameters["outputBindings"]);
        Assert.Equal("$result", Convert.ToString(bindings["mcp_startup_evidence"]));

        var captureGuide = Assert.IsType<CommandTransition>(workflow.Nodes["transition.capture_guide"]);
        Assert.Contains("mcp_startup_evidence", captureGuide.GuardExpression.Source);
        Assert.Contains("mcp_startup_evidence", captureGuide.SucceedExpression.Source);
        Assert.Contains("resolved_guide_surface_ref", captureGuide.SucceedExpression.Source);
        var captureRequiredInputs = Assert.IsAssignableFrom<IEnumerable<object?>>(captureGuide.Command.Parameters!["requiredInputs"]);
        Assert.Contains("operation_id", captureRequiredInputs);
        var capturePayloadMatches = Assert.IsAssignableFrom<IDictionary<string, object?>>(captureGuide.Command.Parameters["mustMatchPayloadInputs"]);
        Assert.Equal("operation_id", Convert.ToString(capturePayloadMatches["operation_id"]));
        Assert.DoesNotContain("context.Get<string>(\"operation_id\") == context.Get<string>(\"mcp_startup_evidence.operation_id\")", captureGuide.GuardExpression.Source);
        Assert.DoesNotContain("context.Get<string>(\"operation_id\") == context.Get<string>(\"mcp_startup_evidence.operation_id\")", captureGuide.SucceedExpression.Source);
    }

    [Theory]
    [InlineData("mcp_stdio", null, "operation-1", true)]
    [InlineData("cli", "mcp_tool_unavailable", "operation-1", true)]
    [InlineData("cli", null, "operation-1", true)]
    [InlineData("stdio", null, "operation-1", false)]
    [InlineData("mcp_stdio", null, "operation-2", false)]
    public void BootstrapGateAcceptsOptionalMcpOrDirectCli(string transport, string? fallbackReason, string rootOperationId, bool expected)
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(repoRoot, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "so-template.json");
        var workflow = WorkflowJsonSerializer.Deserialize(File.ReadAllText(workflowFile));
        var gate = workflow.Validation!.Gates["gate.bootstrap_mcp_ready"];
        var evidence = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["transport"] = transport,
            ["runtime_version"] = "0.3.283-beta",
            ["launch_descriptor"] = "descriptor",
            ["operation_id"] = "operation-1",
            ["workflow_file"] = workflowFile,
            ["workflow_sha256"] = "workflow-hash",
            ["fragment_bounded"] = true,
            ["result_sha256"] = "result-hash",
        };
        if (transport == "mcp_stdio")
        {
            evidence["initialized"] = true;
            evidence["tool_called"] = true;
            evidence["tool_name"] = "so_inspect_workflow_fragment";
        }
        if (fallbackReason is not null)
        {
            evidence["fallback_reason"] = fallbackReason;
        }

        var context = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["operation_id"] = rootOperationId,
            ["mcp_startup_evidence"] = evidence,
        };

        var passExpression = gate.PassExpression?.Source ?? throw new InvalidOperationException("The governed route gate requires a pass expression.");
        Assert.Equal(expected, new CSharpExpressionEvaluator().EvaluateBoolean(passExpression, context));
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
