using System.Text.Json.Serialization;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Models;

public sealed record AoControlPayload(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("session_id")] string SessionId,
    [property: JsonPropertyName("workflow_file")] string WorkflowFile,
    [property: JsonPropertyName("event_log_file")] string EventLogFile,
    [property: JsonPropertyName("current_node_id")] string CurrentNodeId,
    [property: JsonPropertyName("boundary_reason")] string? BoundaryReason = null,
    [property: JsonPropertyName("result_file")] string? ResultFile = null,
    [property: JsonPropertyName("pending_requirements")] IReadOnlyList<string>? PendingRequirements = null,
    [property: JsonPropertyName("next_frontier")] IReadOnlyList<string>? NextFrontier = null,
    [property: JsonPropertyName("human_or_agent_hint")] string? HumanOrAgentHint = null,
    [property: JsonPropertyName("weave_out_request")] AoWeaveOutRequest? WeaveOutRequest = null,
    [property: JsonPropertyName("audit_artifacts")] WorkflowAuditArtifacts? AuditArtifacts = null);
