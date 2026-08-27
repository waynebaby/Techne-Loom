using System.Text.Json.Serialization;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record WorkflowAuditArtifacts(
    [property: JsonPropertyName("output_root")] string OutputRoot,
    [property: JsonPropertyName("workflow_id")] string WorkflowId,
    [property: JsonPropertyName("sequence")] int Sequence,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("step_directory")] string StepDirectory,
    [property: JsonPropertyName("mermaid_file")] string MermaidFile,
    [property: JsonPropertyName("html_file")] string HtmlFile,
    [property: JsonPropertyName("workflow_backup_file")] string WorkflowBackupFile,
    [property: JsonPropertyName("summary_file")] string? SummaryFile = null,
    [property: JsonPropertyName("analysis_file")] string? AnalysisFile = null,
    [property: JsonPropertyName("dataflow_file")] string? DataflowFile = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("reuse_manifest_file")]
    public string? ReuseManifestFile { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("reused_from_step_directory")]
    public string? ReusedFromStepDirectory { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("reuse_reason")]
    public string? ReuseReason { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("reuse_verified_by")]
    public string? ReuseVerifiedBy { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("artifact_origin")]
    public string? ArtifactOrigin { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("official_execution_evidence")]
    public bool? OfficialExecutionEvidence { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("mermaid_delivery")]
    public MermaidDelivery? MermaidDelivery { get; init; }
}
