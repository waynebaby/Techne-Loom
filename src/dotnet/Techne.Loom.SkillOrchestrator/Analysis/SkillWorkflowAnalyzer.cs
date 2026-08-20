using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Analysis;

public sealed class SkillWorkflowAnalyzer
{
    public SkillWorkflowAnalysisReport Analyze(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var states = instance.GetStateNodes();
        var transitions = instance.GetTransitionNodes();
        var edges = BuildEdges(states, transitions);
        var branches = FindBranches(states, transitions);
        var loops = FindLoops(edges);
        var requestedInputFields = FindRequestedInputFields(transitions.Values);
        var publishedOutputFamilies = transitions.Values
            .SelectMany(static transition => transition.PublishesOutputFamilies ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToList();
        var userSeams = transitions.Values
            .Where(static transition => transition.StepKind == WorkflowStepKind.AskUser || string.Equals(transition.OwnedInputMode, "user", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static transition => transition.Id, StringComparer.Ordinal)
            .Select(static transition => new WorkflowSeamAnalysis(transition.Id, transition.StepKind, transition.OwnedInputMode))
            .ToList();
        var runtimeSeams = transitions.Values
            .Where(static transition => transition.StepKind == WorkflowStepKind.WaitResume || string.Equals(transition.OwnedInputMode, "runtime", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static transition => transition.Id, StringComparer.Ordinal)
            .Select(static transition => new WorkflowSeamAnalysis(transition.Id, transition.StepKind, transition.OwnedInputMode))
            .ToList();
        var gateIds = (instance.Validation?.Gates.Keys ?? Enumerable.Empty<string>())
            .Concat(transitions.Values.SelectMany(static transition => transition.SatisfiesGateIds ?? []))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToList();
        var declaredUserOwnedFields = instance.Validation?.DeclaredUserOwnedFields
            ?.Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToList() ?? [];
        var reservedRuntimeOwnedFields = instance.Validation?.ReservedRuntimeOwnedFields
            ?.Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToList() ?? [];
        var stepKindCounts = transitions.Values
            .GroupBy(static transition => transition.StepKind)
            .OrderBy(static group => group.Key)
            .ToDictionary(static group => group.Key, static group => group.Count());
        var nodeArtifactMap = BuildNodeArtifactMap(states.Values, transitions.Values);

        return new SkillWorkflowAnalysisReport(
            instance.InstanceId,
            states.Count,
            transitions.Count,
            stepKindCounts,
            requestedInputFields,
            publishedOutputFamilies,
            branches,
            loops,
            userSeams,
            runtimeSeams,
            gateIds,
            declaredUserOwnedFields,
            reservedRuntimeOwnedFields,
            nodeArtifactMap,
            loops.Count > 0 && branches.Count > 0);
    }

    private static IReadOnlyList<WorkflowNodeArtifactMapping> BuildNodeArtifactMap(
        IEnumerable<StateNode> states,
        IEnumerable<TransitionBase> transitions)
    {
        var stateMappings = states
            .OrderBy(static state => state.Id, StringComparer.Ordinal)
            .Select(static state => new WorkflowNodeArtifactMapping(state.Id, "state", [], [], []));
        var transitionMappings = transitions
            .OrderBy(static transition => transition.Id, StringComparer.Ordinal)
            .Select(static transition => new WorkflowNodeArtifactMapping(
                transition.Id,
                "transition",
                string.IsNullOrWhiteSpace(transition.OutputPath) ? [] : [transition.OutputPath],
                transition.PublishesOutputFamilies?.OrderBy(static value => value, StringComparer.Ordinal).ToList() ?? [],
                transition.SatisfiesGateIds?.OrderBy(static value => value, StringComparer.Ordinal).ToList() ?? []));

        return stateMappings.Concat(transitionMappings).ToList();
    }

    private static IReadOnlyList<WorkflowGraphEdge> BuildEdges(
        IReadOnlyDictionary<string, StateNode> states,
        IReadOnlyDictionary<string, TransitionBase> transitions)
    {
        var edges = new List<WorkflowGraphEdge>();
        foreach (var state in states.Values)
        {
            foreach (var transitionId in state.Groups.SelectMany(static group => group.TransitionIds))
            {
                if (!transitions.TryGetValue(transitionId, out var transition) || string.IsNullOrWhiteSpace(transition.TargetNodeId))
                {
                    continue;
                }

                if (states.ContainsKey(transition.TargetNodeId))
                {
                    edges.Add(new WorkflowGraphEdge(state.Id, transition.TargetNodeId, transition.Id));
                }
            }
        }

        return edges;
    }

    private static IReadOnlyList<WorkflowBranchAnalysis> FindBranches(
        IReadOnlyDictionary<string, StateNode> states,
        IReadOnlyDictionary<string, TransitionBase> transitions)
    {
        var branches = new List<WorkflowBranchAnalysis>();
        foreach (var state in states.Values.OrderBy(static state => state.Id, StringComparer.Ordinal))
        {
            foreach (var group in state.Groups.OrderBy(static group => group.Id, StringComparer.Ordinal))
            {
                var groupTransitions = group.TransitionIds
                    .Where(transitions.ContainsKey)
                    .OrderBy(static transitionId => transitionId, StringComparer.Ordinal)
                    .ToList();
                var hasConditionalTransition = groupTransitions.Any(transitionId =>
                    transitions[transitionId].StepKind == WorkflowStepKind.ConditionBranch ||
                    !string.Equals(transitions[transitionId].GuardExpression, "true", StringComparison.OrdinalIgnoreCase));
                var guardExpressions = groupTransitions
                    .Select(transitionId => transitions[transitionId].GuardExpression.Source)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static value => value, StringComparer.Ordinal)
                    .ToList();

                if (groupTransitions.Count > 1 || hasConditionalTransition)
                {
                    branches.Add(new WorkflowBranchAnalysis(state.Id, group.Id, groupTransitions, guardExpressions, groupTransitions.Count > 2));
                }
            }
        }

        return branches;
    }

    private static IReadOnlyList<WorkflowLoopAnalysis> FindLoops(IReadOnlyList<WorkflowGraphEdge> edges)
    {
        var adjacency = edges
            .GroupBy(static edge => edge.SourceStateId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Select(static edge => edge.TargetStateId).ToList(), StringComparer.Ordinal);
        var loops = new List<WorkflowLoopAnalysis>();

        foreach (var edge in edges.OrderBy(static edge => edge.TransitionId, StringComparer.Ordinal))
        {
            var isSelfLoop = string.Equals(edge.SourceStateId, edge.TargetStateId, StringComparison.Ordinal);
            if (isSelfLoop || HasPath(edge.TargetStateId, edge.SourceStateId, adjacency, []))
            {
                loops.Add(new WorkflowLoopAnalysis(edge.SourceStateId, edge.TargetStateId, edge.TransitionId, isSelfLoop));
            }
        }

        return loops;
    }

    private static bool HasPath(
        string currentStateId,
        string targetStateId,
        IReadOnlyDictionary<string, List<string>> adjacency,
        HashSet<string> visited)
    {
        if (!visited.Add(currentStateId))
        {
            return false;
        }

        if (!adjacency.TryGetValue(currentStateId, out var nextStates))
        {
            return false;
        }

        foreach (var nextStateId in nextStates)
        {
            if (string.Equals(nextStateId, targetStateId, StringComparison.Ordinal) || HasPath(nextStateId, targetStateId, adjacency, visited))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> FindRequestedInputFields(IEnumerable<TransitionBase> transitions)
    {
        return transitions
            .OfType<CommandTransition>()
            .SelectMany(static transition => TryGetRequiredInputs(transition.Command.Parameters))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<string> TryGetRequiredInputs(Dictionary<string, object?>? parameters)
    {
        if (parameters is null || !parameters.TryGetValue("requiredInputs", out var value) || value is null)
        {
            yield break;
        }

        if (value is IEnumerable<object?> items)
        {
            foreach (var item in items)
            {
                var input = Convert.ToString(item);
                if (!string.IsNullOrWhiteSpace(input))
                {
                    yield return input;
                }
            }
        }
    }

    private sealed record WorkflowGraphEdge(string SourceStateId, string TargetStateId, string TransitionId);
}