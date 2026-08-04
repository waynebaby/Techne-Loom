using System.Text.Json;
using Techne.Loom.AgentOrchestrator.Models;

namespace Techne.Loom.AgentOrchestrator.Runtime;

internal static class AoBoundaryPlanner
{
    public static AoBoundaryPlan CreatePlan(Dictionary<string, object?> context)
    {
        var forcedBoundary = context.TryGetValue("force_boundary_reason", out var forcedValue)
            ? Convert.ToString(forcedValue)
            : null;

        if (HasValidReplanContract(context))
        {
            return CreatePlanForReplanStrategy(context);
        }

        if (HasReplanHistory(context) && string.IsNullOrWhiteSpace(forcedBoundary))
        {
            return CreateReplanEntryPlan(context);
        }

        if (!string.IsNullOrWhiteSpace(forcedBoundary))
        {
            return CreatePlanForReason(AoBoundaryReason.Normalize(forcedBoundary), context);
        }

        if (IsConfirmedScopeSatisfied(context))
        {
            return CreatePlanForConfirmedScope(context);
        }

        return CreatePlanForReason(AoBoundaryReason.ClarificationRequired, context);
    }

    private static AoBoundaryPlan CreatePlanForConfirmedScope(IReadOnlyDictionary<string, object?> context)
    {
        return TryGetSelectedFrontierAction(context) switch
        {
            "confirm_target_scope" => CreatePlanForReason(AoBoundaryReason.ToolProbeRequired, context),
            "continue_with_confirmed_plan" => CreatePlanForReason(AoBoundaryReason.ToolProbeRequired, context),
            _ => CreatePlanForReason(AoBoundaryReason.ToolProbeRequired, context),
        };
    }

    private static AoBoundaryPlan CreatePlanForReason(string reason, IReadOnlyDictionary<string, object?> context)
    {
        return reason switch
        {
            AoBoundaryReason.WeaveOutRequired => CreateWeaveOutPlan(reason, context),
            AoBoundaryReason.ReplanRequired => CreateReplanEntryPlan(context),
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

    private static AoBoundaryPlan CreateWeaveOutPlan(string reason, IReadOnlyDictionary<string, object?> context)
    {
        var evidenceReferences = TryGetEvidenceReferences(context, "evidence_references");
        var hasEvidence = evidenceReferences.Count > 0;
        return new AoBoundaryPlan(
            reason,
            CurrentNodeId: "boundary.weave_out",
            TransitionId: "transition.weave_out",
            PendingRequirements: hasEvidence ? ["weave_back_result"] : ["weave_back_result", "evidence_references"],
            NextFrontier: ["compare_frontier_a", "compare_frontier_b"],
            Hint: hasEvidence
                ? "Weave-out request emitted with verified evidence references. Resume with a structured external comparison result."
                : "Weave-out is blocked until a verified evidence_references manifest is supplied.",
            WeaveOutRequest: hasEvidence
                ? new AoWeaveOutRequest(
                    "compare candidate execution frontiers",
                    ["frontier-a.json", "frontier-b.json"],
                    evidenceReferences)
                : null);
    }

    private static IReadOnlyList<AoEvidenceReference> TryGetEvidenceReferences(IReadOnlyDictionary<string, object?> context, string key)
    {
        if (!context.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        try
        {
            var serialized = JsonSerializer.Serialize(value);
            var references = JsonSerializer.Deserialize<List<AoEvidenceReference?>>(serialized) ?? [];
            var root = new[] { "evidence_root", "workspace_root", "runtime_output_root" }.Select(key => context.TryGetValue(key, out var value) ? Convert.ToString(value) : null).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            return AoEvidenceReferenceValidator.Validate(references, root);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static AoBoundaryPlan CreateReplanEntryPlan(IReadOnlyDictionary<string, object?> context)
    {
        return new AoBoundaryPlan(
            AoBoundaryReason.ReplanRequired,
            CurrentNodeId: "state.replan_strategy",
            TransitionId: "transition.plan_recovery_strategy",
            PendingRequirements: ["replan_strategy", "replan_anchor", "candidate_terminal_path"],
            NextFrontier: ["continue_from_current", "rollback_to_unconfirmed", "redesign_from_current", "full_redesign", "reversible_workaround"],
            Hint: "Retained blocker history is ready. AO planner must select one recovery strategy and return a viable terminal path.");
    }

    private static AoBoundaryPlan CreatePlanForReplanStrategy(IReadOnlyDictionary<string, object?> context)
    {
        var strategy = AoReplanHistory.GetString(context, "replan_strategy");
        var route = strategy switch
        {
            "continue_from_current" => ("state.replan_current", "transition.design_from_current", "Continue from the current blocked state."),
            "rollback_to_unconfirmed" => ("state.replan_unconfirmed", "transition.design_from_unconfirmed", "Design forward from the selected unconfirmed node."),
            "redesign_from_current" => ("state.replan_redesign", "transition.design_redesign", "Replace the failing continuation while preserving completed history."),
            "full_redesign" => ("state.replan_full", "transition.design_full", "Replace the route design while retaining blocker history."),
            "reversible_workaround" => ("state.replan_workaround", "transition.design_workaround", "Apply the smallest reversible workaround and retain its rollback plan."),
            _ => ("state.replan_route", "transition.replan_contract_invalid", "The replan contract is incomplete or uses an unsupported strategy."),
        };

        var pending = strategy == "reversible_workaround"
            ? new[] { "replan_evidence_references", "rollback_plan" }
            : new[] { "replan_evidence_references" };
        return new AoBoundaryPlan(
            AoBoundaryReason.ReplanRequired,
            route.Item1,
            route.Item2,
            pending,
            ["validate_replan_path", "resume_official_route"],
            route.Item3);
    }

    private static bool HasReplanHistory(IReadOnlyDictionary<string, object?> context)
        => AoReplanHistory.Read(context).Count > 0;

    private static bool HasValidReplanContract(IReadOnlyDictionary<string, object?> context)
    {
        var strategy = AoReplanHistory.GetString(context, "replan_strategy");
        if (strategy is not ("continue_from_current" or "rollback_to_unconfirmed" or "redesign_from_current" or "full_redesign" or "reversible_workaround"))
        {
            return false;
        }

        if (!HasReplanHistory(context)
            || !AoReplanHistory.HasMeaningfulValue(context, "replan_anchor")
            || !AoReplanHistory.HasMeaningfulValue(context, "candidate_terminal_path")
            || TryGetEvidenceReferences(context, "replan_evidence_references").Count == 0)
        {
            return false;
        }

        return strategy != "reversible_workaround" || AoReplanHistory.HasMeaningfulValue(context, "rollback_plan");
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
