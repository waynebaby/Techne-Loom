namespace Techne.Loom.SkillOrchestrator.Validation;

internal sealed class WorkflowValidationResult
{
    public List<WorkflowValidationDiagnostic> Diagnostics { get; } = [];

    public bool HasErrors => Diagnostics.Count > 0;

    public void Add(string ruleId, string message, string? location = null, string? suggestion = null)
    {
        Diagnostics.Add(new WorkflowValidationDiagnostic(ruleId, message, location, suggestion));
    }

    public void ThrowIfInvalid()
    {
        if (!HasErrors)
        {
            return;
        }

        throw new InvalidOperationException(ToDisplayString());
    }

    public string ToDisplayString()
    {
        return string.Join(Environment.NewLine, Diagnostics.Select(static diagnostic => diagnostic.ToDisplayString()));
    }
}

internal sealed record WorkflowValidationDiagnostic(
    string RuleId,
    string Message,
    string? Location,
    string? Suggestion)
{
    public string ToDisplayString()
    {
        var parts = new List<string> { $"[{RuleId}] {Message}" };
        if (!string.IsNullOrWhiteSpace(Location))
        {
            parts.Add($"Location: {Location}");
        }

        if (!string.IsNullOrWhiteSpace(Suggestion))
        {
            parts.Add($"Suggestion: {Suggestion}");
        }

        return string.Join(" | ", parts);
    }
}