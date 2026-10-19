using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class WorkflowPhaseCompiler
{
    public static WorkflowPhaseCompileResult Compile(
        WorkflowPhaseSkeleton skeleton,
        string phaseId,
        WorkflowPhaseFragment fragment,
        ExpressionBinding expressionBinding,
        ContextFieldCatalog catalog,
        WorkflowValidationContract? validation,
        string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseId);
        ArgumentNullException.ThrowIfNull(fragment);
        ArgumentNullException.ThrowIfNull(expressionBinding);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var phase = skeleton.Phases.FirstOrDefault(candidate => string.Equals(candidate.Id, phaseId, StringComparison.Ordinal));
        if (phase is null)
        {
            throw WorkflowPhaseFragment.Invalid(sourcePath, $"skeleton does not declare phase '{phaseId}'.");
        }

        WorkflowPhaseFragment.Validate(fragment, sourcePath);
        if (!string.Equals(fragment.WorkflowId, skeleton.WorkflowId, StringComparison.Ordinal)
            || !string.Equals(fragment.SkeletonSchemaVersion, skeleton.SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(fragment.PhaseId, phase.Id, StringComparison.Ordinal))
        {
            throw WorkflowPhaseFragment.Invalid(sourcePath, "fragment workflowId, skeletonSchemaVersion, and phaseId must match the supplied skeleton and phase.");
        }

        fragment = fragment with
        {
            Nodes = fragment.Nodes.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
        };

        var diagnostics = WorkflowPhaseSkeletonValidator.Validate(skeleton, catalog, sourcePath).Diagnostics.ToList();
        var interfaceChangeRequests = new List<WorkflowPhaseInterfaceChangeRequest>();
        var requestKeys = new HashSet<(string Kind, string? ContextPath, string? NodeId)>();
        if (diagnostics.Any(static diagnostic => diagnostic.Severity == WorkflowPhaseDiagnosticSeverity.Error))
        {
            return CreateResult(skeleton, phase, diagnostics, interfaceChangeRequests, null, sourcePath);
        }

        ValidateFragmentStructure(skeleton, phase, fragment, diagnostics);
        var expressionCompiler = new CSharpExpressionCompiler();
        foreach (var expression in EnumerateExpressions(fragment, phase, validation, diagnostics))
        {
            var result = expressionCompiler.CompileWithContextFieldCatalog(
                expressionBinding,
                expression.Definition,
                catalog,
                expression.Location);
            var undeclaredAccesses = result.ContextFieldAccesses
                .Where(static access => !access.IsCatalogDeclared)
                .ToArray();

            foreach (var access in undeclaredAccesses)
            {
                AddInterfaceChangeRequest(
                    interfaceChangeRequests,
                    requestKeys,
                    "addContextField",
                    $"Expression '{expression.Location}' reads '{access.Path}', which is not declared in the owning context_fields catalog.",
                    access.Path,
                    expression.NodeId);
            }

            if (!result.IsSuccess)
            {
                if (string.Equals(result.Feedback.DiagnosticCode, "LOOM.EXPR.CONTEXT.FIELD_UNDECLARED", StringComparison.Ordinal)
                    && undeclaredAccesses.Length != 0)
                {
                    continue;
                }

                AddError(
                    diagnostics,
                    result.Feedback.DiagnosticCode,
                    $"{expression.Location}: {result.Feedback.Message}",
                    phase.Id,
                    result.ContextFieldAccesses.FirstOrDefault()?.Path);
                continue;
            }

            foreach (var access in result.ContextFieldAccesses)
            {
                if (!access.IsCatalogDeclared)
                {
                    continue;
                }

                if (!IsPathCoveredByInterface(access.Path, phase.Imports)
                    && !IsPathCoveredByInterface(access.Path, phase.Exports))
                {
                    AddInterfaceChangeRequest(
                        interfaceChangeRequests,
                        requestKeys,
                        "addImport",
                        $"Expression '{expression.Location}' reads '{access.Path}', which is outside the frozen phase imports and exports.",
                        access.Path,
                        expression.NodeId);
                }
            }
        }

        foreach (var commandRead in EnumerateCommandContextReads(fragment, catalog))
        {
            if (IsPathCoveredByInterface(commandRead.Path, phase.Imports)
                || IsPathCoveredByInterface(commandRead.Path, phase.Exports))
            {
                continue;
            }

            AddInterfaceChangeRequest(
                interfaceChangeRequests,
                requestKeys,
                "addImport",
                $"Command transition '{commandRead.NodeId}' reads context path '{commandRead.Path}' outside the frozen phase imports and exports.",
                commandRead.Path,
                commandRead.NodeId);
        }

        var projectionInstance = new WorkflowInstance
        {
            Nodes = fragment.Nodes.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
        };
        foreach (var target in GetProjectionTargets(fragment))
        {
            if (!catalog.TryGetField(target.Path, out _))
            {
                AddInterfaceChangeRequest(
                    interfaceChangeRequests,
                    requestKeys,
                    "addContextField",
                    $"Projection from transition '{target.NodeId}' writes '{target.Path}', which is not declared in the owning context_fields catalog.",
                    target.Path,
                    target.NodeId);
            }
            else if (!IsPathCoveredByInterface(target.Path, phase.Exports))
            {
                AddInterfaceChangeRequest(
                    interfaceChangeRequests,
                    requestKeys,
                    "addExport",
                    $"Projection from transition '{target.NodeId}' writes '{target.Path}', which is outside the frozen phase exports.",
                    target.Path,
                    target.NodeId);
            }
        }

        foreach (var projectionDiagnostic in ContextFieldProjectionValidator.Validate(projectionInstance, catalog, sourcePath))
        {
            if (string.Equals(projectionDiagnostic.Code, "LOOM.CONTEXT.PROJECTION.TARGET_UNDECLARED", StringComparison.Ordinal))
            {
                continue;
            }

            AddError(
                diagnostics,
                projectionDiagnostic.Code,
                $"{projectionDiagnostic.Location}: {projectionDiagnostic.Message}",
                phase.Id);
        }

        return CreateResult(skeleton, phase, diagnostics, interfaceChangeRequests, fragment, sourcePath);
    }

    private static void ValidateFragmentStructure(
        WorkflowPhaseSkeleton skeleton,
        WorkflowPhaseDefinition phase,
        WorkflowPhaseFragment fragment,
        ICollection<WorkflowPhaseDiagnostic> diagnostics)
    {
        var syntheticIds = WorkflowPhaseCompileResult.GetSyntheticBoundaryIds(phase.Id).ToHashSet(StringComparer.Ordinal);
        var routes = skeleton.Routes.Concat(skeleton.Backedges).ToArray();
        foreach (var pair in fragment.Nodes)
        {
            if (syntheticIds.Contains(pair.Key))
            {
                AddError(diagnostics, "LOOM.PHASE.SYNTHETIC_NODE", $"Fragment contains reserved synthetic harness node '{pair.Key}'.", phase.Id);
            }

            if (phase.Mode == "phaseSliced" && !pair.Key.StartsWith(phase.Prefix, StringComparison.Ordinal))
            {
                AddError(diagnostics, "LOOM.PHASE.NODE_PREFIX", $"Node '{pair.Key}' does not use phase prefix '{phase.Prefix}'.", phase.Id);
            }

            if (pair.Value is StateNode state)
            {
                foreach (var transitionId in state.Groups.SelectMany(static group => group.TransitionIds))
                {
                    if (!fragment.Nodes.TryGetValue(transitionId, out var transition) || transition is not TransitionBase)
                    {
                        AddError(diagnostics, "LOOM.PHASE.TRANSITION_MISSING", $"State '{state.Id}' references missing transition '{transitionId}'.", phase.Id);
                    }
                }
            }
            else if (pair.Value is TransitionBase transition
                && transition.TargetNodeId is { Length: > 0 } targetId
                && !fragment.Nodes.ContainsKey(targetId)
                && !routes.Any(route => string.Equals(route.FromPhaseId, phase.Id, StringComparison.Ordinal)
                    && string.Equals(route.FromExitId, transition.Id, StringComparison.Ordinal)
                    && string.Equals(route.ToEntryId, targetId, StringComparison.Ordinal)))
            {
                AddError(diagnostics, "LOOM.PHASE.ROUTE_UNDECLARED", $"Transition '{transition.Id}' targets '{targetId}' outside its fragment without a declared phase route or backedge.", phase.Id);
            }
        }

        foreach (var entryId in phase.Entries)
        {
            if (!fragment.Nodes.TryGetValue(entryId, out var entry) || entry is not StateNode)
            {
                AddError(diagnostics, "LOOM.PHASE.ENTRY_MISSING", $"Phase entry '{entryId}' must be a state node in its fragment.", phase.Id);
            }
        }

        foreach (var exitId in phase.Exits)
        {
            if (!fragment.Nodes.TryGetValue(exitId, out var exit) || exit is not TransitionBase)
            {
                AddError(diagnostics, "LOOM.PHASE.EXIT_MISSING", $"Phase exit '{exitId}' must be a transition in its fragment.", phase.Id);
            }
        }

        foreach (var route in routes.Where(route => string.Equals(route.FromPhaseId, phase.Id, StringComparison.Ordinal)))
        {
            if (!fragment.Nodes.TryGetValue(route.FromExitId, out var source) || source is not TransitionBase transition)
            {
                AddError(diagnostics, "LOOM.PHASE.ROUTE_EXIT_MISSING", $"Declared route exit '{route.FromExitId}' must be a transition in the phase fragment.", phase.Id);
                continue;
            }

            if (transition.TargetNodeId is { Length: > 0 } targetId
                && !string.Equals(targetId, route.ToEntryId, StringComparison.Ordinal))
            {
                AddError(
                    diagnostics,
                    "LOOM.PHASE.ROUTE_TARGET_CONFLICT",
                    $"Declared route exit '{route.FromExitId}' targets '{targetId}', but the frozen phase edge requires '{route.ToEntryId}'.",
                    phase.Id);
            }
        }

        static IEnumerable<string> GetTerminalRoutes(TransitionBase transition)
        {
            var declaredRoutes = transition.TerminalRoutes;
            if (declaredRoutes is { Count: > 0 })
            {
                return declaredRoutes.Where(static route => !string.IsNullOrWhiteSpace(route));
            }

            if (transition is not CommandTransition commandTransition
                || commandTransition.Command.Parameters?.TryGetValue("terminalRoutes", out var value) != true)
            {
                return [];
            }

            return value switch
            {
                string route when !string.IsNullOrWhiteSpace(route) => [route],
                IEnumerable<string> routes => routes.Where(static route => !string.IsNullOrWhiteSpace(route)),
                IEnumerable<object?> routes => routes
                    .Select(static route => Convert.ToString(route))
                    .Where(static route => !string.IsNullOrWhiteSpace(route))
                    .Cast<string>(),
                _ => [],
            };
        }

        var phaseTerminalRoutes = phase.TerminalRoutes.ToHashSet(StringComparer.Ordinal);
        var fragmentTerminalRoutes = fragment.Nodes.Values
            .OfType<TransitionBase>()
            .SelectMany(GetTerminalRoutes)
            .ToHashSet(StringComparer.Ordinal);
        if (!phaseTerminalRoutes.SetEquals(fragmentTerminalRoutes))
        {
            var fragmentOnlyRoutes = fragmentTerminalRoutes
                .Except(phaseTerminalRoutes, StringComparer.Ordinal)
                .OrderBy(static route => route, StringComparer.Ordinal)
                .ToArray();
            var phaseOnlyRoutes = phaseTerminalRoutes
                .Except(fragmentTerminalRoutes, StringComparer.Ordinal)
                .OrderBy(static route => route, StringComparer.Ordinal)
                .ToArray();
            AddError(
                diagnostics,
                "LOOM.PHASE.TERMINAL_ROUTES_MISMATCH",
                $"Phase terminalRoutes must match terminal routes in its fragment. Fragment-only: [{string.Join(", ", fragmentOnlyRoutes)}]; phase-only: [{string.Join(", ", phaseOnlyRoutes)}].",
                phase.Id);
        }
    }

    internal static IEnumerable<PhaseExpression> EnumerateExpressions(
        WorkflowPhaseFragment fragment,
        WorkflowPhaseDefinition phase,
        WorkflowValidationContract? validation,
        ICollection<WorkflowPhaseDiagnostic> diagnostics)
    {
        foreach (var pair in fragment.Nodes.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (pair.Value is StateNode state)
            {
                if (state.StateFailedExpression is not null)
                {
                    yield return new PhaseExpression(state.StateFailedExpression, $"state:{state.Id}/stateFailedExpression", state.Id);
                }

                foreach (var group in state.Groups)
                {
                    if (group.PassExpression is not null)
                    {
                        yield return new PhaseExpression(group.PassExpression, $"state:{state.Id}/groups/{group.Id}/passExpression", state.Id);
                    }

                    if (group.TimeoutTransition is not null)
                    {
                        foreach (var expression in EnumerateTransitionExpressions(group.TimeoutTransition, $"state:{state.Id}/groups/{group.Id}/timeout", group.TimeoutTransition.Id))
                        {
                            yield return expression;
                        }
                    }
                }
            }
            else if (pair.Value is TransitionBase transition)
            {
                foreach (var expression in EnumerateTransitionExpressions(transition, $"transition:{transition.Id}", transition.Id))
                {
                    yield return expression;
                }
            }
        }

        foreach (var gateId in phase.GateIds.OrderBy(static id => id, StringComparer.Ordinal))
        {
            if (validation is null || !validation.Gates.TryGetValue(gateId, out var gate))
            {
                AddError(diagnostics, "LOOM.PHASE.GATE_MISSING", $"Phase declares gate '{gateId}', but the supplied workflow validation contract does not.", phase.Id);
                continue;
            }

            if (gate.PassExpression is not null)
            {
                yield return new PhaseExpression(gate.PassExpression, $"validation.gates.{gateId}.passExpression", null);
            }
        }
    }

    private static IEnumerable<PhaseExpression> EnumerateTransitionExpressions(
        TransitionBase transition,
        string location,
        string? nodeId)
    {
        yield return new PhaseExpression(transition.GuardExpression, $"{location}/guardExpression", nodeId);
        yield return new PhaseExpression(transition.PassExpression, $"{location}/passExpression", nodeId);
    }

    internal static IEnumerable<(string Path, string NodeId)> EnumerateCommandContextReads(
        WorkflowPhaseFragment fragment,
        ContextFieldCatalog catalog)
    {
        foreach (var transition in fragment.Nodes.Values.OfType<CommandTransition>().OrderBy(static node => node.Id, StringComparer.Ordinal))
        {
            if (transition.Plan is not null)
            {
                foreach (var path in transition.Plan.InputPaths)
                {
                    if (catalog.TryGetField(path, out _))
                    {
                        yield return (path, transition.Id);
                    }
                }
            }

            var parameters = transition.Command.Parameters;
            var contextPaths = new HashSet<string>(StringComparer.Ordinal);
            var payloadPaths = GetDeclaredPayloadPaths(parameters);
            foreach (var parameterName in new[] { "updates", "outputBindings" })
            {
                if (parameters?.TryGetValue(parameterName, out var mapValue) != true)
                {
                    continue;
                }

                foreach (var entry in GetMapEntries(mapValue))
                {
                    if (TryGetReferencePath(entry.Value, "$context:", out var path))
                    {
                        contextPaths.Add(path);
                    }
                }
            }

            foreach (var path in contextPaths.OrderBy(static path => path, StringComparer.Ordinal))
            {
                if (catalog.TryGetField(path, out _))
                {
                    yield return (path, transition.Id);
                }
            }

            var requiredInputs = parameters?.TryGetValue("requiredInputs", out var requiredInputsValue) == true
                ? GetStringValues(requiredInputsValue)
                : [];
            foreach (var path in requiredInputs.OrderBy(static path => path, StringComparer.Ordinal))
            {
                if (contextPaths.Contains(path)
                    || (!payloadPaths.Contains(path) && catalog.TryGetField(path, out _)))
                {
                    yield return (path, transition.Id);
                }
            }
        }
    }

    private static HashSet<string> GetDeclaredPayloadPaths(Dictionary<string, object?>? parameters)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var resumeOutputKey = GetStringParameter(parameters, "resumeOutputKey");
        if (!string.IsNullOrWhiteSpace(resumeOutputKey))
        {
            paths.Add(resumeOutputKey);
        }

        foreach (var parameterName in new[] { "updates", "outputBindings" })
        {
            if (parameters?.TryGetValue(parameterName, out var mapValue) != true)
            {
                continue;
            }

            foreach (var entry in GetMapEntries(mapValue))
            {
                if (TryGetReferencePath(entry.Value, "$payload:", out var path))
                {
                    paths.Add(path);
                }
            }
        }

        return paths;
    }

    private static string? GetStringParameter(Dictionary<string, object?>? parameters, string name)
    {
        if (parameters?.TryGetValue(name, out var value) != true)
        {
            return null;
        }

        return value switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => null,
        };
    }

    private static IEnumerable<string> GetStringValues(object? value)
        => value switch
        {
            IEnumerable<object?> values => values.OfType<string>()
                .Where(static item => !string.IsNullOrWhiteSpace(item)),
            JsonElement { ValueKind: JsonValueKind.Array } array => array.EnumerateArray()
                .Where(static item => item.ValueKind == JsonValueKind.String)
                .Select(static item => item.GetString()!)
                .Where(static item => !string.IsNullOrWhiteSpace(item)),
            _ => [],
        };

    private static bool TryGetReferencePath(object? value, string prefix, out string path)
    {
        var text = value switch
        {
            string marker => marker,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => null,
        };
        if (text is not null && text.StartsWith(prefix, StringComparison.Ordinal))
        {
            path = text[prefix.Length..];
            if (!string.IsNullOrWhiteSpace(path))
            {
                return true;
            }
        }

        path = string.Empty;
        return false;
    }

    private static IEnumerable<KeyValuePair<string, object?>> GetMapEntries(object? value)
        => value switch
        {
            IDictionary<string, object?> mutable => mutable,
            IReadOnlyDictionary<string, object?> readOnly => readOnly,
            JsonElement { ValueKind: JsonValueKind.Object } element => element.EnumerateObject()
                .Select(static property => new KeyValuePair<string, object?>(property.Name, property.Value.Clone())),
            _ => [],
        };

    private static IEnumerable<PhaseProjectionTarget> GetProjectionTargets(WorkflowPhaseFragment fragment)
    {
        foreach (var transition in fragment.Nodes.Values.OfType<CommandTransition>().OrderBy(static node => node.Id, StringComparer.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(transition.OutputPath))
            {
                yield return new PhaseProjectionTarget(transition.OutputPath, transition.Id);
            }

            foreach (var parameterName in new[] { "updates", "outputBindings" })
            {
                if (transition.Command.Parameters?.TryGetValue(parameterName, out var mapValue) != true)
                {
                    continue;
                }

                foreach (var path in GetMapKeys(mapValue).OrderBy(static path => path, StringComparer.Ordinal))
                {
                    yield return new PhaseProjectionTarget(path, transition.Id);
                }
            }
        }
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

    private static bool IsPathCoveredByInterface(string path, IEnumerable<string> interfacePaths)
    {
        foreach (var interfacePath in interfacePaths)
        {
            if (string.Equals(path, interfacePath, StringComparison.Ordinal))
            {
                return true;
            }

            var prefix = interfacePath.EndsWith(".", StringComparison.Ordinal) ? interfacePath : interfacePath + ".";
            if (path.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddInterfaceChangeRequest(
        ICollection<WorkflowPhaseInterfaceChangeRequest> requests,
        ISet<(string Kind, string? ContextPath, string? NodeId)> requestKeys,
        string kind,
        string reason,
        string? contextPath,
        string? nodeId)
    {
        if (requestKeys.Add((kind, contextPath, nodeId)))
        {
            requests.Add(new WorkflowPhaseInterfaceChangeRequest(kind, reason, contextPath, nodeId));
        }
    }

    private static void AddError(
        ICollection<WorkflowPhaseDiagnostic> diagnostics,
        string code,
        string message,
        string phaseId,
        string? contextPath = null)
        => diagnostics.Add(new WorkflowPhaseDiagnostic(WorkflowPhaseDiagnosticSeverity.Error, code, message, phaseId, contextPath));

    private static WorkflowPhaseCompileResult CreateResult(
        WorkflowPhaseSkeleton skeleton,
        WorkflowPhaseDefinition phase,
        IReadOnlyCollection<WorkflowPhaseDiagnostic> diagnostics,
        IReadOnlyCollection<WorkflowPhaseInterfaceChangeRequest> interfaceChangeRequests,
        WorkflowPhaseFragment? fragment,
        string sourcePath)
    {
        var status = diagnostics.Any(static diagnostic => diagnostic.Severity == WorkflowPhaseDiagnosticSeverity.Error)
            ? "failed"
            : interfaceChangeRequests.Count > 0
                ? "interfaceChangeRequested"
                : "passed";
        var result = new WorkflowPhaseCompileResult(
            WorkflowPhaseCompileResult.CurrentSchemaVersion,
            skeleton.WorkflowId,
            skeleton.SchemaVersion,
            phase.Id,
            status,
            true,
            WorkflowPhaseCompileResult.GetSyntheticBoundaryIds(phase.Id),
            diagnostics.ToArray(),
            interfaceChangeRequests.ToArray(),
            string.Equals(status, "passed", StringComparison.Ordinal) ? fragment : null);
        WorkflowPhaseCompileResult.Validate(result, sourcePath);
        return result;
    }

    internal sealed record PhaseExpression(ExpressionDefinition Definition, string Location, string? NodeId);

    private sealed record PhaseProjectionTarget(string Path, string NodeId);
}
