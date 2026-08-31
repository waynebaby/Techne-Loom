using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Validation;

internal static class McpFirstGovernedRouteValidator
{
    private const string GovernedTemplateKind = "so-governed-target-skill";
    private const string McpEvidenceFamily = "mcp_startup_evidence";
    private const string RequiredMcpTool = "so_inspect_workflow_fragment";
    private const string BusinessGateRule = "SO3000";

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

        var mcpFirstTransitions = transitions.Values
            .OfType<CommandTransition>()
            .Where(transition => transition.StepKind == WorkflowStepKind.McpCall && HasBooleanParameter(transition.Command.Parameters, "mcpFirst"))
            .ToArray();
        if (mcpFirstTransitions.Length != 1)
        {
            result.Add(
                BusinessGateRule,
                "Loom-governanced target-skill workflows must declare exactly one MCP-first McpCall transition.",
                "nodes/*/mcpFirst",
                "Declare one command transition with stepKind McpCall and command.parameters.mcpFirst=true.");
            return;
        }

        var mcpFirst = mcpFirstTransitions[0];
        var parameters = mcpFirst.Command.Parameters ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!string.Equals(mcpFirst.Command.Name, RequiredMcpTool, StringComparison.Ordinal)
            || !string.Equals(mcpFirst.OutputPath, McpEvidenceFamily, StringComparison.Ordinal)
            || !string.Equals(GetString(parameters, "transport"), "stdio", StringComparison.Ordinal)
            || !string.Equals(GetString(parameters, "requiredTool"), RequiredMcpTool, StringComparison.Ordinal)
            || !string.Equals(GetString(parameters, "projectionMode"), "canonical", StringComparison.Ordinal)
            || !string.Equals(GetString(parameters, "resumeOutputKey"), McpEvidenceFamily, StringComparison.Ordinal)
            || !GetStringList(parameters, "requiredInputs").Contains(McpEvidenceFamily, StringComparer.Ordinal)
            || !string.Equals(GetString(parameters, "workflowFileInput"), "current_external_workflow_copy", StringComparison.Ordinal)
            || !HasResultBinding(parameters, McpEvidenceFamily)
            || !HasMcpEvidencePredicate(mcpFirst.SucceedExpression.Source)
            || !HasMcpEvidenceGate(instance.Validation, mcpFirst))
        {
            result.Add(
                BusinessGateRule,
                "The MCP-first transition must use local stdio, call so_inspect_workflow_fragment, and project mcp_startup_evidence canonically.",
                $"transition:{mcpFirst.Id}",
                "Set mcpFirst=true, transport=stdio, requiredTool=so_inspect_workflow_fragment, projectionMode=canonical, resumeOutputKey/outputPath=mcp_startup_evidence, and satisfy gate.bootstrap_mcp_ready.");
        }

        var preflightExceptions = transitions.Values.Where(IsPreflightException).ToArray();
        if (preflightExceptions.Length > 1)
        {
            result.Add(
                BusinessGateRule,
                "Only one external transition may be marked as the exact runtime preflight exception before MCP-first.",
                "nodes/*/mcpPreflightExempt",
                "Remove mcpPreflightExempt/runtimePreflight from every transition except the single runtime-preflight WaitResume that publishes runtime_preflight_result.");
        }

        var stateDepths = BuildStateDepths(instance.StartNodeId, states, transitions);
        var transitionDepths = BuildTransitionDepths(states, transitions, stateDepths);
        if (!transitionDepths.TryGetValue(mcpFirst.Id, out var mcpDepth))
        {
            result.Add(
                BusinessGateRule,
                "The MCP-first transition is not reachable from the workflow start state.",
                $"transition:{mcpFirst.Id}",
                "Connect the MCP-first state to the start path after the runtime-preflight exception.");
            return;
        }

        foreach (var transition in transitions.Values)
        {
            if (transition.Id == mcpFirst.Id
                || !IsExternalStep(transition.StepKind)
                || IsPreflightException(transition)
                || !transitionDepths.TryGetValue(transition.Id, out var transitionDepth)
                || (transitionDepth >= mcpDepth && !CanReachTransitionWithoutMcp(instance.StartNodeId, states, transitions, mcpFirst.Id, transition.Id)))
            {
                continue;
            }

            result.Add(
                BusinessGateRule,
                $"External transition '{transition.Id}' occurs before the MCP-first transition.",
                $"transition:{transition.Id}",
                "Move this external step after the MCP-first fragment check or explicitly mark only the exact runtime-preflight step with mcpPreflightExempt=true.");
        }
    }

    private static Dictionary<string, int> BuildStateDepths(
        string startNodeId,
        IReadOnlyDictionary<string, StateNode> states,
        IReadOnlyDictionary<string, TransitionBase> transitions)
    {
        var depths = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!states.ContainsKey(startNodeId))
        {
            return depths;
        }

        var pending = new Queue<string>();
        depths[startNodeId] = 0;
        pending.Enqueue(startNodeId);
        while (pending.Count > 0)
        {
            var stateId = pending.Dequeue();
            var state = states[stateId];
            foreach (var transitionId in state.Groups.SelectMany(static group => group.TransitionIds))
            {
                if (!transitions.TryGetValue(transitionId, out var transition)
                    || string.IsNullOrWhiteSpace(transition.TargetNodeId)
                    || !states.ContainsKey(transition.TargetNodeId))
                {
                    continue;
                }

                var targetDepth = depths[stateId] + 1;
                if (!depths.ContainsKey(transition.TargetNodeId))
                {
                    depths[transition.TargetNodeId] = targetDepth;
                    pending.Enqueue(transition.TargetNodeId);
                }
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
            if (!stateDepths.TryGetValue(state.Id, out var stateDepth))
            {
                continue;
            }

            foreach (var transitionId in state.Groups.SelectMany(static group => group.TransitionIds))
            {
                if (transitions.ContainsKey(transitionId)
                    && (!depths.TryGetValue(transitionId, out var existingDepth) || stateDepth < existingDepth))
                {
                    depths[transitionId] = stateDepth;
                }
            }
        }

        return depths;
    }

    private static bool HasMcpEvidencePredicate(string source)
        => source.Contains("mcp_startup_evidence", StringComparison.Ordinal)
            && source.Contains("transport", StringComparison.Ordinal)
            && source.Contains("initialized", StringComparison.Ordinal)
            && source.Contains("tool_called", StringComparison.Ordinal)
            && source.Contains("tool_name", StringComparison.Ordinal)
            && source.Contains("workflow_file", StringComparison.Ordinal)
            && source.Contains("fragment_bounded", StringComparison.Ordinal);

    private static bool HasResultBinding(IReadOnlyDictionary<string, object?> parameters, string outputFamily)
    {
        if (!parameters.TryGetValue("outputBindings", out var value)
            || value is not IReadOnlyDictionary<string, object?> bindings
            || !bindings.TryGetValue(outputFamily, out var binding))
        {
            return false;
        }

        return string.Equals(Convert.ToString(binding), "$result", StringComparison.Ordinal);
    }

    private static bool HasMcpEvidenceGate(WorkflowValidationContract? validation, TransitionBase transition)
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
                .Contains(McpEvidenceFamily, StringComparer.Ordinal));
    }

    private static bool CanReachTransitionWithoutMcp(
        string startNodeId,
        IReadOnlyDictionary<string, StateNode> states,
        IReadOnlyDictionary<string, TransitionBase> transitions,
        string excludedTransitionId,
        string targetTransitionId)
    {
        if (!states.ContainsKey(startNodeId))
        {
            return false;
        }

        var visitedStates = new HashSet<string>(StringComparer.Ordinal) { startNodeId };
        var pendingStates = new Queue<string>();
        pendingStates.Enqueue(startNodeId);
        while (pendingStates.Count > 0)
        {
            var state = states[pendingStates.Dequeue()];
            foreach (var transitionId in state.Groups.SelectMany(static group => group.TransitionIds))
            {
                if (string.Equals(transitionId, excludedTransitionId, StringComparison.Ordinal)
                    || !transitions.TryGetValue(transitionId, out var transition))
                {
                    continue;
                }

                if (string.Equals(transitionId, targetTransitionId, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(transition.TargetNodeId)
                    && states.ContainsKey(transition.TargetNodeId)
                    && visitedStates.Add(transition.TargetNodeId))
                {
                    pendingStates.Enqueue(transition.TargetNodeId);
                }
            }
        }

        return false;
    }

    private static bool IsPreflightException(TransitionBase transition)
        => transition is CommandTransition commandTransition
            && transition.StepKind == WorkflowStepKind.WaitResume
            && HasBooleanParameter(commandTransition.Command.Parameters, "mcpPreflightExempt")
            && HasBooleanParameter(commandTransition.Command.Parameters, "runtimePreflight")
            && GetStringList(transition.PublishesOutputFamilies).Contains("runtime_preflight_result", StringComparer.Ordinal);

    private static bool IsExternalStep(WorkflowStepKind stepKind)
        => stepKind is WorkflowStepKind.ModelThink
            or WorkflowStepKind.Plan
            or WorkflowStepKind.McpCall
            or WorkflowStepKind.SubagentCall
            or WorkflowStepKind.AskUser
            or WorkflowStepKind.WaitResume;

    private static bool HasBooleanParameter(IReadOnlyDictionary<string, object?>? parameters, string name)
    {
        if (parameters?.TryGetValue(name, out var value) != true)
        {
            return false;
        }

        return value switch
        {
            bool boolean => boolean,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            string text when bool.TryParse(text, out var boolean) => boolean,
            _ => false,
        };
    }

    private static string? GetString(IReadOnlyDictionary<string, object?> parameters, string name)
        => parameters.TryGetValue(name, out var value) ? Convert.ToString(value) : null;

    private static IReadOnlyList<string> GetStringList(IReadOnlyDictionary<string, object?> parameters, string name)
        => parameters.TryGetValue(name, out var value) ? GetStringList(value) : [];

    private static IReadOnlyList<string> GetStringList(IEnumerable<string>? values)
        => values?.Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray() ?? [];

    private static IReadOnlyList<string> GetStringList(object? value)
    {
        if (value is IEnumerable<string> strings)
        {
            return GetStringList(strings);
        }

        if (value is IEnumerable<object?> objects)
        {
            return objects.Select(Convert.ToString).Where(static value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToArray();
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } array)
        {
            return array.EnumerateArray()
                .Where(static item => item.ValueKind == JsonValueKind.String)
                .Select(static item => item.GetString())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToArray();
        }

        return [];
    }
}
