using System.Text.Json.Serialization;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Models;

public sealed record AoErrorPayload(
    [property: JsonPropertyName("session_id")] string? SessionId,
    [property: JsonPropertyName("workflow_file")] string WorkflowFile,
    [property: JsonPropertyName("workflow_instance_file")] string? WorkflowInstanceFile,
    [property: JsonPropertyName("event_log_file")] string EventLogFile,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("result_file")] string ResultFile,
    [property: JsonPropertyName("must_show_to_user_files")] IReadOnlyList<string>? MustShowToUserFiles = null,
    [property: JsonPropertyName("workflow_location_summary")] string? WorkflowLocationSummary = null,
    [property: JsonPropertyName("audit_artifacts")] WorkflowAuditArtifacts? AuditArtifacts = null);
