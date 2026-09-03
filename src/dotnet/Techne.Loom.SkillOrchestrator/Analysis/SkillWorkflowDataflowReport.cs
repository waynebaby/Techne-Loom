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

public enum WorkflowEmitterKind
{
    Unknown,
    ExternalResult,
    KnownNull,
    LiteralWriter,
    RealToolResult,
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
    IReadOnlyList<string> UnresolvedOutputFamilies,
    WorkflowEmitterKind EmitterKind = WorkflowEmitterKind.Unknown,
    IReadOnlyList<string>? UpdatesKeys = null,
    string? ToolName = null);

public sealed record WorkflowDataflowIssue(
    string? TransitionId,
    string? GateId,
    string OutputFamily,
    string Reason,
    WorkflowEmitterKind EmitterKind = WorkflowEmitterKind.Unknown);

public sealed class SkillWorkflowDataflowAnalyzer
{
    private const string GovernedTemplateKind = "so-governed-target-skill";

    public SkillWorkflowDataflowReport Analyze(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var governed = string.Equals(instance.TemplateKind, GovernedTemplateKind, StringComparison.Ordinal);

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
            initialContextPaths,
            governed);
        var issues = new List<WorkflowDataflowIssue>();

        foreach (var transition in transitions)
        {
            foreach (var family in transition.UnresolvedOutputFamilies)
            {
                issues.Add(new WorkflowDataflowIssue(
                    transition.TransitionId,
                    null,
                    family,
                    "Published output family has no concrete outputPath or outputBinding producer.",
                    transition.EmitterKind));
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
                         guaranteedContextPaths,
                          governed)))
            {
                issues.Add(new WorkflowDataflowIssue(
                    transition.TransitionId,
                    null,
                    family,
                    governed ? BuildProducerIssueReason(transition, family) : "Published output family has no reachable concrete producer before this transition.",
                    transition.EmitterKind));
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
                        guaranteedContextPaths,
                        governed)))
                {
                    issues.Add(new WorkflowDataflowIssue(
                        null,
                        gate.Key,
                        family,
                        "Required output family has no reachable concrete producer on a gate-satisfying transition.",
                        publishers.FirstOrDefault()?.EmitterKind ?? WorkflowEmitterKind.Unknown));
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
        IReadOnlySet<string> initialContextPaths,
        bool governed)
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
                    ? governed
                        ? new HashSet<string>(StringComparer.Ordinal)
                        : new HashSet<string>(allProducedPaths, StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var incoming = states.Keys.ToDictionary(
            static stateId => stateId,
            static _ => new List<(string SourceState, WorkflowTransitionDataflow Transition)>(),
            StringComparer.Ordinal);
        var backEdges = governed
            ? FindBackEdges(instance, reachableStates)
            : new HashSet<(string SourceState, string TargetState, string TransitionId)>();

        var transitionNodes = instance.GetTransitionNodes();
        foreach (var pair in transitionSourceStates)
        {
            if (!reachableTransitionIds.Contains(pair.Key)
                || !transitions.TryGetValue(pair.Key, out var transition)
                || !transitionNodes.TryGetValue(pair.Key, out var transitionNode))
            {
                continue;
            }

            var targetStateId = transitionNode.TargetNodeId;
            if (string.IsNullOrWhiteSpace(targetStateId) || !states.ContainsKey(targetStateId))
            {
                continue;
            }

            foreach (var sourceStateId in pair.Value)
            {
                if (backEdges.Contains((sourceStateId, targetStateId, pair.Key)))
                {
                    continue;
                }

                incoming[targetStateId].Add((sourceStateId, transition));
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
                    var guaranteedView = guaranteed.ToDictionary(
                        static pair => pair.Key,
                        static pair => (IReadOnlySet<string>)pair.Value,
                        StringComparer.Ordinal);
                    var producedPaths = governed
                        ? edge.Transition.ProducedContextPaths
                            .Where(path => IsConcreteProducer(
                                edge.Transition,
                                path,
                                initialContextPaths,
                                transitionSourceStates,
                                guaranteedView,
                                governed))
                        : edge.Transition.ProducedContextPaths;
                    candidate.UnionWith(producedPaths);
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

        static HashSet<(string SourceState, string TargetState, string TransitionId)> FindBackEdges(
            WorkflowInstance workflow,
            IReadOnlySet<string> reachableStateIds)
        {
            var states = workflow.GetStateNodes();
            var transitions = workflow.GetTransitionNodes();
            var active = new HashSet<string>(StringComparer.Ordinal);
            var completed = new HashSet<string>(StringComparer.Ordinal);
            var backEdges = new HashSet<(string SourceState, string TargetState, string TransitionId)>();

            void Visit(string stateId)
            {
                if (!reachableStateIds.Contains(stateId)
                    || !states.TryGetValue(stateId, out var state)
                    || !active.Add(stateId))
                {
                    return;
                }

                foreach (var transitionId in state.Groups.SelectMany(static group => group.TransitionIds))
                {
                    if (!transitions.TryGetValue(transitionId, out var transition))
                    {
                        continue;
                    }

                    var targetStateId = transition.TargetNodeId;
                    if (string.IsNullOrWhiteSpace(targetStateId)
                        || !reachableStateIds.Contains(targetStateId)
                        || !states.ContainsKey(targetStateId))
                    {
                        continue;
                    }

                    if (active.Contains(targetStateId))
                    {
                        backEdges.Add((stateId, targetStateId, transition.Id));
                    }
                    else if (!completed.Contains(targetStateId))
                    {
                        Visit(targetStateId);
                    }
                }

                active.Remove(stateId);
                completed.Add(stateId);
            }

            if (!string.IsNullOrWhiteSpace(workflow.StartNodeId))
            {
                Visit(workflow.StartNodeId);
            }

            return backEdges;
        }
    }
    private static WorkflowTransitionDataflow BuildTransitionDataflow(TransitionBase transition)
    {
        var commandTransition = transition as CommandTransition;
        var parameters = commandTransition?.Command.Parameters;
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
            unresolvedOutputFamilies,
            ClassifyEmitter(transition),
            GetDeclaredUpdateKeysRaw(parameters),
            commandTransition?.Command?.Name);
    }

    private static IReadOnlyList<string> GetDeclaredUpdateKeysRaw(IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters?.TryGetValue("updates", out var value) != true || value is not IDictionary<string, object?> updates)
        {
            return [];
        }

        return updates.Keys
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();
    }

    private static WorkflowEmitterKind ClassifyEmitter(TransitionBase transition)
    {
        if (transition is not CommandTransition commandTransition)
        {
            return WorkflowEmitterKind.Unknown;
        }

        switch (transition.StepKind)
        {
            case WorkflowStepKind.ModelThink:
            case WorkflowStepKind.Plan:
            case WorkflowStepKind.McpCall:
            case WorkflowStepKind.SubagentCall:
            case WorkflowStepKind.AskUser:
            case WorkflowStepKind.WaitResume:
                return WorkflowEmitterKind.ExternalResult;

            case WorkflowStepKind.StateUpdate:
            case WorkflowStepKind.MemoryWrite:
            case WorkflowStepKind.MemoryRead:
                return WorkflowEmitterKind.LiteralWriter;

            case WorkflowStepKind.ArtifactEmit:
                return WorkflowEmitterKind.RealToolResult;

            default:
                return ClassifyCommandEmitter(commandTransition.Command);
        }
    }

    private static WorkflowEmitterKind ClassifyCommandEmitter(CommandInvocation? command)
    {
        if (command is null || string.IsNullOrWhiteSpace(command.Name))
        {
            return WorkflowEmitterKind.Unknown;
        }

        if (string.Equals(command.Name, "noop", StringComparison.Ordinal))
        {
            return WorkflowEmitterKind.KnownNull;
        }

        if (command.Kind is not CommandInvocationKind.Tool and not CommandInvocationKind.NativeCode)
        {
            return WorkflowEmitterKind.RealToolResult;
        }

        return command.Name switch
        {
            "echo" when HasNonNullParameter(command, "message") => WorkflowEmitterKind.RealToolResult,
            "echo" => WorkflowEmitterKind.KnownNull,
            "ls" => WorkflowEmitterKind.RealToolResult,
            "write-file" when HasNonEmptyStringParameter(command, "path") => WorkflowEmitterKind.RealToolResult,
            "workflow.materializeRuntimeCopy" when HasNonEmptyStringParameter(command, "sourceTemplatePath") => WorkflowEmitterKind.RealToolResult,
            _ => WorkflowEmitterKind.Unknown,
        };
    }

    private static bool HasNonNullParameter(CommandInvocation command, string key)
        => command.Parameters?.TryGetValue(key, out var value) == true && value is not null;

    private static bool HasNonEmptyStringParameter(CommandInvocation command, string key)
        => command.Parameters?.TryGetValue(key, out var value) == true
            && !string.IsNullOrWhiteSpace(Convert.ToString(value));

        private static bool IsConcreteProducer(
            WorkflowTransitionDataflow transition,
            string family,
            IReadOnlySet<string> initialContextPaths,
            IReadOnlyDictionary<string, IReadOnlyList<string>> transitionSourceStates,
            IReadOnlyDictionary<string, IReadOnlySet<string>> guaranteedContextPaths,
            bool governed)
        {
            if (!governed)
            {
                return IsConcreteProducerLegacy(transition, family, initialContextPaths, transitionSourceStates, guaranteedContextPaths);
            }

            // Governed fail-closed producer legitimacy (0.3.282 semantic matrix D1-D5):
            // declaration is not evidence; the emitter must actually write a non-null value.
            if (string.Equals(transition.OutputPath, family, StringComparison.Ordinal))
            {
                return OwnOutputLegit(transition);
            }

            if (!transition.OutputBindings.TryGetValue(family, out var binding))
            {
                return false;
            }

            if (string.Equals(binding, "$result", StringComparison.Ordinal))
            {
                // Known-null emitters (noop tool/natives) produce a null result; their $result
                // is never a non-empty producer. Real tools and external resume results are.
                return ResultLegit(transition);
            }

            if (binding is null || !binding.StartsWith("$context:", StringComparison.Ordinal))
            {
                // A non-empty literal binding is concrete evidence (e.g. a checked-in asset reference).
                return !string.IsNullOrWhiteSpace(binding);
            }

            var sourcePath = binding["$context:".Length..];
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return false;
            }

            // Resume payload paths are available at the transition: requiredInputs and resumeOutputKey.
            if (transition.PayloadPaths.Any(path => PathCovers(path, sourcePath)))
            {
                return true;
            }

            // Own-outputPath subpaths are provable only when the value there comes from an external
            // resume payload (canonical projection) or from this transition's declared literal writes.
            // A transition's own write must never retroactively prove its other published families.
            if (OwnOutputSubpathLegit(transition, sourcePath))
            {
                return true;
            }

            if (initialContextPaths.Any(path => PathCovers(path, sourcePath)))
            {
                return true;
            }

            // Prior producers only: guaranteed context on every source state of this transition.
            return transitionSourceStates.TryGetValue(transition.TransitionId, out var sourceStates)
                && sourceStates.Count > 0
                && sourceStates.All(state => guaranteedContextPaths.TryGetValue(state, out var paths)
                    && paths.Any(path => PathCovers(path, sourcePath)));
        }

        private static bool IsConcreteProducerLegacy(
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

        private static bool OwnOutputLegit(WorkflowTransitionDataflow transition)
        {
            switch (transition.EmitterKind)
            {
                case WorkflowEmitterKind.ExternalResult:
                    // Resume projection writes the extracted payload value (or the whole payload) to outputPath.
                    return true;

                case WorkflowEmitterKind.LiteralWriter:
                    if (transition.StepKind == WorkflowStepKind.MemoryRead)
                    {
                        // memory.read always materializes a non-null snapshot object at outputPath.
                        return true;
                    }

                    // StateUpdate/MemoryWrite write only the declared updates map; the outputPath is
                    // provable only when a declared update covers it (D2/D3b).
                    return !string.IsNullOrWhiteSpace(transition.OutputPath)
                        && GetDeclaredUpdateKeys(transition).Any(key => PathCovers(key, transition.OutputPath));

                case WorkflowEmitterKind.RealToolResult:
                    // Real built-in tools (echo, write-file, ls, workflow.* helpers) produce results;
                    // non-empty gate conclusions still require the runtime gate / semantic probe.
                    return true;

                case WorkflowEmitterKind.KnownNull:
                    // noop tool/natives return null: outputPath receives null and is never a producer (D1).
                    return false;

                default:
                    return false;
            }
        }

        private static bool ResultLegit(WorkflowTransitionDataflow transition)
        {
            switch (transition.EmitterKind)
            {
                case WorkflowEmitterKind.ExternalResult:
                    return true;

                case WorkflowEmitterKind.LiteralWriter:
                    // state.update $result is the value at its own outputPath after updates are applied (D3b).
                    return transition.StepKind == WorkflowStepKind.MemoryRead || OwnOutputLegit(transition);

                case WorkflowEmitterKind.RealToolResult:
                    return true;

                default:
                    return false;
            }
        }

        private static bool OwnOutputSubpathLegit(WorkflowTransitionDataflow transition, string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(transition.OutputPath) || !PathCovers(transition.OutputPath, sourcePath))
            {
                return false;
            }

            switch (transition.EmitterKind)
            {
                case WorkflowEmitterKind.ExternalResult:
                    // Subpath of the externally resumed value is a legitimate projection (D4).
                    return true;

                case WorkflowEmitterKind.LiteralWriter:
                    if (transition.StepKind == WorkflowStepKind.MemoryRead)
                    {
                        return true;
                    }

                    return GetDeclaredUpdateKeys(transition).Any(key => PathCovers(key, sourcePath));

                case WorkflowEmitterKind.RealToolResult:
                    // Real tools and ArtifactEmit write a non-null value to their own outputPath at
                    // runtime, so a $context binding into that path is a concrete projection.
                    return true;

                default:
                    // Known-null and unknown emitters never prove subpaths of their own write.
                    return false;
            }
        }

        private static IReadOnlyList<string> GetDeclaredUpdateKeys(WorkflowTransitionDataflow transition)
            => transition.UpdatesKeys ?? [];

        private static string BuildProducerIssueReason(WorkflowTransitionDataflow transition, string family)
        {
            switch (transition.EmitterKind)
            {
                case WorkflowEmitterKind.KnownNull:
                    return $"Emitter '{transition.TransitionId}' is a known-null tool ('{transition.ToolName}'): its result and literal updates do not write context on 0.3.282, so family '{family}' has no concrete producer. Use StateUpdate/MemoryWrite with declared updates, or an external step whose resume payload carries the value.";

                case WorkflowEmitterKind.LiteralWriter:
                    return $"Emitter '{transition.TransitionId}' ({transition.StepKind}) writes only its declared updates map; family '{family}' is not covered by a declared update key or a provable prior producer. Add the write to parameters.updates, move the family to a dedicated producer transition, or bind it from an earlier proven context path.";

                case WorkflowEmitterKind.RealToolResult:
                    return $"Family '{family}' published by '{transition.TransitionId}' has no reachable concrete producer: the tool result is not projected there and no prior transition guarantees the bound context path. Add an explicit outputPath/outputBindings projection or a dedicated producer step before this transition.";

                default:
                    return $"Family '{family}' published by '{transition.TransitionId}' has no reachable concrete producer before this transition. Publish it through a dedicated outputPath or outputBindings on a proven emitter (StateUpdate/MemoryWrite, real tool result, or external resume projection).";
            }
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
