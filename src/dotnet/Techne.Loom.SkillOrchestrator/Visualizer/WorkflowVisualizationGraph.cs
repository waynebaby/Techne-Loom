using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Visualizer;

internal static class WorkflowVisualizationGraph
{
    public static IReadOnlyList<WorkflowVisualizationEdge> GetEdges(WorkflowInstance instance)
    {
        var states = instance.Nodes.Values
            .OfType<StateNode>()
            .OrderBy(static state => state.Id, StringComparer.Ordinal)
            .ToList();
        var stateLookup = states.ToDictionary(static state => state.Id, StringComparer.Ordinal);
        var transitions = instance.Nodes.Values
            .OfType<TransitionBase>()
            .ToDictionary(static transition => transition.Id, StringComparer.Ordinal);
        var edges = new List<WorkflowVisualizationEdge>();

        foreach (var state in states)
        {
            foreach (var group in state.Groups)
            {
                foreach (var transitionId in group.TransitionIds)
                {
                    if (!transitions.TryGetValue(transitionId, out var transition))
                    {
                        continue;
                    }

                    edges.Add(CreateEdge(state, transition, stateLookup));
                }
            }
        }

        return edges;
    }

    private static WorkflowVisualizationEdge CreateEdge(
        StateNode sourceState,
        TransitionBase transition,
        IReadOnlyDictionary<string, StateNode> stateLookup)
    {
        var targetStateName = !string.IsNullOrWhiteSpace(transition.TargetNodeId) && stateLookup.TryGetValue(transition.TargetNodeId, out var targetState)
            ? targetState.Name
            : transition.TargetNodeId ?? string.Empty;

        return new WorkflowVisualizationEdge(
            sourceState.Id,
            sourceState.Name,
            sourceState.WorkflowPhase,
            transition.Id,
            transition.Name,
            transition.TargetNodeId,
            targetStateName,
            transition.GuardExpression.Source,
            transition.StepKind,
            transition.OwnedInputMode);
    }
}
