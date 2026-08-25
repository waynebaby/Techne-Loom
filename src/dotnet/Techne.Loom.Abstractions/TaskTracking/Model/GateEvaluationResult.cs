namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed record GateEvaluationResult
{
    public bool Passed { get; init; }

    public string InstanceId { get; init; } = string.Empty;

    public string TransitionId { get; init; } = string.Empty;

    public string? StepKind { get; init; }

    public string? GateId { get; init; }

    public string? InstanceBinding { get; init; }

    public string? FailedCheck { get; init; }

    public string? ExpectedPayloadShape { get; init; }

    public IReadOnlyList<string> ReceivedPayloadTopLevelKeys { get; init; } = [];

    public IReadOnlyList<string> RequiredInputs { get; init; } = [];

    public string? ResumeOutputKey { get; init; }

    public string? OutputPath { get; init; }

    public IReadOnlyList<string> ProjectedContextPaths { get; init; } = [];

    public IReadOnlyList<string> MissingOutputFamilies { get; init; } = [];

    public IReadOnlyList<string> EmptyOutputFamilies { get; init; } = [];

    public IReadOnlyDictionary<string, string?> ResolvedOutputPaths { get; init; } = new Dictionary<string, string?>(StringComparer.Ordinal);

    public string? PassExpressionSource { get; init; }

    public string? NextAction { get; init; }

    public static GateEvaluationResult Succeeded(string instanceId, string transitionId, string? stepKind)
    {
        return new GateEvaluationResult
        {
            Passed = true,
            InstanceId = instanceId,
            TransitionId = transitionId,
            StepKind = stepKind,
        };
    }
}
