using System.Text.Json;
using System.Text.Json.Nodes;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record WorkflowPhaseImpactSnapshot(
    WorkflowPhaseSkeleton Skeleton,
    ContextFieldCatalog Catalog,
    ExpressionBinding ExpressionBinding,
    WorkflowValidationContract? Validation,
    IReadOnlyCollection<WorkflowPhaseCompileResult> PhaseResults);

public sealed record WorkflowPhaseImpactReason(
    string Code,
    string Message,
    string? PhaseId = null,
    string? ContextPath = null);

public sealed record WorkflowPhaseImpactAnalysis(
    string SchemaVersion,
    string WorkflowId,
    string Status,
    bool FullWorkflowCompileRequired,
    bool FullRevalidationRequired,
    IReadOnlyList<string> RevalidatePhaseIds,
    IReadOnlyList<string> ReusablePhaseIds,
    IReadOnlyList<string> RemovedPhaseIds,
    IReadOnlyList<WorkflowPhaseImpactReason> Reasons)
{
    public const string CurrentSchemaVersion = "loom.workflow-impact.v1";

    public string ToJson(bool indented = true)
        => JsonSerializer.Serialize(this, WorkflowJsonSerializer.CreateDefaultOptions(indented));

    public static WorkflowPhaseImpactAnalysis Parse(string json, string sourcePath)
    {
        using var document = JsonDocument.Parse(json);
        WorkflowPhaseFragment.EnsureObjectProperties(
            document.RootElement,
            sourcePath,
            "schemaVersion",
            "workflowId",
            "status",
            "fullWorkflowCompileRequired",
            "fullRevalidationRequired",
            "revalidatePhaseIds",
            "reusablePhaseIds",
            "removedPhaseIds",
            "reasons");
        var analysis = JsonSerializer.Deserialize<WorkflowPhaseImpactAnalysis>(json, WorkflowJsonSerializer.CreateDefaultOptions())
            ?? throw WorkflowPhaseFragment.Invalid(sourcePath, "the phase impact analysis could not be deserialized.");
        Validate(analysis, sourcePath);
        return analysis;
    }

    internal static void Validate(WorkflowPhaseImpactAnalysis analysis, string sourcePath)
    {
        if (!string.Equals(analysis.SchemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw WorkflowPhaseFragment.Invalid(sourcePath, $"phase impact schemaVersion '{analysis.SchemaVersion}' is unsupported.");
        }

        if (string.IsNullOrWhiteSpace(analysis.WorkflowId)
            || analysis.Status is not ("targeted" or "fullRevalidation")
            || !analysis.FullWorkflowCompileRequired
            || analysis.RevalidatePhaseIds is null
            || analysis.ReusablePhaseIds is null
            || analysis.RemovedPhaseIds is null
            || analysis.Reasons is null)
        {
            throw WorkflowPhaseFragment.Invalid(sourcePath, "workflowId, supported status, fullWorkflowCompileRequired=true, and all result arrays are required.");
        }

        if (analysis.FullRevalidationRequired != string.Equals(analysis.Status, "fullRevalidation", StringComparison.Ordinal))
        {
            throw WorkflowPhaseFragment.Invalid(sourcePath, "status must agree with fullRevalidationRequired.");
        }

        ValidateIds(analysis.RevalidatePhaseIds, "revalidatePhaseIds", sourcePath);
        ValidateIds(analysis.ReusablePhaseIds, "reusablePhaseIds", sourcePath);
        ValidateIds(analysis.RemovedPhaseIds, "removedPhaseIds", sourcePath);
        if (analysis.RevalidatePhaseIds.Intersect(analysis.ReusablePhaseIds, StringComparer.Ordinal).Any())
        {
            throw WorkflowPhaseFragment.Invalid(sourcePath, "a phase cannot be both revalidated and reusable.");
        }

        if (analysis.Reasons.Any(static reason => reason is null
            || string.IsNullOrWhiteSpace(reason.Code)
            || string.IsNullOrWhiteSpace(reason.Message)))
        {
            throw WorkflowPhaseFragment.Invalid(sourcePath, "reasons must contain non-empty codes and messages.");
        }
    }

    private static void ValidateIds(IReadOnlyList<string> ids, string propertyName, string sourcePath)
    {
        if (ids.Any(string.IsNullOrWhiteSpace)
            || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count
            || !ids.SequenceEqual(ids.OrderBy(static id => id, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw WorkflowPhaseFragment.Invalid(sourcePath, $"{propertyName} must contain sorted unique non-empty strings.");
        }
    }
}

public static class WorkflowPhaseImpactAnalyzer
{
    public static WorkflowPhaseImpactAnalysis Analyze(
        WorkflowPhaseImpactSnapshot previous,
        WorkflowPhaseImpactSnapshot candidate,
        IReadOnlyCollection<string> authoredChangedPhaseIds)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(authoredChangedPhaseIds);
        ValidateSnapshot(previous, "previous");
        ValidateSnapshot(candidate, "candidate");

        var previousPhases = previous.Skeleton.Phases.ToDictionary(static phase => phase.Id, StringComparer.Ordinal);
        var candidatePhases = candidate.Skeleton.Phases.ToDictionary(static phase => phase.Id, StringComparer.Ordinal);
        var impacted = new HashSet<string>(StringComparer.Ordinal);
        var reasons = new List<WorkflowPhaseImpactReason>();
        var reasonKeys = new HashSet<(string Code, string? PhaseId, string? ContextPath)>();
        var fullRevalidationRequired = false;

        void AddReason(string code, string message, string? phaseId = null, string? contextPath = null)
        {
            if (reasonKeys.Add((code, phaseId, contextPath)))
            {
                reasons.Add(new WorkflowPhaseImpactReason(code, message, phaseId, contextPath));
            }
        }

        void MarkPhase(string phaseId, string code, string message, string? contextPath = null)
        {
            if (candidatePhases.ContainsKey(phaseId))
            {
                impacted.Add(phaseId);
                AddReason(code, message, phaseId, contextPath);
            }
        }

        void MarkAll(string code, string message)
        {
            fullRevalidationRequired = true;
            AddReason(code, message);
            foreach (var phase in candidate.Skeleton.Phases)
            {
                MarkPhase(phase.Id, code, message);
            }
        }

        if (!string.Equals(previous.Skeleton.WorkflowId, candidate.Skeleton.WorkflowId, StringComparison.Ordinal))
        {
            MarkAll("LOOM.PHASE.IMPACT.WORKFLOW_CHANGED", "The workflow identity changed; no prior phase result can be reused.");
        }

        if (!string.Equals(previous.Catalog.SchemaVersion, candidate.Catalog.SchemaVersion, StringComparison.Ordinal))
        {
            MarkAll("LOOM.PHASE.IMPACT.CATALOG_VERSION_CHANGED", "The context catalog schema version changed; all phases require revalidation.");
        }

        if (!AreStructurallyEqual(previous.ExpressionBinding, candidate.ExpressionBinding))
        {
            MarkAll("LOOM.PHASE.IMPACT.EXPRESSION_BINDING_CHANGED", "The workflow expression binding changed; all phases require revalidation.");
        }

        if (!AreStructurallyEqual(previous.Validation, candidate.Validation))
        {
            MarkAll("LOOM.PHASE.IMPACT.VALIDATION_CONTRACT_CHANGED", "The workflow validation contract changed; all phases require revalidation.");
        }

        var previousSkeletonValidation = WorkflowPhaseSkeletonValidator.Validate(previous.Skeleton, previous.Catalog, "previous impact skeleton");
        if (!previousSkeletonValidation.IsValid)
        {
            MarkAll("LOOM.PHASE.IMPACT.PREVIOUS_SKELETON_INVALID", "The previous skeleton or catalog is invalid, so reuse cannot be established.");
        }

        var candidateSkeletonValidation = WorkflowPhaseSkeletonValidator.Validate(candidate.Skeleton, candidate.Catalog, "candidate impact skeleton");
        if (!candidateSkeletonValidation.IsValid)
        {
            MarkAll("LOOM.PHASE.IMPACT.CANDIDATE_SKELETON_INVALID", "The candidate skeleton or catalog is invalid; revalidate every candidate phase after repair.");
        }

        var previousResults = IndexResults(previous, previousPhases, "previous", MarkAll, MarkPhase);
        var candidateResults = IndexResults(candidate, candidatePhases, "candidate", MarkAll, MarkPhase, previousPhases.Keys.ToHashSet(StringComparer.Ordinal));

        foreach (var phase in candidate.Skeleton.Phases)
        {
            if (!previousPhases.TryGetValue(phase.Id, out var previousPhase))
            {
                MarkPhase(phase.Id, "LOOM.PHASE.IMPACT.PHASE_ADDED", $"Phase '{phase.Id}' is new and has no reusable result.");
            }
            else if (!ArePhaseDefinitionsEqual(previousPhase, phase))
            {
                MarkPhase(phase.Id, "LOOM.PHASE.IMPACT.PHASE_DEFINITION_CHANGED", $"The structured definition or frozen interface of phase '{phase.Id}' changed.");
            }

            if (!previousResults.ContainsKey(phase.Id))
            {
                MarkPhase(phase.Id, "LOOM.PHASE.IMPACT.PRIOR_RESULT_MISSING", $"Phase '{phase.Id}' has no valid passing prior result to reuse.");
            }
        }

        foreach (var previousResult in previousResults.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!candidatePhases.ContainsKey(previousResult.Key)
                || !string.Equals(previous.Skeleton.WorkflowId, candidate.Skeleton.WorkflowId, StringComparison.Ordinal))
            {
                continue;
            }

            var compatibility = RevalidateResult(candidate, previousResult.Value, $"candidate reuse/{previousResult.Key}");
            if (!string.Equals(compatibility.Status, "passed", StringComparison.Ordinal))
            {
                MarkPhase(
                    previousResult.Key,
                    "LOOM.PHASE.IMPACT.PREVIOUS_RESULT_INCOMPATIBLE",
                    $"The prior result for phase '{previousResult.Key}' is not compatible with the candidate contract. {GetRevalidationFailure(compatibility)}");
            }
        }

        var removedPhaseIds = previous.Skeleton.Phases
            .Where(phase => !candidatePhases.ContainsKey(phase.Id))
            .Select(static phase => phase.Id)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        foreach (var changedPhaseId in authoredChangedPhaseIds.Distinct(StringComparer.Ordinal).OrderBy(static id => id, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(changedPhaseId))
            {
                MarkAll("LOOM.PHASE.IMPACT.CHANGED_PHASE_UNKNOWN", "A changed phase id is empty; expand revalidation conservatively.");
                continue;
            }
            if (candidatePhases.ContainsKey(changedPhaseId))
            {
                MarkPhase(changedPhaseId, "LOOM.PHASE.IMPACT.AUTHORED_PHASE_CHANGED", $"Phase '{changedPhaseId}' was explicitly identified as edited.");
            }
            else if (!previousPhases.ContainsKey(changedPhaseId))
            {
                MarkAll("LOOM.PHASE.IMPACT.CHANGED_PHASE_UNKNOWN", $"Changed phase id '{changedPhaseId}' is unknown to both snapshots; expand revalidation conservatively.");
            }
        }

        MarkChangedEdges(previous.Skeleton, candidate.Skeleton, candidatePhases, MarkPhase);
        MarkChangedCatalogPaths(previous, previousResults, candidate, candidatePhases, MarkPhase);
        CompareCandidateResults(candidateResults, previousResults, candidatePhases, MarkPhase, MarkAll);
        ExpandDependents(candidate.Skeleton, impacted, reasons, reasonKeys);
        if (candidateResults.Count == 0
            && authoredChangedPhaseIds.Count == 0
            && impacted.Count == 0)
        {
            MarkAll(
                "LOOM.PHASE.IMPACT.CANDIDATE_EVIDENCE_MISSING",
                "No candidate phase results or authored changed-phase ids were supplied, so candidate fragment compatibility cannot be established.");
        }

        if (fullRevalidationRequired)
        {
            foreach (var phase in candidate.Skeleton.Phases)
            {
                impacted.Add(phase.Id);
            }
        }

        var revalidatePhaseIds = impacted.OrderBy(static id => id, StringComparer.Ordinal).ToArray();
        var reusablePhaseIds = candidate.Skeleton.Phases
            .Select(static phase => phase.Id)
            .Where(phaseId => !impacted.Contains(phaseId) && previousResults.ContainsKey(phaseId))
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        var requiresFullRevalidation = fullRevalidationRequired || revalidatePhaseIds.Length == candidate.Skeleton.Phases.Count;
        var analysis = new WorkflowPhaseImpactAnalysis(
            WorkflowPhaseImpactAnalysis.CurrentSchemaVersion,
            candidate.Skeleton.WorkflowId,
            requiresFullRevalidation ? "fullRevalidation" : "targeted",
            true,
            requiresFullRevalidation,
            revalidatePhaseIds,
            reusablePhaseIds,
            removedPhaseIds,
            reasons.OrderBy(static reason => reason.PhaseId, StringComparer.Ordinal)
                .ThenBy(static reason => reason.Code, StringComparer.Ordinal)
                .ThenBy(static reason => reason.ContextPath, StringComparer.Ordinal)
                .ToArray());
        WorkflowPhaseImpactAnalysis.Validate(analysis, "phase impact analysis");
        return analysis;
    }

    private static void ValidateSnapshot(WorkflowPhaseImpactSnapshot snapshot, string label)
    {
        ArgumentNullException.ThrowIfNull(snapshot.Skeleton);
        ArgumentNullException.ThrowIfNull(snapshot.Catalog);
        ArgumentNullException.ThrowIfNull(snapshot.ExpressionBinding);
        ArgumentNullException.ThrowIfNull(snapshot.PhaseResults);
    }

    private static Dictionary<string, WorkflowPhaseCompileResult> IndexResults(
        WorkflowPhaseImpactSnapshot snapshot,
        IReadOnlyDictionary<string, WorkflowPhaseDefinition> phases,
        string label,
        Action<string, string> markAll,
        Action<string, string, string, string?> markPhase,
        IReadOnlySet<string>? previouslyKnownPhaseIds = null)
    {
        var results = new Dictionary<string, WorkflowPhaseCompileResult>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var result in snapshot.PhaseResults)
        {
            if (result is null)
            {
                markAll("LOOM.PHASE.IMPACT.RESULT_INVALID", $"The {label} phase result set contains a null result.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(result.PhaseId))
            {
                markAll("LOOM.PHASE.IMPACT.RESULT_INVALID", $"The {label} phase result is missing its phase id.");
                continue;
            }

            if (!phases.ContainsKey(result.PhaseId))
            {
                if (previouslyKnownPhaseIds?.Contains(result.PhaseId) == true)
                {
                    continue;
                }

                markAll("LOOM.PHASE.IMPACT.RESULT_PHASE_UNKNOWN", $"The {label} phase result references unknown phase '{result.PhaseId}'.");
                continue;
            }

            if (!seen.Add(result.PhaseId))
            {
                results.Remove(result.PhaseId);
                markPhase(result.PhaseId, "LOOM.PHASE.IMPACT.RESULT_DUPLICATE", $"The {label} phase result set contains duplicate results for '{result.PhaseId}'.", null);
                continue;
            }

            try
            {
                WorkflowPhaseCompileResult.Validate(result, $"{label} impact result");
            }
            catch (InvalidOperationException exception)
            {
                markPhase(result.PhaseId, "LOOM.PHASE.IMPACT.RESULT_INVALID", $"The {label} result for '{result.PhaseId}' is invalid: {exception.Message}", null);
                continue;
            }

            if (!string.Equals(result.WorkflowId, snapshot.Skeleton.WorkflowId, StringComparison.Ordinal)
                || !string.Equals(result.SkeletonSchemaVersion, snapshot.Skeleton.SchemaVersion, StringComparison.Ordinal))
            {
                markPhase(result.PhaseId, "LOOM.PHASE.IMPACT.RESULT_IDENTITY_MISMATCH", $"The {label} result for '{result.PhaseId}' belongs to a different workflow or skeleton version.", null);
                continue;
            }

            if (!string.Equals(result.Status, "passed", StringComparison.Ordinal) || result.Fragment is null)
            {
                markPhase(result.PhaseId, "LOOM.PHASE.IMPACT.RESULT_NOT_PASSING", $"The {label} result for '{result.PhaseId}' is not a reusable passing result.", null);
                continue;
            }

            var compatibility = RevalidateResult(snapshot, result, $"{label} impact result/{result.PhaseId}");
            if (!string.Equals(compatibility.Status, "passed", StringComparison.Ordinal))
            {
                markPhase(
                    result.PhaseId,
                    "LOOM.PHASE.IMPACT.RESULT_INCOMPATIBLE",
                    $"The {label} result for '{result.PhaseId}' is not statically compatible with its snapshot. {GetRevalidationFailure(compatibility)}",
                    null);
                continue;
            }

            results.Add(result.PhaseId, result);
        }

        return results;
    }

    private static WorkflowPhaseCompileResult RevalidateResult(
        WorkflowPhaseImpactSnapshot snapshot,
        WorkflowPhaseCompileResult result,
        string sourcePath)
        => WorkflowPhaseCompiler.Compile(
            snapshot.Skeleton,
            result.PhaseId,
            result.Fragment!,
            snapshot.ExpressionBinding,
            snapshot.Catalog,
            snapshot.Validation,
            sourcePath);

    private static string GetRevalidationFailure(WorkflowPhaseCompileResult result)
    {
        var messages = result.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == WorkflowPhaseDiagnosticSeverity.Error)
            .Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
            .Concat(result.InterfaceChangeRequests.Select(static request => $"{request.Kind}: {request.Reason}"))
            .ToArray();
        return messages.Length == 0
            ? "The static phase compiler did not return a passing result."
            : string.Join(" ", messages);
    }

    private static void MarkChangedEdges(
        WorkflowPhaseSkeleton previous,
        WorkflowPhaseSkeleton candidate,
        IReadOnlyDictionary<string, WorkflowPhaseDefinition> candidatePhases,
        Action<string, string, string, string?> markPhase)
    {
        var previousEdges = previous.Routes.Select(static edge => (Kind: "route", Edge: edge))
            .Concat(previous.Backedges.Select(static edge => (Kind: "backedge", Edge: edge)))
            .ToHashSet();
        var candidateEdges = candidate.Routes.Select(static edge => (Kind: "route", Edge: edge))
            .Concat(candidate.Backedges.Select(static edge => (Kind: "backedge", Edge: edge)))
            .ToHashSet();
        var changedEdges = previousEdges.Except(candidateEdges)
            .Concat(candidateEdges.Except(previousEdges))
            .Distinct()
            .OrderBy(static edge => edge.Kind, StringComparer.Ordinal)
            .ThenBy(static edge => edge.Edge.FromPhaseId, StringComparer.Ordinal)
            .ThenBy(static edge => edge.Edge.FromExitId, StringComparer.Ordinal)
            .ThenBy(static edge => edge.Edge.ToPhaseId, StringComparer.Ordinal)
            .ThenBy(static edge => edge.Edge.ToEntryId, StringComparer.Ordinal);
        foreach (var changed in changedEdges)
        {
            var message = $"Declared {changed.Kind} '{changed.Edge.FromPhaseId}.{changed.Edge.FromExitId}' to '{changed.Edge.ToPhaseId}.{changed.Edge.ToEntryId}' changed.";
            markPhase(changed.Edge.FromPhaseId, "LOOM.PHASE.IMPACT.ROUTE_CHANGED", message, null);
            if (candidatePhases.ContainsKey(changed.Edge.ToPhaseId))
            {
                markPhase(changed.Edge.ToPhaseId, "LOOM.PHASE.IMPACT.ROUTE_CHANGED", message, null);
            }
        }
    }

    private static void MarkChangedCatalogPaths(
        WorkflowPhaseImpactSnapshot previous,
        IReadOnlyDictionary<string, WorkflowPhaseCompileResult> previousResults,
        WorkflowPhaseImpactSnapshot candidate,
        IReadOnlyDictionary<string, WorkflowPhaseDefinition> candidatePhases,
        Action<string, string, string, string?> markPhase)
    {
        var changedPaths = previous.Catalog.Fields.Keys
            .Union(candidate.Catalog.Fields.Keys, StringComparer.Ordinal)
            .Where(path => !previous.Catalog.Fields.TryGetValue(path, out var previousField)
                || !candidate.Catalog.Fields.TryGetValue(path, out var candidateField)
                || !AreStructurallyEqual(previousField, candidateField))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        if (changedPaths.Length == 0)
        {
            return;
        }

        foreach (var path in changedPaths)
        {
            foreach (var phase in previous.Skeleton.Phases)
            {
                if (phase.Imports.Concat(phase.Exports).Any(interfacePath => PathsOverlap(interfacePath, path)))
                {
                    markPhase(phase.Id, "LOOM.PHASE.IMPACT.CONTEXT_FIELD_CHANGED", $"Owning context field '{path}' changed and intersects a declared phase interface.", path);
                }
            }

            foreach (var phase in candidate.Skeleton.Phases)
            {
                if (phase.Imports.Concat(phase.Exports).Any(interfacePath => PathsOverlap(interfacePath, path)))
                {
                    markPhase(phase.Id, "LOOM.PHASE.IMPACT.CONTEXT_FIELD_CHANGED", $"Owning context field '{path}' changed and intersects a declared phase interface.", path);
                }
            }
        }

        var compiler = new CSharpExpressionCompiler();
        foreach (var pair in previousResults.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            var previousPhase = previous.Skeleton.Phases.FirstOrDefault(phase => string.Equals(phase.Id, pair.Key, StringComparison.Ordinal));
            if (!candidatePhases.ContainsKey(pair.Key) || previousPhase is null)
            {
                continue;
            }

            if (!TryGetUsedContextPaths(previous, previousPhase, pair.Value.Fragment!, compiler, out var usedPaths, out var failure))
            {
                markPhase(pair.Key, "LOOM.PHASE.IMPACT.PRIOR_USAGE_UNKNOWN", failure, null);
                continue;
            }

            foreach (var path in usedPaths.Where(usedPath => changedPaths.Any(changedPath => PathsOverlap(usedPath, changedPath))))
            {
                markPhase(pair.Key, "LOOM.PHASE.IMPACT.CONTEXT_FIELD_USED", $"Phase '{pair.Key}' reads or writes changed context field '{path}'.", path);
            }
        }
    }

    private static bool TryGetUsedContextPaths(
        WorkflowPhaseImpactSnapshot snapshot,
        WorkflowPhaseDefinition phase,
        WorkflowPhaseFragment fragment,
        CSharpExpressionCompiler compiler,
        out HashSet<string> usedPaths,
        out string failure)
    {
        usedPaths = new HashSet<string>(StringComparer.Ordinal);
        var diagnostics = new List<WorkflowPhaseDiagnostic>();
        foreach (var expression in WorkflowPhaseCompiler.EnumerateExpressions(fragment, phase, snapshot.Validation, diagnostics))
        {
            var result = compiler.CompileWithContextFieldCatalog(
                snapshot.ExpressionBinding,
                expression.Definition,
                snapshot.Catalog,
                expression.Location);
            foreach (var access in result.ContextFieldAccesses)
            {
                usedPaths.Add(access.Path);
            }

            if (!result.IsSuccess)
            {
                failure = $"Could not prove context usage for phase '{phase.Id}' because '{expression.Location}' does not compile against its baseline contract.";
                return false;
            }
        }

        if (diagnostics.Any(static diagnostic => diagnostic.Severity == WorkflowPhaseDiagnosticSeverity.Error))
        {
            failure = $"Could not prove context usage for phase '{phase.Id}' because one or more declared gates are missing.";
            return false;
        }

        foreach (var transition in fragment.Nodes.Values.OfType<CommandTransition>())
        {
            if (!string.IsNullOrWhiteSpace(transition.OutputPath))
            {
                usedPaths.Add(transition.OutputPath);
            }

            foreach (var parameterName in new[] { "updates", "outputBindings" })
            {
                if (transition.Command.Parameters?.TryGetValue(parameterName, out var mapValue) != true)
                {
                    continue;
                }

                foreach (var path in GetMapKeys(mapValue))
                {
                    usedPaths.Add(path);
                }
            }
        }

        foreach (var commandRead in WorkflowPhaseCompiler.EnumerateCommandContextReads(fragment))
        {
            usedPaths.Add(commandRead.Path);
        }

        failure = string.Empty;
        return true;
    }

    private static IEnumerable<string> GetMapKeys(object? value)
        => value switch
        {
            IDictionary<string, object?> mutable => mutable.Keys,
            IReadOnlyDictionary<string, object?> readOnly => readOnly.Keys,
            JsonElement { ValueKind: JsonValueKind.Object } element => element.EnumerateObject()
                .Select(static property => property.Name),
            _ => [],
        };

    private static void CompareCandidateResults(
        IReadOnlyDictionary<string, WorkflowPhaseCompileResult> candidateResults,
        IReadOnlyDictionary<string, WorkflowPhaseCompileResult> previousResults,
        IReadOnlyDictionary<string, WorkflowPhaseDefinition> candidatePhases,
        Action<string, string, string, string?> markPhase,
        Action<string, string> markAll)
    {
        foreach (var pair in candidateResults.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!candidatePhases.ContainsKey(pair.Key))
            {
                markAll("LOOM.PHASE.IMPACT.CANDIDATE_RESULT_UNKNOWN", $"Candidate results contain unknown phase '{pair.Key}'.");
                continue;
            }

            if (!previousResults.TryGetValue(pair.Key, out var previousResult) || previousResult.Fragment is null)
            {
                markPhase(pair.Key, "LOOM.PHASE.IMPACT.PRIOR_RESULT_MISSING", $"Phase '{pair.Key}' has no prior fragment for a structural comparison.", null);
                continue;
            }

            if (pair.Value.Fragment is null || !AreFragmentsEqual(previousResult.Fragment, pair.Value.Fragment))
            {
                markPhase(pair.Key, "LOOM.PHASE.IMPACT.FRAGMENT_CHANGED", $"The structured node definitions for phase '{pair.Key}' changed.", null);
            }
        }
    }

    private static void ExpandDependents(
        WorkflowPhaseSkeleton skeleton,
        ISet<string> impacted,
        ICollection<WorkflowPhaseImpactReason> reasons,
        ISet<(string Code, string? PhaseId, string? ContextPath)> reasonKeys)
    {
        var queue = new Queue<string>(impacted.OrderBy(static id => id, StringComparer.Ordinal));
        while (queue.TryDequeue(out var changedPhaseId))
        {
            foreach (var dependent in skeleton.Phases
                .Where(phase => phase.Dependencies.Contains(changedPhaseId, StringComparer.Ordinal))
                .OrderBy(static phase => phase.Id, StringComparer.Ordinal))
            {
                if (!impacted.Add(dependent.Id))
                {
                    continue;
                }

                const string code = "LOOM.PHASE.IMPACT.DEPENDENT_INVALIDATED";
                var message = $"Phase '{dependent.Id}' depends on changed phase '{changedPhaseId}' and must be revalidated.";
                if (reasonKeys.Add((code, dependent.Id, null)))
                {
                    reasons.Add(new WorkflowPhaseImpactReason(code, message, dependent.Id));
                }

                queue.Enqueue(dependent.Id);
            }
        }
    }

    private static bool ArePhaseDefinitionsEqual(WorkflowPhaseDefinition first, WorkflowPhaseDefinition second)
        => string.Equals(first.Id, second.Id, StringComparison.Ordinal)
            && string.Equals(first.Prefix, second.Prefix, StringComparison.Ordinal)
            && string.Equals(first.Mode, second.Mode, StringComparison.Ordinal)
            && first.Entries.SequenceEqual(second.Entries, StringComparer.Ordinal)
            && first.Exits.SequenceEqual(second.Exits, StringComparer.Ordinal)
            && first.Imports.SequenceEqual(second.Imports, StringComparer.Ordinal)
            && first.Exports.SequenceEqual(second.Exports, StringComparer.Ordinal)
            && first.Dependencies.SequenceEqual(second.Dependencies, StringComparer.Ordinal)
            && first.GateIds.SequenceEqual(second.GateIds, StringComparer.Ordinal)
            && first.TerminalRoutes.ToHashSet(StringComparer.Ordinal).SetEquals(second.TerminalRoutes);

    private static bool AreFragmentsEqual(WorkflowPhaseFragment first, WorkflowPhaseFragment second)
    {
        var firstNormalized = first with
        {
            Nodes = first.Nodes.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
        };
        var secondNormalized = second with
        {
            Nodes = second.Nodes.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
        };
        return JsonNode.DeepEquals(
            JsonNode.Parse(firstNormalized.ToJson(indented: false)),
            JsonNode.Parse(secondNormalized.ToJson(indented: false)));
    }

    private static bool AreStructurallyEqual<T>(T? first, T? second)
    {
        var options = WorkflowJsonSerializer.CreateDefaultOptions(indented: false);
        return JsonNode.DeepEquals(
            JsonSerializer.SerializeToNode(first, options),
            JsonSerializer.SerializeToNode(second, options));
    }

    private static bool PathsOverlap(string first, string second)
    {
        var normalizedFirst = first.TrimEnd('.');
        var normalizedSecond = second.TrimEnd('.');
        return string.Equals(normalizedFirst, normalizedSecond, StringComparison.Ordinal)
            || normalizedFirst.StartsWith(normalizedSecond + ".", StringComparison.Ordinal)
            || normalizedSecond.StartsWith(normalizedFirst + ".", StringComparison.Ordinal);
    }
}
