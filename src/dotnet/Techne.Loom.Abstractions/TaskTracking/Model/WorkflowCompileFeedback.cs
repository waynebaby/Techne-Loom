using System.Text.Json.Serialization;

namespace Techne.Loom.Abstractions.TaskTracking.Model;

public static class WorkflowCompileFeedbackContract
{
    public const string SchemaVersion = "workflow.compile-feedback.v1";
}

public sealed class WorkflowCompileFeedback
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = WorkflowCompileFeedbackContract.SchemaVersion;

    [JsonPropertyName("product")]
    public string Product { get; set; } = string.Empty;

    [JsonPropertyName("runtime")]
    public string Runtime { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "succeeded";

    [JsonPropertyName("workflow_path")]
    public string? WorkflowPath { get; set; }

    [JsonPropertyName("workflow_hash")]
    public string? WorkflowHash { get; set; }

    [JsonPropertyName("candidate_path")]
    public string? CandidatePath { get; set; }

    [JsonPropertyName("candidate_hash")]
    public string? CandidateHash { get; set; }

    [JsonPropertyName("runtime_identity")]
    public string? RuntimeIdentity { get; set; }

    [JsonPropertyName("runtime_version")]
    public string? RuntimeVersion { get; set; }

    [JsonPropertyName("counts")]
    public WorkflowCompileFeedbackCounts Counts { get; set; } = new();

    [JsonPropertyName("diagnostics")]
    public List<WorkflowCompileDiagnostic> Diagnostics { get; set; } = [];

    [JsonPropertyName("phases")]
    public List<WorkflowCompilePhaseFeedback> Phases { get; set; } = [];

    [JsonPropertyName("truncated")]
    public bool Truncated { get; set; }
}

public sealed class WorkflowCompileFeedbackCounts
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("errors")]
    public int Errors { get; set; }

    [JsonPropertyName("warnings")]
    public int Warnings { get; set; }

    [JsonPropertyName("info")]
    public int Info { get; set; }

    [JsonPropertyName("blocked")]
    public int Blocked { get; set; }
}

public sealed class WorkflowCompileDiagnostic
{
    [JsonPropertyName("rule_id")]
    public string RuleId { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "error";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("suggested_fix")]
    public string? SuggestedFix { get; set; }

    [JsonPropertyName("phase")]
    public string Phase { get; set; } = "structure";

    [JsonPropertyName("blocked_by")]
    public List<string> BlockedBy { get; set; } = [];

    [JsonPropertyName("expression_feedback")]
    public ExpressionCompileFeedback? ExpressionFeedback { get; set; }
}

public sealed class WorkflowCompilePhaseFeedback
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "completed";

    [JsonPropertyName("prerequisites")]
    public List<string> Prerequisites { get; set; } = [];

    [JsonPropertyName("diagnostic_count")]
    public int DiagnosticCount { get; set; }

    [JsonPropertyName("blocked_by")]
    public List<string> BlockedBy { get; set; } = [];
}