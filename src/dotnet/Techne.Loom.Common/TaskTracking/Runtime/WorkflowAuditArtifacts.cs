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
    [property: JsonPropertyName("summary_file")] string? SummaryFile = null);
