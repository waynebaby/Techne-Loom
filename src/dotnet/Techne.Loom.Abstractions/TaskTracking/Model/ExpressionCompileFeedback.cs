namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class ExpressionCompileFeedback
{
    public string Status { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string LanguageVersion { get; set; } = string.Empty;

    public string ContractId { get; set; } = string.Empty;

    public string ContractVersion { get; set; } = string.Empty;

    public string? WorkflowId { get; set; }

    public string? GateId { get; set; }

    public string? TransitionId { get; set; }

    public string Field { get; set; } = string.Empty;

    public ExpressionSourceSpan? SourceSpan { get; set; }

    public string DiagnosticCode { get; set; } = string.Empty;

    public string DiagnosticCategory { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? SuggestedFix { get; set; }

    public List<string> ReferencedSymbols { get; set; } = [];

    public string CompilerIdentity { get; set; } = string.Empty;

    public string? Kind { get; set; }

    public string? EntryPoint { get; set; }

    public string? ResultType { get; set; }

    public List<string> Capabilities { get; set; } = [];

    public List<string> Warnings { get; set; } = [];
}

public sealed class ExpressionSourceSpan
{
    public int StartLine { get; set; }

    public int StartColumn { get; set; }

    public int EndLine { get; set; }

    public int EndColumn { get; set; }
}
