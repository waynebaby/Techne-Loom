using System.Text.Json.Serialization;

namespace Techne.Loom.AgentOrchestrator.Models;

public sealed record AoErrorPayload(
    [property: JsonPropertyName("session_id")] string? SessionId,
    [property: JsonPropertyName("workflow_file")] string WorkflowFile,
    [property: JsonPropertyName("event_log_file")] string EventLogFile,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("result_file")] string ResultFile);
