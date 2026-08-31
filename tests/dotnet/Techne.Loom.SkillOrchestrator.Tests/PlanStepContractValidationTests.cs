using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class PlanStepContractValidationTests
{
    [Fact]
    public void PlanStepContract_RoundTripsAndValidates()
    {
        var transition = new CommandTransition
        {
            Id = "transition.plan",
            StepKind = WorkflowStepKind.Plan,
            Plan = new PlanStepContract
            {
                InputPaths = ["objective"],
                ResultFile = "plan.result.json",
                RequiredEvidence = ["plan.evidence"],
                WeaveBackTargetNodeId = "state.done",
            },
        };

        var instance = new WorkflowInstance
        {
            InstanceId = "plan-contract",
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["state.done"] = new StateNode { Id = "state.done" },
                [transition.Id] = transition,
            },
        };
        var roundTrip = WorkflowJsonSerializer.Deserialize(WorkflowJsonSerializer.Serialize(instance));

        var roundTripTransition = Assert.IsType<CommandTransition>(roundTrip.Nodes[transition.Id]);
        Assert.NotNull(roundTripTransition.Plan);
        Assert.Equal(["objective"], roundTripTransition.Plan!.InputPaths);
        Assert.Equal("plan.result.json", roundTripTransition.Plan.ResultFile);
        Assert.Equal("state.done", roundTripTransition.Plan.WeaveBackTargetNodeId);
        Assert.Empty(PlanStepContractValidator.Validate(roundTrip));
    }

    [Fact]
    public void PlanStepContract_RejectsMissingAndInvalidFields()
    {
        var transition = new CommandTransition
        {
            Id = "transition.invalid-plan",
            StepKind = WorkflowStepKind.Plan,
            Plan = new PlanStepContract
            {
                InputPaths = [""],
                ResultFile = "",
                RequiredEvidence = [""],
                ApplyMode = "manual",
            },
        };

        var diagnostics = PlanStepContractValidator.Validate([transition]);

        Assert.Equal(5, diagnostics.Count);
        Assert.Contains(diagnostics, item => item.Location.EndsWith("/inputPaths", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Location.EndsWith("/resultFile", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Location.EndsWith("/requiredEvidence", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Location.EndsWith("/applyMode", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Location.EndsWith("/targetNodeId", StringComparison.Ordinal));
    }

    [Fact]
    public void PlanStepContract_RejectsPlanWithoutAnyTarget()
    {
        var transition = new CommandTransition
        {
            Id = "transition.plan",
            StepKind = WorkflowStepKind.Plan,
            Plan = new PlanStepContract
            {
                InputPaths = ["objective"],
                ResultFile = "plan.result.json",
                RequiredEvidence = ["plan.evidence"],
            },
        };

        var diagnostics = PlanStepContractValidator.Validate([transition]);

        var diagnostic = Assert.Single(diagnostics);
        Assert.EndsWith("/targetNodeId", diagnostic.Location, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanStepContract_RejectsMissingWeaveBackTargetNode()
    {
        var transition = new CommandTransition
        {
            Id = "transition.plan",
            StepKind = WorkflowStepKind.Plan,
            Plan = new PlanStepContract
            {
                InputPaths = ["objective"],
                ResultFile = "plan.result.json",
                RequiredEvidence = ["plan.evidence"],
                WeaveBackTargetNodeId = "state.missing",
            },
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "plan-target",
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["state.start"] = new StateNode { Id = "state.start" },
                [transition.Id] = transition,
            },
        };

        var diagnostics = PlanStepContractValidator.Validate(instance);

        var diagnostic = Assert.Single(diagnostics);
        Assert.EndsWith("/weaveBackTargetNodeId", diagnostic.Location, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanContract_IsNotAdvertisedForExpressionOrRefinementNodes()
    {
        var schema = WorkflowSchemaDemoExporter.CreateSchemaContract();

        Assert.DoesNotContain("plan", schema.NodeFields["expr"]);
        Assert.DoesNotContain("plan", schema.NodeFields["tbr"]);
        Assert.Contains("plan", schema.NodeFields["command"]);
    }
}