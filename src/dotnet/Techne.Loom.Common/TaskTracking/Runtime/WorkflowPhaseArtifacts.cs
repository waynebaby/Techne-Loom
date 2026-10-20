using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record WorkflowPhaseFragment(
    string SchemaVersion,
    string SkeletonSchemaVersion,
    string WorkflowId,
    string PhaseId,
    IReadOnlyDictionary<string, ITaskNode> Nodes)
{
    public const string CurrentSchemaVersion = "loom.workflow-phase-fragment.v1";

    public static WorkflowPhaseFragment Create(
        string workflowId,
        string phaseId,
        IReadOnlyDictionary<string, ITaskNode> nodes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseId);
        ArgumentNullException.ThrowIfNull(nodes);
        if (nodes.Count == 0)
        {
            throw new InvalidOperationException("A workflow phase fragment must contain at least one node.");
        }

        var sortedNodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal);
        foreach (var pair in nodes.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (pair.Value is null || !string.Equals(pair.Key, pair.Value.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Phase fragment node key '{pair.Key}' must match its node id.");
            }

            sortedNodes.Add(pair.Key, pair.Value);
        }

        return new WorkflowPhaseFragment(
            CurrentSchemaVersion,
            WorkflowPhaseSkeleton.CurrentSchemaVersion,
            workflowId,
            phaseId,
            sortedNodes);
    }

    public static WorkflowPhaseFragment Parse(string json, string sourcePath)
    {
        using var document = JsonDocument.Parse(json);
        EnsureObjectProperties(
            document.RootElement,
            sourcePath,
            "schemaVersion",
            "skeletonSchemaVersion",
            "workflowId",
            "phaseId",
            "nodes");
        var fragment = JsonSerializer.Deserialize<WorkflowPhaseFragment>(json, WorkflowJsonSerializer.CreateDefaultOptions())
            ?? throw Invalid(sourcePath, "the phase fragment could not be deserialized.");
        Validate(fragment, sourcePath);
        return fragment with
        {
            Nodes = fragment.Nodes.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
        };
    }

    public string ToJson(bool indented = true)
        => JsonSerializer.Serialize(this, WorkflowJsonSerializer.CreateDefaultOptions(indented));

    internal static void Validate(WorkflowPhaseFragment fragment, string sourcePath)
    {
        if (!string.Equals(fragment.SchemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw Invalid(sourcePath, $"phase fragment schemaVersion '{fragment.SchemaVersion}' is unsupported.");
        }

        if (!string.Equals(fragment.SkeletonSchemaVersion, WorkflowPhaseSkeleton.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw Invalid(sourcePath, $"phase fragment skeletonSchemaVersion must be '{WorkflowPhaseSkeleton.CurrentSchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(fragment.WorkflowId)
            || string.IsNullOrWhiteSpace(fragment.PhaseId)
            || fragment.Nodes is null
            || fragment.Nodes.Count == 0)
        {
            throw Invalid(sourcePath, "workflowId, phaseId, and a non-empty nodes object are required.");
        }

        foreach (var pair in fragment.Nodes)
        {
            if (pair.Value is null || !string.Equals(pair.Key, pair.Value.Id, StringComparison.Ordinal))
            {
                throw Invalid(sourcePath, $"node key '{pair.Key}' must match its node id.");
            }
        }
    }

    internal static void EnsureObjectProperties(JsonElement element, string sourcePath, params string[] allowedProperties)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(sourcePath, "the document root must be an object.");
        }

        var allowed = allowedProperties.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw Invalid(sourcePath, $"duplicate property '{property.Name}'.");
            }

            if (!allowed.Contains(property.Name))
            {
                throw Invalid(sourcePath, $"unsupported property '{property.Name}'.");
            }
        }
    }

    internal static InvalidOperationException Invalid(string sourcePath, string message)
        => new($"Workflow phase artifact '{sourcePath}': {message}");
}

public sealed record WorkflowPhaseInterfaceChangeRequest(
    string Kind,
    string Reason,
    string? ContextPath = null,
    string? NodeId = null);

public sealed record WorkflowPhaseCompileResult(
    string SchemaVersion,
    string WorkflowId,
    string SkeletonSchemaVersion,
    string PhaseId,
    string Status,
    bool StaticOnly,
    IReadOnlyList<string> SyntheticBoundaryIds,
    IReadOnlyList<WorkflowPhaseDiagnostic> Diagnostics,
    IReadOnlyList<WorkflowPhaseInterfaceChangeRequest> InterfaceChangeRequests,
    WorkflowPhaseFragment? Fragment)
{
    public const string CurrentSchemaVersion = "loom.workflow-phase-result.v1";

    public static IReadOnlyList<string> GetSyntheticBoundaryIds(string phaseId)
        =>
        [
            $"__loom_phase_harness.{phaseId}.input",
            $"__loom_phase_harness.{phaseId}.output",
        ];

    public static WorkflowPhaseCompileResult Parse(string json, string sourcePath)
    {
        using var document = JsonDocument.Parse(json);
        WorkflowPhaseFragment.EnsureObjectProperties(
            document.RootElement,
            sourcePath,
            "schemaVersion",
            "workflowId",
            "skeletonSchemaVersion",
            "phaseId",
            "status",
            "staticOnly",
            "syntheticBoundaryIds",
            "diagnostics",
            "interfaceChangeRequests",
            "fragment");
        var result = JsonSerializer.Deserialize<WorkflowPhaseCompileResult>(json, WorkflowJsonSerializer.CreateDefaultOptions())
            ?? throw WorkflowPhaseFragment.Invalid(sourcePath, "the phase compile result could not be deserialized.");
        Validate(result, sourcePath);
        return result;
    }

    public string ToJson(bool indented = true)
        => JsonSerializer.Serialize(this, WorkflowJsonSerializer.CreateDefaultOptions(indented));

    internal static void Validate(WorkflowPhaseCompileResult result, string sourcePath)
    {
        if (!string.Equals(result.SchemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw WorkflowPhaseFragment.Invalid(sourcePath, $"phase result schemaVersion '{result.SchemaVersion}' is unsupported.");
        }

        if (!string.Equals(result.SkeletonSchemaVersion, WorkflowPhaseSkeleton.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw WorkflowPhaseFragment.Invalid(sourcePath, $"phase result skeletonSchemaVersion must be '{WorkflowPhaseSkeleton.CurrentSchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(result.WorkflowId)
            || string.IsNullOrWhiteSpace(result.PhaseId)
            || result.Status is not ("passed" or "failed" or "interfaceChangeRequested")
            || !result.StaticOnly
            || result.SyntheticBoundaryIds is null
            || result.Diagnostics is null
            || result.InterfaceChangeRequests is null)
        {
            throw WorkflowPhaseFragment.Invalid(sourcePath, "workflowId, phaseId, a supported status, staticOnly=true, and result arrays are required.");
        }

        if (result.SyntheticBoundaryIds.Any(string.IsNullOrWhiteSpace)
            || result.SyntheticBoundaryIds.Distinct(StringComparer.Ordinal).Count() != result.SyntheticBoundaryIds.Count)
        {
            throw WorkflowPhaseFragment.Invalid(sourcePath, "syntheticBoundaryIds must contain unique non-empty strings.");
        }

        if (result.Status == "passed")
        {
            if (result.Fragment is null
                || result.Diagnostics.Any(static diagnostic => diagnostic.Severity == WorkflowPhaseDiagnosticSeverity.Error)
                || result.InterfaceChangeRequests.Count != 0)
            {
                throw WorkflowPhaseFragment.Invalid(sourcePath, "a passed result requires a fragment, no errors, and no interface-change requests.");
            }

            var expectedBoundaries = GetSyntheticBoundaryIds(result.PhaseId);
            if (!result.SyntheticBoundaryIds.SequenceEqual(expectedBoundaries, StringComparer.Ordinal))
            {
                throw WorkflowPhaseFragment.Invalid(sourcePath, "a passed result must identify its synthetic input and output boundaries in order.");
            }

            WorkflowPhaseFragment.Validate(result.Fragment, sourcePath);
            if (!string.Equals(result.Fragment.WorkflowId, result.WorkflowId, StringComparison.Ordinal)
                || !string.Equals(result.Fragment.PhaseId, result.PhaseId, StringComparison.Ordinal))
            {
                throw WorkflowPhaseFragment.Invalid(sourcePath, "the result and fragment workflowId/phaseId values must match.");
            }
        }
        else if (result.Status == "interfaceChangeRequested" && result.InterfaceChangeRequests.Count == 0)
        {
            throw WorkflowPhaseFragment.Invalid(sourcePath, "interfaceChangeRequested requires at least one structured request.");
        }
    }
}

public static class WorkflowPhaseMerger
{
    public static WorkflowInstance Merge(
        WorkflowInstance designTemplate,
        WorkflowPhaseSkeleton skeleton,
        ContextFieldCatalog catalog,
        IReadOnlyCollection<WorkflowPhaseCompileResult> results)
    {
        ArgumentNullException.ThrowIfNull(designTemplate);
        ArgumentNullException.ThrowIfNull(skeleton);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(results);
        EnsureDesignTemplate(designTemplate);

        var interfaceValidation = WorkflowPhaseSkeletonValidator.Validate(skeleton, catalog, "phase merge");
        if (!interfaceValidation.IsValid)
        {
            throw WorkflowPhaseFragment.Invalid("phase merge", string.Join(" ", interfaceValidation.Diagnostics
                .Where(static diagnostic => diagnostic.Severity == WorkflowPhaseDiagnosticSeverity.Error)
                .Select(static diagnostic => diagnostic.Message)));
        }

        if (results.Count != skeleton.Phases.Count)
        {
            throw WorkflowPhaseFragment.Invalid("phase merge", $"expected exactly one result for each of {skeleton.Phases.Count} phases; received {results.Count}.");
        }

        var phasesById = skeleton.Phases.ToDictionary(static phase => phase.Id, StringComparer.Ordinal);
        var resultsById = new Dictionary<string, WorkflowPhaseCompileResult>(StringComparer.Ordinal);
        foreach (var result in results)
        {
            WorkflowPhaseCompileResult.Validate(result, "phase merge");
            if (!string.Equals(result.WorkflowId, skeleton.WorkflowId, StringComparison.Ordinal)
                || !string.Equals(result.SkeletonSchemaVersion, skeleton.SchemaVersion, StringComparison.Ordinal))
            {
                throw WorkflowPhaseFragment.Invalid("phase merge", $"result for phase '{result.PhaseId}' belongs to a different workflow or skeleton version.");
            }

            if (!phasesById.TryGetValue(result.PhaseId, out var phase))
            {
                throw WorkflowPhaseFragment.Invalid("phase merge", $"result references unknown phase '{result.PhaseId}'.");
            }

            if (!resultsById.TryAdd(result.PhaseId, result))
            {
                throw WorkflowPhaseFragment.Invalid("phase merge", $"duplicate result for phase '{result.PhaseId}'.");
            }

            if (!string.Equals(result.Status, "passed", StringComparison.Ordinal))
            {
                throw WorkflowPhaseFragment.Invalid("phase merge", $"phase '{phase.Id}' has status '{result.Status}' and cannot be merged.");
            }
        }

        var rawNodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal);
        var nodePhases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var phase in skeleton.Phases.OrderBy(static phase => phase.Id, StringComparer.Ordinal))
        {
            if (!resultsById.TryGetValue(phase.Id, out var result) || result.Fragment is null)
            {
                throw WorkflowPhaseFragment.Invalid("phase merge", $"missing result for phase '{phase.Id}'.");
            }

            var revalidated = WorkflowPhaseCompiler.Compile(
                skeleton,
                phase.Id,
                result.Fragment,
                designTemplate.ExpressionBinding,
                catalog,
                designTemplate.Validation,
                $"phase merge/{phase.Id}");
            if (!string.Equals(revalidated.Status, "passed", StringComparison.Ordinal))
            {
                var diagnostics = revalidated.Diagnostics
                    .Where(static diagnostic => diagnostic.Severity == WorkflowPhaseDiagnosticSeverity.Error)
                    .Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(revalidated.InterfaceChangeRequests.Select(static request => $"{request.Kind}: {request.Reason}"))
                    .ToArray();
                throw WorkflowPhaseFragment.Invalid(
                    "phase merge",
                    $"phase '{phase.Id}' result does not pass static revalidation against the current skeleton, catalog, expression binding, and validation contract. {string.Join(" ", diagnostics)}");
            }

            var syntheticIds = result.SyntheticBoundaryIds.ToHashSet(StringComparer.Ordinal);
            foreach (var pair in result.Fragment.Nodes.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                if (syntheticIds.Contains(pair.Key))
                {
                    throw WorkflowPhaseFragment.Invalid("phase merge", $"phase '{phase.Id}' fragment contains synthetic harness node '{pair.Key}'.");
                }

                if (phase.Mode == "phaseSliced" && !pair.Key.StartsWith(phase.Prefix, StringComparison.Ordinal))
                {
                    throw WorkflowPhaseFragment.Invalid("phase merge", $"node '{pair.Key}' does not use phase prefix '{phase.Prefix}'.");
                }

                if (!rawNodes.TryAdd(pair.Key, pair.Value))
                {
                    throw WorkflowPhaseFragment.Invalid("phase merge", $"node id '{pair.Key}' is duplicated across phase results.");
                }

                nodePhases.Add(pair.Key, phase.Id);
            }

            foreach (var entryId in phase.Entries)
            {
                if (!result.Fragment.Nodes.TryGetValue(entryId, out var entry) || entry is not StateNode)
                {
                    throw WorkflowPhaseFragment.Invalid("phase merge", $"phase '{phase.Id}' entry '{entryId}' must be a state node in its fragment.");
                }
            }

            foreach (var exitId in phase.Exits)
            {
                if (!result.Fragment.Nodes.TryGetValue(exitId, out var exit) || exit is not TransitionBase)
                {
                    throw WorkflowPhaseFragment.Invalid("phase merge", $"phase '{phase.Id}' exit '{exitId}' must be a transition in its fragment.");
                }
            }
        }

        var candidateNodes = WorkflowInstanceCloner.Clone(new WorkflowInstance { Nodes = rawNodes }).Nodes;
        var routeTargets = BuildRouteTargets(skeleton);
        foreach (var route in skeleton.Routes.Concat(skeleton.Backedges))
        {
            if (!candidateNodes.TryGetValue(route.FromExitId, out var sourceNode)
                || sourceNode is not TransitionBase sourceTransition
                || !candidateNodes.TryGetValue(route.ToEntryId, out var targetNode)
                || targetNode is not StateNode)
            {
                throw WorkflowPhaseFragment.Invalid("phase merge", $"route '{route.FromPhaseId}.{route.FromExitId}' to '{route.ToPhaseId}.{route.ToEntryId}' has incompatible phase fragment nodes.");
            }

            if (!string.Equals(nodePhases[route.FromExitId], route.FromPhaseId, StringComparison.Ordinal)
                || !string.Equals(nodePhases[route.ToEntryId], route.ToPhaseId, StringComparison.Ordinal))
            {
                throw WorkflowPhaseFragment.Invalid("phase merge", "a declared route endpoint is owned by the wrong phase fragment.");
            }

            if (sourceTransition.TargetNodeId is not null
                && !string.Equals(sourceTransition.TargetNodeId, route.ToEntryId, StringComparison.Ordinal))
            {
                throw WorkflowPhaseFragment.Invalid("phase merge", $"transition '{route.FromExitId}' conflicts with its frozen route to '{route.ToEntryId}'.");
            }

            candidateNodes[route.FromExitId] = WithTarget(sourceTransition, route.ToEntryId);
        }

        foreach (var pair in candidateNodes)
        {
            if (pair.Value is StateNode state)
            {
                foreach (var transitionId in state.Groups.SelectMany(static group => group.TransitionIds))
                {
                    if (!candidateNodes.TryGetValue(transitionId, out var transition) || transition is not TransitionBase)
                    {
                        throw WorkflowPhaseFragment.Invalid("phase merge", $"state '{state.Id}' references missing transition '{transitionId}'.");
                    }
                }
            }
            else if (pair.Value is TransitionBase transition && transition.TargetNodeId is { Length: > 0 } targetId)
            {
                if (!candidateNodes.ContainsKey(targetId))
                {
                    throw WorkflowPhaseFragment.Invalid("phase merge", $"transition '{transition.Id}' targets unknown node '{targetId}'.");
                }

                if (!string.Equals(nodePhases[transition.Id], nodePhases[targetId], StringComparison.Ordinal)
                    && (!routeTargets.TryGetValue(transition.Id, out var declaredTarget)
                        || !string.Equals(declaredTarget, targetId, StringComparison.Ordinal)))
                {
                    throw WorkflowPhaseFragment.Invalid("phase merge", $"cross-phase target '{transition.Id}' to '{targetId}' is not declared by the skeleton.");
                }
            }
        }

        if (!candidateNodes.TryGetValue(designTemplate.StartNodeId, out var startNode) || startNode is not StateNode)
        {
            throw WorkflowPhaseFragment.Invalid("phase merge", $"startNodeId '{designTemplate.StartNodeId}' must identify a merged state node.");
        }

        if (designTemplate.EndNodeId is { Length: > 0 } endNodeId
            && (!candidateNodes.TryGetValue(endNodeId, out var endNode) || endNode is not StateNode))
        {
            throw WorkflowPhaseFragment.Invalid("phase merge", $"endNodeId '{endNodeId}' must identify a merged state node.");
        }

        var candidate = WorkflowInstanceCloner.Clone(designTemplate);
        candidate.Nodes = candidateNodes.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        candidate.CurrentNodeId = candidate.StartNodeId;
        return candidate;
    }

    private static Dictionary<string, string> BuildRouteTargets(WorkflowPhaseSkeleton skeleton)
    {
        var targets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var edge in skeleton.Routes.Concat(skeleton.Backedges))
        {
            if (!targets.TryAdd(edge.FromExitId, edge.ToEntryId))
            {
                throw WorkflowPhaseFragment.Invalid("phase merge", $"exit '{edge.FromExitId}' has more than one cross-phase target.");
            }
        }

        return targets;
    }

    private static TransitionBase WithTarget(TransitionBase transition, string targetNodeId)
        => transition switch
        {
            CommandTransition command => command with { TargetNodeId = targetNodeId },
            ExpressionTransition expression => expression with { TargetNodeId = targetNodeId },
            ToBeRefinedTransition toBeRefined => toBeRefined with { TargetNodeId = targetNodeId },
            _ => throw WorkflowPhaseFragment.Invalid("phase merge", $"transition type '{transition.GetType().Name}' cannot be routed."),
        };

    private static void EnsureDesignTemplate(WorkflowInstance template)
    {
        if (template.Status is not (WorkflowStatus.Drafting or WorkflowStatus.ReadyToStart)
            || template.Version != 0
            || template.History.Count != 0
            || template.ActiveWaitGroups.Count != 0
            || template.FanoutCancellationSignals.Count != 0
            || template.LastGateEvaluation is not null
            || template.LastActivityUtc is not null
            || template.LastHeartbeatUtc is not null
            || template.LeaseOwner is not null
            || template.LeaseExpiresUtc is not null)
        {
            throw WorkflowPhaseFragment.Invalid("phase merge", "merge accepts a design template only; active or previously executed WorkflowInstance files are not migrated.");
        }
    }
}
