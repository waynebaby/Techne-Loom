using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.SkillOrchestrator.Validation;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowFragmentGraphTests
{
    [Fact]
    public void RemovedTransitionInvalidatesPreviousConsumersButLeavesOtherGroupsAlone()
    {
        var draft = CreateDraft();
        var previous = WorkflowFragmentGraph.Create(draft);
        draft.Nodes.Remove("transition");
        ((StateNode)draft.Nodes["start"]).Groups[0].TransitionIds.Clear();
        var candidate = WorkflowFragmentGraph.Create(draft);

        var impact = WorkflowFragmentGraph.Impact(previous, candidate,
            [new(WorkflowFragmentKind.Transition, "transition")], hasUnindexedDependencies: false);

        Assert.Contains(new(WorkflowFragmentKind.Group, "start", "first"), impact);
        Assert.DoesNotContain(new(WorkflowFragmentKind.Group, "start", "second"), impact);
    }

    [Fact]
    public void GroupIdentityRemainsStableWhenOrderedGroupsMove()
    {
        var draft = CreateDraft();
        var id = new WorkflowFragmentId(WorkflowFragmentKind.Group, "start", "first");
        var previous = WorkflowFragmentGraph.Create(draft);
        ((StateNode)draft.Nodes["start"]).Groups.Reverse();
        var candidate = WorkflowFragmentGraph.Create(draft);

        Assert.Equal("/nodes/start/groups/0", previous.Locations[id]);
        Assert.Equal("/nodes/start/groups/1", candidate.Locations[id]);
    }

    [Fact]
    public void UnknownDependencyEscalatesToAllKnownFragments()
    {
        var draft = CreateDraft();
        var previous = WorkflowFragmentGraph.Create(draft);
        ((StateNode)draft.Nodes["start"]).Groups[0].TransitionIds.Add("missing");
        var candidate = WorkflowFragmentGraph.Create(draft);
        var impact = WorkflowFragmentGraph.Impact(previous, candidate,
            [new(WorkflowFragmentKind.Transition, "transition")], hasUnindexedDependencies: false);

        Assert.True(impact.SetEquals(candidate.Locations.Keys));
    }

    [Fact]
    public void GlobalChangeWithNoFragmentIdInvalidatesAllEvidence()
    {
        var graph = WorkflowFragmentGraph.Create(CreateDraft());

        var impact = WorkflowFragmentGraph.Impact(graph, graph, [], hasUnindexedDependencies: true);

        Assert.True(impact.SetEquals(graph.Locations.Keys));
    }

    [Fact]
    public void RouteChangesInvalidateItsTerminalTransitionAndGateCompatibilityDeclarations()
    {
        var draft = CreateDraft();
        draft.Validation = new WorkflowValidationContract
        {
            Gates = new() { ["final"] = new() },
            Routes = new() { ["success"] = new() { RequiredTerminalGateIds = ["final"] } },
        };
        draft.Nodes["transition"] = new CommandTransition
        {
            Id = "transition",
            TargetNodeId = "end",
            Command = new CommandInvocation
            {
                Parameters = new()
                {
                    ["terminalRoutes"] = new[] { "success" },
                    ["satisfiesGateIds"] = new[] { "final" },
                },
            },
        };
        var graph = WorkflowFragmentGraph.Create(draft);
        var route = new WorkflowFragmentId(WorkflowFragmentKind.Route, "success");
        var impact = WorkflowFragmentGraph.Impact(graph, graph, [route], hasUnindexedDependencies: false);

        Assert.Contains(new(WorkflowFragmentKind.Transition, "transition"), impact);
        Assert.Contains(new(WorkflowFragmentKind.Gate, "final"), impact);
        Assert.DoesNotContain(new(WorkflowFragmentKind.Group, "start", "second"), impact);
    }

    private static WorkflowInstance CreateDraft()
    {
        return new WorkflowInstance
        {
            Nodes = new(StringComparer.Ordinal)
            {
                ["start"] = new StateNode
                {
                    Id = "start",
                    Groups =
                    [
                        new TransitionGroup { Id = "first", TransitionIds = ["transition"] },
                        new TransitionGroup { Id = "second" },
                    ],
                },
                ["end"] = new StateNode { Id = "end" },
                ["transition"] = new ExpressionTransition { Id = "transition", TargetNodeId = "end" },
            },
        };
    }
}
