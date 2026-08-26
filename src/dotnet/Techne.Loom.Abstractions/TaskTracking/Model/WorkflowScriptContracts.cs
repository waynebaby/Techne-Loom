namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class WorkflowScriptInput
{
    public string RuntimeBinding { get; set; } = string.Empty;

    public string? RuntimeVersion { get; set; }

    public IReadOnlyDictionary<string, object?> Context { get; set; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, object?> Options { get; set; } = new Dictionary<string, object?>(StringComparer.Ordinal);
}

public sealed class WorkflowModelReference
{
    public string SchemaId { get; set; } = string.Empty;

    public string SchemaVersion { get; set; } = string.Empty;

    public string RuntimeBinding { get; set; } = string.Empty;
    public string? RuntimeVersion { get; set; }

    public IReadOnlyList<string> RootFields { get; set; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> NodeFields { get; set; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    public IReadOnlyList<string> RequiredRootFields { get; set; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredNodeFields { get; set; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedValues { get; set; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    public IReadOnlyList<string> ExpressionDefinitionFields { get; set; } = [];

    public IReadOnlyDictionary<string, string> CommandParameterContracts { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed class WorkflowScriptVerificationResult
{
    public bool Passed { get; set; }

    public List<string> Checks { get; set; } = [];

    public List<string> Failures { get; set; } = [];

    public List<WorkflowScriptVerificationCheck> TestCases { get; set; } = [];

    public int TotalChecks { get; set; }

    public int PassedChecks { get; set; }

    public int FailedChecks { get; set; }

    public int SkippedChecks { get; set; }

    public bool RuntimeEvidenceObserved { get; set; }

    public Dictionary<string, object?> NormalizedDiff { get; set; } = new(StringComparer.Ordinal);

    public List<string> EvidenceReferences { get; set; } = [];
}

public sealed class WorkflowScriptDiagnostic
{
    public string Id { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public int? StartLine { get; set; }

    public int? StartColumn { get; set; }

    public int? EndLine { get; set; }

    public int? EndColumn { get; set; }
}

public sealed class WorkflowScriptExecutionFeedback
{
    public string Status { get; set; } = "failed";

    public string ScriptFile { get; set; } = string.Empty;

    public string EntryPoint { get; set; } = string.Empty;

    public string CompilerIdentity { get; set; } = "Microsoft.CodeAnalysis.CSharp";

    public string? DiagnosticCode { get; set; }

    public string? DiagnosticCategory { get; set; }

    public string? SuggestedFix { get; set; }

    public string? Error { get; set; }

    public List<WorkflowScriptDiagnostic> Diagnostics { get; set; } = [];
}

public sealed class WorkflowScriptExecution<T>
{
    public T? Value { get; init; }

    public WorkflowScriptExecutionFeedback Feedback { get; init; } = new();

    public bool IsSuccess => string.Equals(Feedback.Status, "succeeded", StringComparison.Ordinal);
}
