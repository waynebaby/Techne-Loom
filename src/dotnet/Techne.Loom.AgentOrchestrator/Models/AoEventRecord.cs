using System.Text.Json.Serialization;

namespace Techne.Loom.AgentOrchestrator.Models;

public sealed record AoEventRecord(
    [property: JsonPropertyName("ts")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("event_type")] string EventType,
    [property: JsonPropertyName("session_id")] string SessionId,
    [property: JsonPropertyName("workflow_file")] string WorkflowFile,
    [property: JsonPropertyName("event_log_file")] string EventLogFile,
    [property: JsonPropertyName("from_status")] string? FromStatus = null,
    [property: JsonPropertyName("to_status")] string? ToStatus = null,
    [property: JsonPropertyName("boundary_reason")] string? BoundaryReason = null,
    [property: JsonPropertyName("transition_id")] string? TransitionId = null,
    [property: JsonPropertyName("correlation_key")] string? CorrelationKey = null);
