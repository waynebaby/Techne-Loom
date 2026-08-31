using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Validation;

internal sealed class WorkflowValidationResult
{
    internal static readonly IReadOnlyList<(string Name, string[] Prerequisites)> PhaseDefinitions =
    [
        ("parse", []),
        ("structure", ["parse"]),
        ("local_contracts", []),
        ("expressions", []),
        ("governance", []),
        ("dataflow", ["structure"]),
        ("reachability", ["structure"]),
    ];


    public List<WorkflowCompileDiagnostic> Diagnostics { get; } = [];

    public bool HasErrors => Diagnostics.Any(static diagnostic => IsBlocking(diagnostic.Severity));

    public bool HasWarnings => Diagnostics.Any(static diagnostic => string.Equals(diagnostic.Severity, "warning", StringComparison.OrdinalIgnoreCase));

    public bool HasBlockingInPhase(string phase)
        => Diagnostics.Any(diagnostic => string.Equals(diagnostic.Phase, phase, StringComparison.OrdinalIgnoreCase) && IsBlocking(diagnostic.Severity));

    public void Add(
        string ruleId,
        string message,
        string? location = null,
        string? suggestion = null,
        string? code = null,
        string? category = null,
        string severity = "error",
        string? phase = null,
        IEnumerable<string>? blockedBy = null,
        ExpressionCompileFeedback? expressionFeedback = null)
    {
        var resolvedPhase = phase ?? ResolveDefaultPhase(ruleId);
        var resolvedCategory = category ?? ResolveDefaultCategory(ruleId);
        Diagnostics.Add(new WorkflowCompileDiagnostic
        {
            RuleId = ruleId,
            Code = code ?? ruleId,
            Category = resolvedCategory,
            Severity = severity,
            Message = message,
            Location = location,
            SuggestedFix = suggestion,
            Phase = resolvedPhase,
            BlockedBy = blockedBy?.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(static value => value, StringComparer.Ordinal).ToList() ?? [],
            ExpressionFeedback = expressionFeedback
        });
    }

    public void AddBlockedPhase(string phase, IEnumerable<string> blockedBy, string reason)
    {
        var dependencies = blockedBy
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        Add(
            "LOOM.COMPILE.PHASE_BLOCKED",
            $"Validation phase '{phase}' was blocked because its prerequisite phase reported an unsafe workflow: {reason}",
            $"phase:{phase}",
            "Repair the prerequisite diagnostics before rerunning compile.",
            code: "LOOM.COMPILE.PHASE_BLOCKED",
            category: "resource",
            severity: "blocked",
            phase: phase,
            blockedBy: dependencies);
    }

    public void Normalize()
    {
        var normalized = Diagnostics
            .Where(static diagnostic => diagnostic is not null)
            .Select(NormalizeDiagnostic)
            .GroupBy(static diagnostic => $"{diagnostic.Code}\u001f{diagnostic.Location}\u001f{diagnostic.Message}", StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static diagnostic => PhaseOrder(diagnostic.Phase))
            .ThenBy(static diagnostic => SeverityOrder(diagnostic.Severity))
            .ThenBy(static diagnostic => diagnostic.Location, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToList();
        Diagnostics.Clear();
        Diagnostics.AddRange(normalized);
    }

    public WorkflowCompileFeedback ToFeedback(string product, string runtime, string? workflowPath = null, string? workflowHash = null)
    {
        Normalize();
        var feedback = new WorkflowCompileFeedback
        {
            Product = product,
            Runtime = runtime,
            Status = HasErrors ? "failed" : "succeeded",
            WorkflowPath = workflowPath,
            WorkflowHash = workflowHash,
            Diagnostics = [.. Diagnostics],
            Phases = PhaseDefinitions.Select(definition => new WorkflowCompilePhaseFeedback
            {
                Name = definition.Name,
                Prerequisites = [.. definition.Prerequisites],
                DiagnosticCount = Diagnostics.Count(diagnostic => string.Equals(diagnostic.Phase, definition.Name, StringComparison.Ordinal)),
                BlockedBy = Diagnostics
                    .Where(diagnostic => string.Equals(diagnostic.Phase, definition.Name, StringComparison.Ordinal))
                    .SelectMany(static diagnostic => diagnostic.BlockedBy)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static value => value, StringComparer.Ordinal)
                    .ToList(),
                Status = GetPhaseStatus(definition.Name)
            }).ToList(),
            Counts = new WorkflowCompileFeedbackCounts
            {
                Total = Diagnostics.Count,
                Errors = Diagnostics.Count(static diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase)),
                Warnings = Diagnostics.Count(static diagnostic => IsWarning(diagnostic.Severity)),
                Info = Diagnostics.Count(static diagnostic => string.Equals(diagnostic.Severity, "info", StringComparison.OrdinalIgnoreCase)),
                Blocked = Diagnostics.Count(static diagnostic => string.Equals(diagnostic.Severity, "blocked", StringComparison.OrdinalIgnoreCase))
            }
        };
        feedback.Truncated = Diagnostics.Any(static diagnostic => diagnostic.Message.Contains("truncated", StringComparison.OrdinalIgnoreCase))
            || Diagnostics.Any(static diagnostic => diagnostic.ExpressionFeedback?.Truncated == true)
            || Diagnostics.Any(static diagnostic => diagnostic.ExpressionFeedback is not null
                && diagnostic.ExpressionFeedback.DiagnosticCount > diagnostic.ExpressionFeedback.Diagnostics.Count);
        return feedback;
    }

    public void ThrowIfInvalid()
    {
        Normalize();
        if (HasErrors)
        {
            throw new WorkflowValidationException(this);
        }
    }

    public string ToDisplayString()
    {
        Normalize();
        return string.Join(Environment.NewLine, Diagnostics.Select(static diagnostic => FormatDiagnostic(diagnostic)));
    }

    private static string FormatDiagnostic(WorkflowCompileDiagnostic diagnostic)
    {
        var parts = new List<string> { $"[{diagnostic.RuleId}] {diagnostic.Message}" };
        if (!string.IsNullOrWhiteSpace(diagnostic.Location))
        {
            parts.Add($"Location: {diagnostic.Location}");
        }

        if (!string.IsNullOrWhiteSpace(diagnostic.SuggestedFix))
        {
            parts.Add($"Suggestion: {diagnostic.SuggestedFix}");
        }

        return string.Join(" | ", parts);
    }

    private static WorkflowCompileDiagnostic NormalizeDiagnostic(WorkflowCompileDiagnostic diagnostic)
    {
        diagnostic.Code = string.IsNullOrWhiteSpace(diagnostic.Code) ? diagnostic.RuleId : diagnostic.Code.Trim();
        diagnostic.Category = string.IsNullOrWhiteSpace(diagnostic.Category) ? "contract" : diagnostic.Category.Trim().ToLowerInvariant();
        diagnostic.Severity = string.IsNullOrWhiteSpace(diagnostic.Severity) ? "error" : diagnostic.Severity.Trim().ToLowerInvariant();
        diagnostic.Phase = string.IsNullOrWhiteSpace(diagnostic.Phase) ? "structure" : diagnostic.Phase.Trim().ToLowerInvariant();
        diagnostic.BlockedBy = diagnostic.BlockedBy.Distinct(StringComparer.Ordinal).OrderBy(static value => value, StringComparer.Ordinal).ToList();
        return diagnostic;
    }

    private static string ResolveDefaultPhase(string ruleId) => ruleId switch
    {
        "SO1000" => "structure",
        "SO2000" => "governance",
        "SO3000" => "governance",
        "SO4000" => "reachability",
        _ => "structure"
    };

    private static string ResolveDefaultCategory(string ruleId) => ruleId switch
    {
        "SO1000" => "syntax",
        "SO2000" => "contract",
        "SO3000" => "contract",
        "SO4000" => "contract",
        _ => "contract"
    };

    private string GetPhaseStatus(string phase)
    {
        if (Diagnostics.Any(diagnostic => string.Equals(diagnostic.Phase, phase, StringComparison.OrdinalIgnoreCase) && string.Equals(diagnostic.Severity, "blocked", StringComparison.OrdinalIgnoreCase)))
        {
            return "blocked";
        }
        if (HasBlockingInPhase(phase))
        {
            return "failed";
        }
        return "completed";
    }

    private static int PhaseOrder(string phase)
    {
        for (var index = 0; index < PhaseDefinitions.Count; index++)
        {
            if (string.Equals(PhaseDefinitions[index].Name, phase, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private static int SeverityOrder(string severity) => severity switch
    {
        "error" => 0,
        "blocked" => 1,
        "warning" => 2,
        "info" => 3,
        _ => 4
    };

    private static bool IsBlocking(string severity) => string.Equals(severity, "error", StringComparison.OrdinalIgnoreCase) || string.Equals(severity, "blocked", StringComparison.OrdinalIgnoreCase);
    private static bool IsWarning(string severity) => string.Equals(severity, "warning", StringComparison.OrdinalIgnoreCase);
}

internal sealed class WorkflowValidationException : InvalidOperationException
{
    public WorkflowValidationException(WorkflowValidationResult result)
        : base(result.ToDisplayString())
    {
        Result = result;
    }

    public WorkflowValidationResult Result { get; }
}