using Techne.Loom.AgentOrchestrator.Models;

namespace Techne.Loom.AgentOrchestrator.Runtime;

internal static class AoBoundaryPlanner
{
    public static AoBoundaryPlan CreatePlan(Dictionary<string, object?> context)
    {
        context.TryGetValue("force_boundary_reason", out var forcedBoundary);
        var reason = AoBoundaryReason.Normalize(forcedBoundary?.ToString());
        return reason switch
        {
            AoBoundaryReason.SamplingRequired => new AoBoundaryPlan(
                reason,
                CurrentNodeId: "boundary.sampling",
                TransitionId: "transition.sampling",
                PendingRequirements: ["sampling_decision"],
                NextFrontier: ["evaluate_frontier_a", "evaluate_frontier_b"],
                Hint: "Sampling request emitted. Resolve with a structured comparison result.",
                SamplingRequest: new AoSamplingRequest(
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
}
