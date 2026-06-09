using System.Text.Json.Serialization;

namespace Techne.Loom.AgentOrchestrator.Models;

public sealed record AoWorkflowSnapshot(
    [property: JsonPropertyName("objective")] string Objective,
    [property: JsonPropertyName("context")] Dictionary<string, object?> Context,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("current_node_id")] string CurrentNodeId,
    [property: JsonPropertyName("last_transition_id")] string? LastTransitionId,
    [property: JsonPropertyName("last_boundary_reason")] string? LastBoundaryReason,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);
