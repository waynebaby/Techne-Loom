using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Validation;

internal static class McpFirstGovernedRouteValidator
{
    private const string GovernedTemplateKind = "so-governed-target-skill";
    private const string EvidenceFamily = "mcp_startup_evidence";
    private const string McpAttemptEvidenceFamily = "mcp_registration_attempt_evidence";
    private const string TransportSelector = "governance_entry_transport";
    private const string RuntimeLaunchDescriptorField = "runtime_launch_descriptor_ref";
    private const string McpTransport = "mcp_stdio";
    private const string CliTransport = "cli";
    private const string RequiredMcpTool = "so_inspect_workflow_fragment";
    private const string TransportNeutralRuntimeCommand = "descriptor_owned_mcp_or_cli";
    private const string BusinessGateRule = "SO3000";
    private static readonly string[] AllowedFallbackReasons =
    [
        "mcp_transport_unavailable",
        "mcp_handshake_unsupported",
        "mcp_tool_unavailable",
    ];

    public static void Validate(
        WorkflowInstance instance,
        IReadOnlyDictionary<string, StateNode> states,
        IReadOnlyDictionary<string, TransitionBase> transitions,
        WorkflowValidationResult result)
    {
        if (!string.Equals(instance.TemplateKind, GovernedTemplateKind, StringComparison.Ordinal))
        {
            return;
        }

        ValidateContract(instance.Validation?.GovernanceEntry, result);

        var governanceEntries = transitions.Values
            .OfType<CommandTransition>()
            .Where(transition => HasBooleanParameter(transition.Command.Parameters, "mcpFirst"))
            .ToArray();

        if (governanceEntries.Length != 1)
        {
            result.Add(
                BusinessGateRule,
                "Loom-governanced target-skill workflows must declare exactly one optional-MCP governance-entry transition.",
                "nodes/*/mcpFirst",
                "Declare exactly one transition with mcpFirst=true, mcpRequired=false, mcp_startup_evidence output, and gate.bootstrap_mcp_ready.");
            return;
        }

        var governanceEntry = governanceEntries[0];
        ValidateGovernanceEntry(instance.Validation, governanceEntry, result);

        var preflightExceptions = transitions.Values.Where(IsPreflightException).ToArray();
        if (preflightExceptions.Length != 1)
        {
            result.Add(
                BusinessGateRule,
                "Exactly one external transition must be the exact runtime preflight exception before the optional-MCP governance entry.",
                "nodes/*/mcpPreflightExempt",
                "Keep runtimePreflight/mcpPreflightExempt only on the runtime-preparation WaitResume transition.");
        }

        var stateDepths = BuildStateDepths(instance.StartNodeId, states, transitions);
        var transitionDepths = BuildTransitionDepths(states, transitions, stateDepths);
        var entryIds = governanceEntries.Select(static transition => transition.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var entry in governanceEntries)
        {
            if (!transitionDepths.ContainsKey(entry.Id))
            {
                result.Add(
                    BusinessGateRule,
                    $"Governance-entry transition '{entry.Id}' is not reachable from the workflow start state.",
                    $"transition:{entry.Id}",
                    "Connect the MCP-first transition after runtime preflight and before guide capture.");
            }
        }

        foreach (var transition in transitions.Values)
        {
            if (entryIds.Contains(transition.Id)
                || !IsExternalStep(transition.StepKind)
                || IsPreflightException(transition)
                || !transitionDepths.ContainsKey(transition.Id)
                || !CanReachTransitionWithoutGovernanceEntry(instance.StartNodeId, states, transitions, entryIds, transition.Id))
            {
                continue;
            }

            result.Add(
                BusinessGateRule,
                $"External transition '{transition.Id}' occurs before the governance entry or can bypass both transports.",
                $"transition:{transition.Id}",
                "Route every external step through the MCP-first bounded fragment inspection and its shared gate.");
        }
    }

    private static void ValidateContract(WorkflowGovernanceEntryContract? contract, WorkflowValidationResult result)
    {
        if (contract is null
            || !string.Equals(contract.PreferredTransport, McpTransport, StringComparison.Ordinal)
            || !string.Equals(contract.EvidenceFamily, EvidenceFamily, StringComparison.Ordinal)
            || !string.Equals(contract.McpAttemptEvidenceFamily, McpAttemptEvidenceFamily, StringComparison.Ordinal)
            || !string.Equals(contract.RuntimeLaunchDescriptorField, RuntimeLaunchDescriptorField, StringComparison.Ordinal)
            || contract.AllowedTransports.Count != 2
            || !contract.AllowedTransports.Contains(McpTransport, StringComparer.Ordinal)
            || !contract.AllowedTransports.Contains(CliTransport, StringComparer.Ordinal)
            || AllowedFallbackReasons.Any(reason => !contract.CliFallbackReasons.Contains(reason, StringComparer.Ordinal)))
        {
            result.Add(
                BusinessGateRule,
                "Governed workflows must declare MCP-optional, CLI-capable governance-entry policy.",
                "validation/governanceEntry",
                "Set preferredTransport to mcp_stdio or cli, allowedTransports=[mcp_stdio,cli], evidenceFamily=mcp_startup_evidence, mcpAttemptEvidenceFamily=mcp_registration_attempt_evidence, runtimeLaunchDescriptorField=runtime_launch_descriptor_ref, and the three pre-dispatch CLI reasons.");
        }
    }

    private static void ValidateGovernanceEntry(WorkflowValidationContract? validation, CommandTransition transition, WorkflowValidationResult result)
    {
        var parameters = transition.Command.Parameters ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var usesMcp = transition.StepKind == WorkflowStepKind.McpCall;
        var usesHostContinuation = transition.StepKind == WorkflowStepKind.WaitResume;
        if ((!usesMcp && !usesHostContinuation)
            || !HasBooleanParameter(parameters, "mcpFirst")
            || HasBooleanParameter(parameters, "mcpRequired")
            || !HasCommonEntryShape(validation, transition, parameters)
            || !string.Equals(GetString(parameters, "operationIdInput"), "operation_id", StringComparison.Ordinal)
            || !GetStringList(parameters, "requiredInputs").Contains("operation_id", StringComparer.Ordinal)
            || !GetStringList(parameters, "requiredInputs").Contains("mcp_startup_evidence.operation_id", StringComparer.Ordinal)
            || !HasPayloadInputMatch(parameters, "operation_id", "mcp_startup_evidence.operation_id")
            || !(transition.SatisfiesGateIds ?? []).Contains("gate.bootstrap_mcp_ready", StringComparer.Ordinal))
        {
            result.Add(
                BusinessGateRule,
                "The optional-MCP governance entry must allow CLI, never require MCP, and project bounded mcp_startup_evidence canonically.",
                $"transition:{transition.Id}",
                "Set mcpFirst=true as an optional attempt priority, mcpRequired=false, provide descriptor-owned MCP/CLI dispatch, preserve operation_id, workflow identity and bounded result hashes, project mcp_startup_evidence, and satisfy gate.bootstrap_mcp_ready. Require stdio/tool fields only when the selected transport is MCP.");
            return;
        }

        if (usesMcp && (!string.Equals(transition.Command.Name, RequiredMcpTool, StringComparison.Ordinal)
            || !string.Equals(GetString(parameters, "transport"), "stdio", StringComparison.Ordinal)
            || !string.Equals(GetString(parameters, "requiredTool"), RequiredMcpTool, StringComparison.Ordinal)
            || !string.Equals(GetString(parameters, "runtimeCommand"), "descriptor_owned_mcp_stdio", StringComparison.Ordinal)
            || !string.Equals(GetString(parameters, "serverNameTemplate"), "loom-so-{resolved_runtime_version}", StringComparison.Ordinal)
            || !string.Equals(GetString(parameters, "workflowFileInput"), "current_external_workflow_copy", StringComparison.Ordinal)))
        {
            result.Add(
                BusinessGateRule,
                "An MCP governance-entry attempt must use the descriptor-owned local stdio tool against the current workflow copy.",
                $"transition:{transition.Id}",
                "For MCP, use so_inspect_workflow_fragment over stdio with the descriptor-owned server, exact workflow copy, and same operation identity.");
        }

        if (usesHostContinuation && (!string.Equals(transition.Command.Name, "workflow.inspectGovernanceEntry", StringComparison.Ordinal)
            || !string.Equals(GetString(parameters, "runtimeCommand"), TransportNeutralRuntimeCommand, StringComparison.Ordinal)
            || HasBooleanParameter(parameters, "mcpRequired")
            || !string.Equals(GetString(parameters, "workflowFileInput"), "current_external_workflow_copy", StringComparison.Ordinal)))
        {
            result.Add(
                BusinessGateRule,
                "A CLI-capable governance-entry path must declare MCP optional and use the same descriptor for bounded inspection.",
                $"transition:{transition.Id}",
                "Declare mcpRequired=false, runtimeCommand=descriptor_owned_mcp_or_cli, mcpFirst=true as optional priority, and the same bounded operation/workflow identity evidence.");
        }
    }

    private static bool HasCommonEntryShape(
        WorkflowValidationContract? validation,
        CommandTransition transition,
        IReadOnlyDictionary<string, object?> parameters)
        => string.Equals(transition.OutputPath, EvidenceFamily, StringComparison.Ordinal)
            && string.Equals(GetString(parameters, "projectionMode"), "canonical", StringComparison.Ordinal)
            && string.Equals(GetString(parameters, "resumeOutputKey"), EvidenceFamily, StringComparison.Ordinal)
            && GetStringList(parameters, "requiredInputs").Contains(EvidenceFamily, StringComparer.Ordinal)
            && string.Equals(GetString(parameters, "workflowFileInput"), "current_external_workflow_copy", StringComparison.Ordinal)
            && GetOutputBinding(parameters, EvidenceFamily) == "$result"
            && HasGovernanceEvidencePredicate(transition.SucceedExpression.Source)
            && HasGovernanceEvidenceGate(validation, transition);

    private static bool HasPayloadInputMatch(
        IReadOnlyDictionary<string, object?> parameters,
        string sourcePath,
        string expectedPath)
    {
        if (!parameters.TryGetValue("mustMatchPayloadInputs", out var value)
            || value is not IEnumerable<KeyValuePair<string, object?>> matches)
        {
            return false;
        }

        return matches.Any(pair =>
            string.Equals(pair.Key, sourcePath, StringComparison.Ordinal)
            && string.Equals(Convert.ToString(pair.Value), expectedPath, StringComparison.Ordinal));
    }

    private static string? GetOutputBinding(IReadOnlyDictionary<string, object?> parameters, string key)
    {
        if (parameters.TryGetValue("outputBindings", out var value) == false || value is null)
        {
            return null;
        }

        if (value is IDictionary<string, object?> mutable && mutable.TryGetValue(key, out var mutableValue))
        {
            return Convert.ToString(mutableValue);
        }

        if (value is IReadOnlyDictionary<string, object?> readOnly && readOnly.TryGetValue(key, out var readOnlyValue))
        {
            return Convert.ToString(readOnlyValue);
        }

        return null;
    }


    private static bool HasTransportGuard(string source, string transport)
        => source.Contains(TransportSelector, StringComparison.Ordinal)
            && source.Contains(transport, StringComparison.Ordinal);

    private static bool HasMcpAttemptGuard(string source, string status)
        => source.Contains(McpAttemptEvidenceFamily, StringComparison.Ordinal)
            && source.Contains("status", StringComparison.Ordinal)
            && source.Contains(status, StringComparison.Ordinal)
            && source.Contains("mcp_attempted", StringComparison.Ordinal)
            && source.Contains("== true", StringComparison.Ordinal);

    private static bool HasGovernanceEvidencePredicate(string source)
        => source.Contains(EvidenceFamily, StringComparison.Ordinal)
            && source.Contains("transport", StringComparison.Ordinal)
            && source.Contains("workflow_file", StringComparison.Ordinal)
            && source.Contains("fragment_bounded", StringComparison.Ordinal);

    private static bool HasMcpRegistrationAttemptPredicate(string source)
        => source.Contains("transport", StringComparison.Ordinal)
            && source.Contains("mcp_stdio", StringComparison.Ordinal)
            && source.Contains("initialized", StringComparison.Ordinal)
            && source.Contains("tool_called", StringComparison.Ordinal)
            && source.Contains("so_inspect_workflow_fragment", StringComparison.Ordinal);

    private static bool HasGovernanceEvidenceGate(WorkflowValidationContract? validation, TransitionBase transition)
    {
        if (validation is null || transition.SatisfiesGateIds is null)
        {
            return false;
        }

        return transition.SatisfiesGateIds.Any(gateId =>
            validation.Gates.TryGetValue(gateId, out var gate)
            && gate.RequiredOutputFamilies
                .Concat(gate.RequiredMachineReadableOutputFamilies)
                .Concat(gate.RequiredHumanReviewableOutputFamilies)
                .Contains(EvidenceFamily, StringComparer.Ordinal));
    }

    private static Dictionary<string, int> BuildStateDepths(
        string startNodeId,
        IReadOnlyDictionary<string, StateNode> states,
        IReadOnlyDictionary<string, TransitionBase> transitions)
    {
        var depths = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!states.ContainsKey(startNodeId)) return depths;
        var pending = new Queue<string>();
        depths[startNodeId] = 0;
        pending.Enqueue(startNodeId);
        while (pending.Count > 0)
        {
            var stateId = pending.Dequeue();
            foreach (var transitionId in states[stateId].Groups.SelectMany(static group => group.TransitionIds))
            {
                if (!transitions.TryGetValue(transitionId, out var transition)
                    || string.IsNullOrWhiteSpace(transition.TargetNodeId)
                    || !states.ContainsKey(transition.TargetNodeId)) continue;
                if (depths.TryAdd(transition.TargetNodeId, depths[stateId] + 1)) pending.Enqueue(transition.TargetNodeId);
            }
        }
        return depths;
    }

    private static Dictionary<string, int> BuildTransitionDepths(
        IReadOnlyDictionary<string, StateNode> states,
        IReadOnlyDictionary<string, TransitionBase> transitions,
        IReadOnlyDictionary<string, int> stateDepths)
    {
        var depths = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var state in states.Values)
        {
            if (!stateDepths.TryGetValue(state.Id, out var stateDepth)) continue;
            foreach (var transitionId in state.Groups.SelectMany(static group => group.TransitionIds))
            {
                if (transitions.ContainsKey(transitionId)
                    && (!depths.TryGetValue(transitionId, out var existing) || stateDepth < existing)) depths[transitionId] = stateDepth;
            }
        }
        return depths;
    }

    private static bool CanReachTransitionWithoutGovernanceEntry(
        string startNodeId,
        IReadOnlyDictionary<string, StateNode> states,
        IReadOnlyDictionary<string, TransitionBase> transitions,
        IReadOnlySet<string> excludedTransitionIds,
        string targetTransitionId)
    {
        if (!states.ContainsKey(startNodeId)) return false;
        var visited = new HashSet<string>(StringComparer.Ordinal) { startNodeId };
        var pending = new Queue<string>();
        pending.Enqueue(startNodeId);
        while (pending.Count > 0)
        {
            var state = states[pending.Dequeue()];
            foreach (var transitionId in state.Groups.SelectMany(static group => group.TransitionIds))
            {
                if (excludedTransitionIds.Contains(transitionId) || !transitions.TryGetValue(transitionId, out var transition)) continue;
                if (string.Equals(transitionId, targetTransitionId, StringComparison.Ordinal)) return true;
                if (!string.IsNullOrWhiteSpace(transition.TargetNodeId)
                    && states.ContainsKey(transition.TargetNodeId)
                    && visited.Add(transition.TargetNodeId)) pending.Enqueue(transition.TargetNodeId);
            }
        }
        return false;
    }

    private static bool HasOptionalMcpPlan(IReadOnlyDictionary<string, object?>? parameters)
        => !HasBooleanParameter(parameters, "mcpRegistrationRequired")
            && string.Equals(GetString(parameters, "runtimeLaunchDescriptorOutput"), RuntimeLaunchDescriptorField, StringComparison.Ordinal)
            && string.Equals(GetString(parameters, "runtimeLaunchSelection"), "runtime_owned", StringComparison.Ordinal)
            && string.Equals(GetString(parameters, "mcpRegistrationAttemptOutput"), McpAttemptEvidenceFamily, StringComparison.Ordinal);

    private static bool IsPreflightException(TransitionBase transition)
        => transition is CommandTransition commandTransition
            && transition.StepKind == WorkflowStepKind.WaitResume
            && HasBooleanParameter(commandTransition.Command.Parameters, "mcpPreflightExempt")
            && HasBooleanParameter(commandTransition.Command.Parameters, "runtimePreflight")
            && GetStringList(transition.PublishesOutputFamilies).Contains("runtime_preflight_result", StringComparer.Ordinal)
            && GetStringList(transition.PublishesOutputFamilies).Contains(McpAttemptEvidenceFamily, StringComparer.Ordinal)
            && GetStringList(transition.PublishesOutputFamilies).Contains(TransportSelector, StringComparer.Ordinal)
            && GetStringList(transition.PublishesOutputFamilies).Contains(RuntimeLaunchDescriptorField, StringComparer.Ordinal)
            && HasOptionalMcpPlan(commandTransition.Command.Parameters);

    private static bool IsExternalStep(WorkflowStepKind stepKind)
        => stepKind is WorkflowStepKind.ModelThink
            or WorkflowStepKind.Plan
            or WorkflowStepKind.McpCall
            or WorkflowStepKind.SubagentCall
            or WorkflowStepKind.AskUser
            or WorkflowStepKind.WaitResume;

    private static bool HasBooleanParameter(IReadOnlyDictionary<string, object?>? parameters, string name)
    {
        if (parameters?.TryGetValue(name, out var value) != true || value is null) return false;
        if (value is JsonElement element) return element.ValueKind == JsonValueKind.True;
        return value is bool boolean ? boolean : bool.TryParse(Convert.ToString(value), out var parsed) && parsed;
    }

    private static string? GetString(IReadOnlyDictionary<string, object?>? parameters, string name)
    {
        if (parameters?.TryGetValue(name, out var value) != true || value is null) return null;
        return value is JsonElement element && element.ValueKind == JsonValueKind.String ? element.GetString() : Convert.ToString(value);
    }

    private static IReadOnlyList<string> GetStringList(IReadOnlyDictionary<string, object?>? parameters, string name)
    {
        if (parameters?.TryGetValue(name, out var value) != true || value is null) return [];
        if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray().Select(static item => item.GetString()).Where(static item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToArray();
        if (value is IEnumerable<object?> objects)
            return objects.Select(Convert.ToString).Where(static item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToArray();
        if (value is IEnumerable<string> strings) return strings.ToArray();
        return [];
    }

    private static IReadOnlyList<string> GetStringList(IEnumerable<string>? values) => values?.ToArray() ?? [];
}
