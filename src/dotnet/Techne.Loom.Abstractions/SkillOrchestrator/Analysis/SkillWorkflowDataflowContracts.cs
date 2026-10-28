using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Analysis;

public sealed record SkillWorkflowDataflowReport(
    string InstanceId,
    IReadOnlyList<WorkflowTransitionDataflow> Transitions,
    IReadOnlyDictionary<string, IReadOnlyList<string>> GateRequiredOutputFamilies,
    IReadOnlyList<WorkflowDataflowIssue> Issues)
{
    public bool IsResolved => Issues.Count == 0;
}

public enum WorkflowEmitterKind
{
    Unknown,
    ExternalResult,
    KnownNull,
    LiteralWriter,
    RealToolResult,
}

public sealed record WorkflowTransitionDataflow(
    string TransitionId,
    WorkflowStepKind StepKind,
    IReadOnlyList<string> InputPaths,
    IReadOnlyList<string> PayloadPaths,
    string? ResumeOutputKey,
    string? OutputPath,
    string? ProjectionMode,
    IReadOnlyDictionary<string, string?> OutputBindings,
    IReadOnlyList<string> ProducedContextPaths,
    IReadOnlyList<string> PublishedOutputFamilies,
    IReadOnlyList<string> SatisfiedGateIds,
    IReadOnlyList<string> RouteNames,
    IReadOnlyList<string> UnresolvedOutputFamilies,
    WorkflowEmitterKind EmitterKind = WorkflowEmitterKind.Unknown,
    IReadOnlyList<string>? UpdatesKeys = null,
    string? ToolName = null);

public sealed record WorkflowDataflowIssue(
    string? TransitionId,
    string? GateId,
    string OutputFamily,
    string Reason,
    WorkflowEmitterKind EmitterKind = WorkflowEmitterKind.Unknown);
