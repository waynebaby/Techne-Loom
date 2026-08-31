using System.Text.Json.Serialization;

namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class ExpressionCompileFeedback
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("language_version")]
    public string LanguageVersion { get; set; } = string.Empty;

    [JsonPropertyName("contract_id")]
    public string ContractId { get; set; } = string.Empty;

    [JsonPropertyName("contract_version")]
    public string ContractVersion { get; set; } = string.Empty;

    [JsonPropertyName("workflow_id")]
    public string? WorkflowId { get; set; }

    [JsonPropertyName("gate_id")]
    public string? GateId { get; set; }

    [JsonPropertyName("transition_id")]
    public string? TransitionId { get; set; }

    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("source_span")]
    public ExpressionSourceSpan? SourceSpan { get; set; }

    [JsonPropertyName("diagnostic_code")]
    public string DiagnosticCode { get; set; } = string.Empty;

    [JsonPropertyName("diagnostic_category")]
    public string DiagnosticCategory { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("suggested_fix")]
    public string? SuggestedFix { get; set; }

    [JsonPropertyName("referenced_symbols")]
    public List<string> ReferencedSymbols { get; set; } = [];

    [JsonPropertyName("compiler_identity")]
    public string CompilerIdentity { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("entry_point")]
    public string? EntryPoint { get; set; }

    [JsonPropertyName("result_type")]
    public string? ResultType { get; set; }

    [JsonPropertyName("capabilities")]
    public List<string> Capabilities { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("diagnostics")]
    public List<ExpressionCompileDiagnostic> Diagnostics { get; set; } = [];

    [JsonPropertyName("truncated")]
    public bool Truncated { get; set; }

    [JsonPropertyName("diagnostic_count")]
    public int DiagnosticCount { get; set; }
}

public sealed class ExpressionCompileDiagnostic
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("source_span")]
    public ExpressionSourceSpan? SourceSpan { get; set; }

    [JsonPropertyName("suggested_fix")]
    public string? SuggestedFix { get; set; }
}

public sealed class ExpressionSourceSpan
{
    [JsonPropertyName("start_line")]
    public int StartLine { get; set; }

    [JsonPropertyName("start_column")]
    public int StartColumn { get; set; }

    [JsonPropertyName("end_line")]
    public int EndLine { get; set; }

    [JsonPropertyName("end_column")]
    public int EndColumn { get; set; }
}
