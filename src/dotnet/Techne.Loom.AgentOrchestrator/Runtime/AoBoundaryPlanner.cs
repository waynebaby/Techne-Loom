using Techne.Loom.AgentOrchestrator.Models;

namespace Techne.Loom.AgentOrchestrator.Runtime;

internal static class AoBoundaryPlanner
{
    public static AoBoundaryPlan CreatePlan(Dictionary<string, object?> context)
    {
        context.TryGetValue("force_boundary_reason", out var forcedBoundary);
        var reason = AoBoundaryReason.Normalize(forcedBoundary?.ToString());
        if (!string.IsNullOrWhiteSpace(forcedBoundary?.ToString()))
        {
            return CreatePlanForReason(reason);
        }

        if (IsConfirmedScopeSatisfied(context))
        {
            return CreatePlanForConfirmedScope(context);
        }

        return CreatePlanForReason(AoBoundaryReason.ClarificationRequired);
    }

    private static AoBoundaryPlan CreatePlanForConfirmedScope(IReadOnlyDictionary<string, object?> context)
    {
        return TryGetSelectedFrontierAction(context) switch
        {
            "confirm_target_scope" => CreatePlanForReason(AoBoundaryReason.ToolProbeRequired),
            "continue_with_confirmed_plan" => CreatePlanForReason(AoBoundaryReason.ToolProbeRequired),
            _ => CreatePlanForReason(AoBoundaryReason.ToolProbeRequired),
        };
    }

    private static AoBoundaryPlan CreatePlanForReason(string reason)
    {
        return reason switch
        {
            AoBoundaryReason.WeaveOutRequired => new AoBoundaryPlan(
                reason,
                CurrentNodeId: "boundary.weave_out",
                TransitionId: "transition.weave_out",
                PendingRequirements: ["weave_back_result"],
                NextFrontier: ["compare_frontier_a", "compare_frontier_b"],
                Hint: "Weave-out request emitted. Resume with a structured external comparison result.",
                WeaveOutRequest: new AoWeaveOutRequest(
                    "compare candidate execution frontiers",
                    ["frontier-a.json", "frontier-b.json"])),
            AoBoundaryReason.DelegationRequired => new AoBoundaryPlan(
                reason,
                CurrentNodeId: "boundary.delegation",
                TransitionId: "transition.delegation",
                PendingRequirements: ["delegation_result"],
                NextFrontier: ["dispatch_specialist_agent", "collect_review_notes"],
                Hint: "Delegation boundary reached. Resume with delegated findings."),
            AoBoundaryReason.ToolProbeRequired => new AoBoundaryPlan(
                reason,
                CurrentNodeId: "boundary.tool_probe",
                TransitionId: "transition.tool_probe",
                PendingRequirements: ["probe_report"],
                NextFrontier: ["probe_repo_structure", "probe_recent_logs"],
                Hint: "Tool probing required before AO can continue."),
            _ => new AoBoundaryPlan(
                AoBoundaryReason.ClarificationRequired,
                CurrentNodeId: "boundary.clarification",
                TransitionId: "transition.clarify",
                PendingRequirements: ["confirmed_scope"],
                NextFrontier: ["confirm_target_scope", "continue_with_confirmed_plan"],
                Hint: "Clarification required. Resume with confirmed_scope in payload."),
        };
    }

    private static bool IsConfirmedScopeSatisfied(IReadOnlyDictionary<string, object?> context)
    {
        if (!context.TryGetValue("confirmed_scope", out var confirmedScope) || confirmedScope is null)
        {
            return false;
        }

        return confirmedScope switch
        {
            bool boolValue => boolValue,
            string text when bool.TryParse(text, out var parsed) => parsed,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.True } => true,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.False } => false,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } jsonElement
                when bool.TryParse(jsonElement.GetString(), out var parsed) => parsed,
            _ => true,
        };
    }

    private static string? TryGetSelectedFrontierAction(IReadOnlyDictionary<string, object?> context)
    {
        if (!context.TryGetValue("plan_meta", out var planMeta) || planMeta is null)
        {
            return null;
        }

        return planMeta switch
        {
            IReadOnlyDictionary<string, object?> dictionary when dictionary.TryGetValue("selected_frontier_action", out var value) => value?.ToString(),
            IDictionary<string, object?> dictionary when dictionary.TryGetValue("selected_frontier_action", out var value) => value?.ToString(),
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Object } jsonElement
                when jsonElement.TryGetProperty("selected_frontier_action", out var property) => property.GetString(),
            _ => null,
        };
    }
}
