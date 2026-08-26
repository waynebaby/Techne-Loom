using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class WorkflowScriptVerificationResultMerger
{
    public static WorkflowScriptVerificationResult Merge(
        WorkflowScriptVerificationResult builtIn,
        WorkflowScriptVerificationResult? custom)
    {
        ArgumentNullException.ThrowIfNull(builtIn);

        var sources = custom is null
            ? new[] { (Name: "builtIn", Result: builtIn) }
            : new[] { (Name: "builtIn", Result: builtIn), (Name: "script", Result: custom) };
        var cases = new List<WorkflowScriptVerificationCheck>();
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        var failures = new List<string>();
        var evidence = new List<string>();
        var normalizedDiff = new Dictionary<string, object?>(StringComparer.Ordinal);
        var allPassed = true;

        foreach (var source in sources)
        {
            allPassed &= source.Result.Passed;
            evidence.AddRange(source.Result.EvidenceReferences);
            normalizedDiff[$"{source.Name}NormalizedDiff"] = source.Result.NormalizedDiff;
            var sourceCases = source.Result.TestCases.Count == 0
                ? [new WorkflowScriptVerificationCheck
                {
                    Id = $"{source.Name}.result",
                    Name = $"{source.Name} result",
                    Category = source.Name == "builtIn" ? "model" : "script",
                    Passed = source.Result.Passed,
                    Message = source.Result.Passed ? "The verification result passed." : "The verification result failed.",
                }]
                : source.Result.TestCases;

            foreach (var testCase in sourceCases)
            {
                var id = testCase.Id;
                if (!usedIds.Add(id))
                {
                    id = $"{source.Name}.{id}";
                    usedIds.Add(id);
                }

                cases.Add(new WorkflowScriptVerificationCheck
                {
                    Id = id,
                    Name = testCase.Name,
                    Category = testCase.Category,
                    Passed = testCase.Passed,
                    Skipped = testCase.Skipped,
                    Message = testCase.Message,
                    Expected = testCase.Expected,
                    Actual = testCase.Actual,
                    EvidenceReferences = testCase.EvidenceReferences.ToList(),
                });
            }

            failures.AddRange(source.Result.Failures.Select(failure => $"{source.Name}: {failure}"));
        }

        failures = cases
            .Where(static item => !item.Passed && !item.Skipped)
            .Select(static item => string.IsNullOrWhiteSpace(item.Message) ? item.Id : $"{item.Id}: {item.Message}")
            .Concat(failures.Where(failure => !string.IsNullOrWhiteSpace(failure)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var duplicateFreeEvidence = evidence
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new WorkflowScriptVerificationResult
        {
            Passed = allPassed && cases.Count > 0 && failures.Count == 0,
            Checks = cases.Select(static item => item.Id).ToList(),
            Failures = failures,
            TestCases = cases,
            TotalChecks = cases.Count,
            PassedChecks = cases.Count(static item => item.Passed && !item.Skipped),
            FailedChecks = cases.Count(static item => !item.Passed && !item.Skipped),
            SkippedChecks = cases.Count(static item => item.Skipped),
            RuntimeEvidenceObserved = sources.Any(static source => source.Result.RuntimeEvidenceObserved),
            NormalizedDiff = normalizedDiff,
            EvidenceReferences = duplicateFreeEvidence,
        };
    }
}
