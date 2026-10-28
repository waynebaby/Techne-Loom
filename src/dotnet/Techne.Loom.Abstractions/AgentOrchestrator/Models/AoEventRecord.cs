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
    [property: JsonPropertyName("correlation_key")] string? CorrelationKey = null,
    [property: JsonPropertyName("step_sequence")] int? StepSequence = null,
    [property: JsonPropertyName("step_action")] string? StepAction = null,
    [property: JsonPropertyName("step_directory")] string? StepDirectory = null,
    [property: JsonPropertyName("summary_file")] string? SummaryFile = null,
    [property: JsonPropertyName("pending_requirements")] IReadOnlyList<string>? PendingRequirements = null,
    [property: JsonPropertyName("next_frontier")] IReadOnlyList<string>? NextFrontier = null,
    [property: JsonPropertyName("workflow_instance_file")] string? WorkflowInstanceFile = null,
    [property: JsonPropertyName("replan_history")] IReadOnlyList<Dictionary<string, object?>>? ReplanHistory = null);
