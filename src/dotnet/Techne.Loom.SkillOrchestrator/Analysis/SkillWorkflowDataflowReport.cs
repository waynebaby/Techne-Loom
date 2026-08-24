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
        var reachableTransitionIds = FindReachableTransitionIds(instance);
        var initialContextPaths = instance.Context.Keys
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.Ordinal);
        var transitionSourceStates = BuildTransitionSourceStates(instance);
        var earliestArrival = ComputeEarliestArrival(instance);
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
                         transitionsById,
                         transitionSourceStates,
                         earliestArrival)))
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
                        transitionsById,
                        transitionSourceStates,
                        earliestArrival)))
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

    private static HashSet<string> FindReachableTransitionIds(WorkflowInstance instance)
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

        return reachableTransitions;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildTransitionSourceStates(WorkflowInstance instance)
    {
        var sourceStates = instance.GetTransitionNodes().Keys.ToDictionary(
            static transitionId => transitionId,
            static _ => new List<string>(),
            StringComparer.Ordinal);
        foreach (var state in instance.GetStateNodes().Values)
        {
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

    private static IReadOnlyDictionary<string, int> ComputeEarliestArrival(WorkflowInstance instance)
    {
        var states = instance.GetStateNodes();
        var transitions = instance.GetTransitionNodes();
        var arrival = new Dictionary<string, int>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(instance.StartNodeId) || !states.ContainsKey(instance.StartNodeId))
        {
            return arrival;
        }

        // Unit-weight BFS from the start state: first discovery is the earliest step at which
        // control can enter each state, which stays sound across cycles and back edges.
        var pending = new Queue<string>();
        arrival[instance.StartNodeId] = 0;
        pending.Enqueue(instance.StartNodeId);

        while (pending.Count > 0)
        {
            var stateId = pending.Dequeue();
            if (!states.TryGetValue(stateId, out var state))
            {
                continue;
            }

            foreach (var transitionId in state.Groups.SelectMany(static group => group.TransitionIds))
            {
                if (!transitions.TryGetValue(transitionId, out var transition)
                    || string.IsNullOrWhiteSpace(transition.TargetNodeId)
                    || !states.ContainsKey(transition.TargetNodeId)
                    || arrival.ContainsKey(transition.TargetNodeId))
                {
                    continue;
                }

                arrival[transition.TargetNodeId] = arrival[stateId] + 1;
                pending.Enqueue(transition.TargetNodeId);
            }
        }

        return arrival;
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
        IReadOnlyDictionary<string, WorkflowTransitionDataflow> transitions,
        IReadOnlyDictionary<string, IReadOnlyList<string>> transitionSourceStates,
        IReadOnlyDictionary<string, int> earliestArrival)
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

        if (transition.PayloadPaths.Contains(sourcePath, StringComparer.Ordinal)
            || PathCovers(transition.OutputPath, sourcePath)
            || initialContextPaths.Any(path => PathCovers(path, sourcePath)))
        {
            return true;
        }

        return transitions.Values
            .Where(producer => !string.Equals(producer.TransitionId, transition.TransitionId, StringComparison.Ordinal))
            .Where(producer => producer.ProducedContextPaths.Any(path => PathCovers(path, sourcePath)))
            .Any(producer => CanPrecede(
                producer.TransitionId,
                transition.TransitionId,
                transitionSourceStates,
                earliestArrival));
    }

    private static bool CanPrecede(
        string producerTransitionId,
        string consumerTransitionId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> transitionSourceStates,
        IReadOnlyDictionary<string, int> earliestArrival)
    {
        if (!transitionSourceStates.TryGetValue(producerTransitionId, out var producerSources)
            || !transitionSourceStates.TryGetValue(consumerTransitionId, out var consumerSources))
        {
            return false;
        }

        // The producer's value becomes available one step after control first enters the producer's source state.
        var availability = int.MaxValue;
        foreach (var state in producerSources)
        {
            if (earliestArrival.TryGetValue(state, out var arrival))
            {
                availability = Math.Min(availability, arrival + 1);
            }
        }

        // The consumer can first execute the moment control enters any of its source states.
        var firstExecution = int.MaxValue;
        foreach (var state in consumerSources)
        {
            if (earliestArrival.TryGetValue(state, out var arrival))
            {
                firstExecution = Math.Min(firstExecution, arrival);
            }
        }

        return availability != int.MaxValue && availability <= firstExecution;
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
