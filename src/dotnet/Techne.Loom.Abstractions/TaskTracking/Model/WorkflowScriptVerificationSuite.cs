namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class WorkflowScriptVerificationCheck
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = "model";

    public bool Passed { get; set; }

    public bool Skipped { get; set; }

    public string? Message { get; set; }

    public string? Expected { get; set; }

    public string? Actual { get; set; }

    public List<string> EvidenceReferences { get; set; } = [];
}

public sealed class WorkflowScriptVerificationSuite
{
    private readonly List<WorkflowScriptVerificationCheck> _testCases = [];

    public void Check(
        string id,
        bool passed,
        string? message = null,
        string category = "model",
        string? expected = null,
        string? actual = null,
        params string[] evidenceReferences)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A verification check id is required.", nameof(id));
        }

        if (_testCases.Any(item => string.Equals(item.Id, id, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"The verification check id '{id}' is already registered.", nameof(id));
        }

        _testCases.Add(new WorkflowScriptVerificationCheck
        {
            Id = id,
            Name = id,
            Category = string.IsNullOrWhiteSpace(category) ? "model" : category,
            Passed = passed,
            Message = message,
            Expected = expected,
            Actual = actual,
            EvidenceReferences = evidenceReferences.Where(static item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToList(),
        });
    }

    public void Skip(
        string id,
        string? message = null,
        string category = "runtime",
        params string[] evidenceReferences)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A verification check id is required.", nameof(id));
        }

        if (_testCases.Any(item => string.Equals(item.Id, id, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"The verification check id '{id}' is already registered.", nameof(id));
        }

        _testCases.Add(new WorkflowScriptVerificationCheck
        {
            Id = id,
            Name = id,
            Category = string.IsNullOrWhiteSpace(category) ? "runtime" : category,
            Passed = false,
            Skipped = true,
            Message = message,
            EvidenceReferences = evidenceReferences.Where(static item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToList(),
        });
    }
    public WorkflowScriptVerificationResult Complete(
        IReadOnlyDictionary<string, object?>? normalizedDiff = null,
        params string[] evidenceReferences)
    {
        var testCases = _testCases.ToList();
        var failures = testCases
            .Where(static item => !item.Passed && !item.Skipped)
            .Select(static item => string.IsNullOrWhiteSpace(item.Message) ? item.Id : $"{item.Id}: {item.Message}")
            .ToList();
        var checks = testCases.Select(static item => item.Id).ToList();
        var evidence = testCases
            .SelectMany(static item => item.EvidenceReferences)
            .Concat(evidenceReferences)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new WorkflowScriptVerificationResult
        {
            Passed = testCases.Count > 0 && failures.Count == 0,
            Checks = checks,
            Failures = failures,
            TestCases = testCases,
            TotalChecks = testCases.Count,
            PassedChecks = testCases.Count(static item => item.Passed && !item.Skipped),
            FailedChecks = failures.Count,
            SkippedChecks = testCases.Count(static item => item.Skipped),
            NormalizedDiff = normalizedDiff is null
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                : normalizedDiff.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
            EvidenceReferences = evidence,
        };
    }
}