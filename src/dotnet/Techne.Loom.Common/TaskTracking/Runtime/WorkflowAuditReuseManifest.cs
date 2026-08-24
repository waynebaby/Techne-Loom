using System.Text.Json.Serialization;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record WorkflowAuditReuseManifest(
    [property: JsonPropertyName("source_step_directory")] string SourceStepDirectory,
    [property: JsonPropertyName("destination_step_directory")] string DestinationStepDirectory,
    [property: JsonPropertyName("source_workflow_id")] string SourceWorkflowId,
    [property: JsonPropertyName("source_instance_id")] string? SourceInstanceId,
    [property: JsonPropertyName("workflow_id")] string WorkflowId,
    [property: JsonPropertyName("sequence")] int Sequence,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("verified_by")] string VerifiedBy,
    [property: JsonPropertyName("copied_at_utc")] DateTimeOffset CopiedAtUtc,
    [property: JsonPropertyName("artifact_origin")] string ArtifactOrigin,
    [property: JsonPropertyName("official_execution_evidence")] bool OfficialExecutionEvidence,
    [property: JsonPropertyName("source_file_sha256")] IReadOnlyDictionary<string, string> SourceFileSha256);