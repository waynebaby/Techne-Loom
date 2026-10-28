using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record WorkflowFragmentLimits(
    int MaxBytes = 262_144,
    int MaxArrayItems = 32,
    int MaxDepth = 6)
{
    public int MaxObjectProperties { get; init; } = MaxArrayItems;

    public static WorkflowFragmentLimits Default { get; } = new();

    public void Validate()
    {
        if (MaxBytes < 128)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxBytes), "MaxBytes must be at least 128.");
        }

        if (MaxArrayItems < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxArrayItems), "MaxArrayItems must be positive.");
        }

        if (MaxObjectProperties < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxObjectProperties), "MaxObjectProperties must be positive.");
        }

        if (MaxDepth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxDepth), "MaxDepth must be positive.");
        }
    }
}

public sealed record WorkflowFragmentTransitionSummary(
    string Id,
    string? Name,
    string? StepKind,
    string? TargetNodeId);

public sealed record WorkflowFragmentSummary(
    string InstanceId,
    string? TemplateKind,
    string? TaskType,
    string? WorkflowKind,
    string? CaseId,
    string? RunId,
    string? Status,
    string? StartNodeId,
    string? CurrentNodeId,
    string? EndNodeId,
    int Version,
    int NodeCount,
    int TransitionCount,
    int HistoryCount,
    int ActiveWaitGroupCount,
    DateTimeOffset? LastActivityUtc,
    IReadOnlyList<string> ContextKeys,
    bool ContextKeysTruncated,
    IReadOnlyList<WorkflowFragmentTransitionSummary> CurrentTransitions);

public sealed record WorkflowFragmentResult(
    WorkflowFragmentSummary Summary,
    string? JsonPointer,
    JsonElement? Fragment,
    int ReturnedBytes,
    bool Truncated,
    string? TruncationReason,
    [property: JsonPropertyName("operation_id")] string? OperationId = null);
