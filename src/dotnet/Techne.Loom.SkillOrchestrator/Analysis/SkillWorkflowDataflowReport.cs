using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Analysis;

public sealed record SkillWorkflowDataflowReport(
    string InstanceId,
    IReadOnlyList<WorkflowTransitionDataflow> Transitions,
    IReadOnlyDictionary<string, IReadOnlyList<string>> GateRequiredOutputFamilies,
    IReadOnlyList<WorkflowDataflowIssue> Issues)
{
    public bool IsResolved => Issues.Count == 0;
}

public sealed record WorkflowTransitionDataflow(
    string TransitionId,
    WorkflowStepKind StepKind,
    IReadOnlyList<string> InputPaths,
    IReadOnlyList<string> PayloadPaths,
    string? ResumeOutputKey,
    string? OutputPath,
    string? ProjectionMode,
    IReadOnlyDictionary<string, string?> OutputBindings,
    IReadOnlyList<string> ProducedContextPaths,
    IReadOnlyList<string> PublishedOutputFamilies,
    IReadOnlyList<string> SatisfiedGateIds,
    IReadOnlyList<string> RouteNames,
    IReadOnlyList<string> UnresolvedOutputFamilies);

public sealed record WorkflowDataflowIssue(
    string? TransitionId,
    string? GateId,
    string OutputFamily,
    string Reason);

public sealed class SkillWorkflowDataflowAnalyzer
{
    public SkillWorkflowDataflowReport Analyze(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var transitionNodes = instance.GetTransitionNodes();
        var transitions = transitionNodes.Values
            .Select(BuildTransitionDataflow)
            .OrderBy(static transition => transition.TransitionId, StringComparer.Ordinal)
            .ToArray();
        var transitionsById = transitions.ToDictionary(
            static transition => transition.TransitionId,
            StringComparer.Ordinal);
        var reachability = FindReachability(instance);
        var reachableTransitionIds = reachability.TransitionIds;
        var initialContextPaths = instance.Context.Keys
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.Ordinal);
        var transitionSourceStates = BuildTransitionSourceStates(instance, reachability.StateIds);
        var guaranteedContextPaths = ComputeGuaranteedContextPaths(
            instance,
            transitionsById,
            reachableTransitionIds,
            transitionSourceStates,
            initialContextPaths);
        var issues = new List<WorkflowDataflowIssue>();

        foreach (var transition in transitions)
        {
            foreach (var family in transition.UnresolvedOutputFamilies)
            {
                issues.Add(new WorkflowDataflowIssue(
                    transition.TransitionId,
                    null,
                    family,
                    "Published output family has no concrete outputPath or outputBinding producer."));
            }

            if (!reachableTransitionIds.Contains(transition.TransitionId))
            {
                continue;
            }

            foreach (var family in transition.PublishedOutputFamilies.Where(family => !IsConcreteProducer(
                         transition,
                         family,
                         initialContextPaths,
                         transitionSourceStates,
                         guaranteedContextPaths)))
            {
                issues.Add(new WorkflowDataflowIssue(
                    transition.TransitionId,
                    null,
                    family,
                    "Published output family has no reachable concrete producer before this transition."));
            }
        }

        var gateFamilies = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var gate in instance.Validation?.Gates ?? new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal))
        {
            var families = gate.Value.RequiredOutputFamilies
                .Concat(gate.Value.RequiredMachineReadableOutputFamilies)
                .Concat(gate.Value.RequiredHumanReviewableOutputFamilies)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static family => family, StringComparer.Ordinal)
                .ToArray();
            gateFamilies[gate.Key] = families;

            foreach (var family in families)
            {
                var publishers = transitions.Where(transition =>
                    reachableTransitionIds.Contains(transition.TransitionId)
                    && transition.SatisfiedGateIds.Contains(gate.Key, StringComparer.Ordinal));
                if (!publishers.Any(transition => IsConcreteProducer(
                        transition,
                        family,
                        initialContextPaths,
                        transitionSourceStates,
                        guaranteedContextPaths)))
                {
                    issues.Add(new WorkflowDataflowIssue(
                        null,
                        gate.Key,
                        family,
                        "Required output family has no reachable concrete producer on a gate-satisfying transition."));
                }
            }
        }

        return new SkillWorkflowDataflowReport(instance.InstanceId, transitions, gateFamilies, issues);
    }

    private static (HashSet<string> StateIds, HashSet<string> TransitionIds) FindReachability(WorkflowInstance instance)
    {
        var states = instance.GetStateNodes();
        var transitions = instance.GetTransitionNodes();
        var reachableStates = new HashSet<string>(StringComparer.Ordinal);
        var reachableTransitions = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        if (!string.IsNullOrWhiteSpace(instance.StartNodeId))
        {
            pending.Push(instance.StartNodeId);
        }

        while (pending.Count > 0)
        {
            var stateId = pending.Pop();
            if (!reachableStates.Add(stateId) || !states.TryGetValue(stateId, out var state))
            {
                continue;
            }

            foreach (var transitionId in state.Groups.SelectMany(static group => group.TransitionIds))
            {
                if (!transitions.TryGetValue(transitionId, out var transition))
                {
                    continue;
                }

                reachableTransitions.Add(transition.Id);
                if (!string.IsNullOrWhiteSpace(transition.TargetNodeId))
                {
                    pending.Push(transition.TargetNodeId);
                }
            }
        }

        return (reachableStates, reachableTransitions);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildTransitionSourceStates(
        WorkflowInstance instance,
        IReadOnlySet<string> reachableStateIds)
    {
        var sourceStates = instance.GetTransitionNodes().Keys.ToDictionary(
            static transitionId => transitionId,
            static _ => new List<string>(),
            StringComparer.Ordinal);
        foreach (var state in instance.GetStateNodes().Values)
        {
            if (!reachableStateIds.Contains(state.Id))
            {
                continue;
            }

            foreach (var transitionId in state.Groups.SelectMany(static group => group.TransitionIds))
            {
                if (sourceStates.TryGetValue(transitionId, out var states))
                {
                    states.Add(state.Id);
                }
            }
        }

        return sourceStates.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value.Distinct(StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);
    }



    private static IReadOnlyDictionary<string, IReadOnlySet<string>> ComputeGuaranteedContextPaths(
        WorkflowInstance instance,
        IReadOnlyDictionary<string, WorkflowTransitionDataflow> transitions,
        IReadOnlySet<string> reachableTransitionIds,
        IReadOnlyDictionary<string, IReadOnlyList<string>> transitionSourceStates,
        IReadOnlySet<string> initialContextPaths)
    {
        var states = instance.GetStateNodes();
        var reachableStates = transitionSourceStates
            .Where(pair => reachableTransitionIds.Contains(pair.Key))
            .SelectMany(static pair => pair.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(instance.StartNodeId) && states.ContainsKey(instance.StartNodeId))
        {
            reachableStates.Add(instance.StartNodeId);
        }

        var allProducedPaths = initialContextPaths
            .Concat(transitions.Values
                .Where(transition => reachableTransitionIds.Contains(transition.TransitionId))
                .SelectMany(static transition => transition.ProducedContextPaths))
            .ToHashSet(StringComparer.Ordinal);
        var guaranteed = states.Keys.ToDictionary(
            stateId => stateId,
            stateId => stateId == instance.StartNodeId
                ? new HashSet<string>(initialContextPaths, StringComparer.Ordinal)
                : reachableStates.Contains(stateId)
                    ? new HashSet<string>(allProducedPaths, StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var incoming = states.Keys.ToDictionary(
            static stateId => stateId,
            static _ => new List<(string SourceState, WorkflowTransitionDataflow Transition)>(),
            StringComparer.Ordinal);

        var transitionNodes = instance.GetTransitionNodes();
        foreach (var pair in transitionSourceStates)
        {
            if (!reachableTransitionIds.Contains(pair.Key)
                || !transitions.TryGetValue(pair.Key, out var transition)
                || !transitionNodes.TryGetValue(pair.Key, out var transitionNode)
                || string.IsNullOrWhiteSpace(transitionNode.TargetNodeId)
                || !states.ContainsKey(transitionNode.TargetNodeId))
            {
                continue;
            }

            foreach (var sourceStateId in pair.Value)
            {
                incoming[transitionNode.TargetNodeId].Add((sourceStateId, transition));
            }
        }

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var stateId in reachableStates.OrderBy(static value => value, StringComparer.Ordinal))
            {
                if (string.Equals(stateId, instance.StartNodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                var next = new HashSet<string>(StringComparer.Ordinal);
                var hasIncoming = false;
                foreach (var edge in incoming[stateId])
                {
                    if (!guaranteed.TryGetValue(edge.SourceState, out var sourcePaths))
                    {
                        continue;
                    }

                    var candidate = new HashSet<string>(sourcePaths, StringComparer.Ordinal);
                    candidate.UnionWith(edge.Transition.ProducedContextPaths);
                    if (!hasIncoming)
                    {
                        next = candidate;
                        hasIncoming = true;
                    }
                    else
                    {
                        next.IntersectWith(candidate);
                    }
                }

                if (!hasIncoming)
                {
                    next.Clear();
                }

                if (!guaranteed[stateId].SetEquals(next))
                {
                    guaranteed[stateId] = next;
                    changed = true;
                }
            }
        }

        return guaranteed.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlySet<string>)pair.Value,
            StringComparer.Ordinal);
    }

    private static WorkflowTransitionDataflow BuildTransitionDataflow(TransitionBase transition)
    {
        var parameters = transition is CommandTransition commandTransition
            ? commandTransition.Command.Parameters
            : null;
        var inputPaths = GetStrings(parameters, "requiredInputs");
        var resumeOutputKey = GetString(parameters, "resumeOutputKey");
        var payloadPaths = inputPaths
            .Concat(string.IsNullOrWhiteSpace(resumeOutputKey) ? Enumerable.Empty<string>() : [resumeOutputKey])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        var outputBindings = GetOutputBindings(parameters);
        var producedContextPaths = (string.IsNullOrWhiteSpace(transition.OutputPath) ? Enumerable.Empty<string>() : [transition.OutputPath])
            .Concat(outputBindings.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        var publishedOutputFamilies = (transition.PublishesOutputFamilies ?? [])
            .Concat(transition.PublishesBlockedOutputFamilies ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static family => family, StringComparer.Ordinal)
            .ToArray();
        var unresolvedOutputFamilies = publishedOutputFamilies
            .Where(family => !string.Equals(family, transition.OutputPath, StringComparison.Ordinal) && !outputBindings.ContainsKey(family))
            .ToArray();
        var satisfiedGateIds = GetTransitionStrings(transition.SatisfiesGateIds, parameters, "satisfiesGateIds");
        var routeNames = (transition.TerminalRoutes ?? [])
            .Concat(transition.BlockedRoutes ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static route => route, StringComparer.Ordinal)
            .ToArray();

        return new WorkflowTransitionDataflow(
            transition.Id,
            transition.StepKind,
            inputPaths,
            payloadPaths,
            resumeOutputKey,
            transition.OutputPath,
            GetString(parameters, "projectionMode"),
            outputBindings,
            producedContextPaths,
            publishedOutputFamilies,
            satisfiedGateIds,
            routeNames,
            unresolvedOutputFamilies);
    }

        private static bool IsConcreteProducer(
            WorkflowTransitionDataflow transition,
            string family,
            IReadOnlySet<string> initialContextPaths,
            IReadOnlyDictionary<string, IReadOnlyList<string>> transitionSourceStates,
            IReadOnlyDictionary<string, IReadOnlySet<string>> guaranteedContextPaths)
        {
            if (string.Equals(transition.OutputPath, family, StringComparison.Ordinal))
            {
                return true;
            }

            if (!transition.OutputBindings.TryGetValue(family, out var binding))
            {
                return false;
            }

            if (string.Equals(binding, "$result", StringComparison.Ordinal))
            {
                return true;
            }

            if (binding is null || !binding.StartsWith("$context:", StringComparison.Ordinal))
            {
                return !string.IsNullOrWhiteSpace(binding);
            }

            var sourcePath = binding["$context:".Length..];
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return false;
            }

            if (transition.PayloadPaths.Any(path => PathCovers(path, sourcePath))
                || PathCovers(transition.OutputPath, sourcePath)
                || initialContextPaths.Any(path => PathCovers(path, sourcePath)))
            {
                return true;
            }

            return transitionSourceStates.TryGetValue(transition.TransitionId, out var sourceStates)
                && sourceStates.Count > 0
                && sourceStates.All(state => guaranteedContextPaths.TryGetValue(state, out var paths)
                    && paths.Any(path => PathCovers(path, sourcePath)));
        }

    private static bool PathCovers(string? producerPath, string targetPath)
    {
        return !string.IsNullOrWhiteSpace(producerPath)
            && (string.Equals(producerPath, targetPath, StringComparison.Ordinal)
                || targetPath.StartsWith($"{producerPath}.", StringComparison.Ordinal));
    }

    private static IReadOnlyDictionary<string, string?> GetOutputBindings(IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters?.TryGetValue("outputBindings", out var value) != true || value is null)
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        IEnumerable<KeyValuePair<string, object?>>? bindings = value switch
        {
            IDictionary<string, object?> mutable => mutable,
            IReadOnlyDictionary<string, object?> readOnly => readOnly,
            _ => null,
        };

        return bindings?.ToDictionary(
                   static pair => pair.Key,
                   static pair => Convert.ToString(pair.Value),
                   StringComparer.Ordinal)
               ?? new Dictionary<string, string?>(StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> GetTransitionStrings(
        IReadOnlyList<string>? declared,
        IReadOnlyDictionary<string, object?>? parameters,
        string key)
    {
        return declared is { Count: > 0 }
            ? declared.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray()
            : GetStrings(parameters, key);
    }

    private static IReadOnlyList<string> GetStrings(IReadOnlyDictionary<string, object?>? parameters, string key)
    {
        if (parameters?.TryGetValue(key, out var value) != true || value is null)
        {
            return [];
        }

        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => [text],
            IEnumerable<string> items => items.Where(static item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToArray(),
            IEnumerable<object?> items => items.Select(Convert.ToString).Where(static item => !string.IsNullOrWhiteSpace(item)).Cast<string>().Distinct(StringComparer.Ordinal).ToArray(),
            _ => [],
        };
    }

    private static string? GetString(IReadOnlyDictionary<string, object?>? parameters, string key)
    {
        return parameters?.TryGetValue(key, out var value) == true && value is not null
            ? Convert.ToString(value)
            : null;
    }
}
