using System.Text.Json.Serialization;

namespace Techne.Loom.AgentOrchestrator.Models;

public sealed record AoWorkflowSnapshot(
    [property: JsonPropertyName("objective")] string Objective,
    [property: JsonPropertyName("context")] Dictionary<string, object?> Context,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("current_node_id")] string CurrentNodeId,
    [property: JsonPropertyName("last_transition_id")] string? LastTransitionId,
    [property: JsonPropertyName("last_boundary_reason")] string? LastBoundaryReason,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("pending_requirements")] IReadOnlyList<string>? PendingRequirements = null,
    [property: JsonPropertyName("next_frontier")] IReadOnlyList<string>? NextFrontier = null,
    [property: JsonPropertyName("human_or_agent_hint")] string? HumanOrAgentHint = null,
    [property: JsonPropertyName("weave_out_request")] AoWeaveOutRequest? WeaveOutRequest = null,
    [property: JsonPropertyName("audit_step_sequence")] int AuditStepSequence = 0);
