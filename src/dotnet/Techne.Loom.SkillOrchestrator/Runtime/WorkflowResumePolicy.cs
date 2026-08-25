using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Runtime;

internal enum FailedResumeRejectionReason
{
    NotFailed,
    NoFailedTransition,
    RequestedTransitionMismatch,
    PreviousStateMissing,
    TransitionNotOwned,
}

internal sealed record FailedResumeAssessment(
    bool IsRecoverable,
    string? FailedTransitionId,
    string? PreviousStateId,
    FailedResumeRejectionReason? RejectionReason);

internal static class WorkflowResumePolicy
{
    public static bool CanResume(WorkflowInstance instance)
    {
        return instance.Status == WorkflowStatus.Failed
            ? AssessFailedInstance(instance).IsRecoverable
            : instance.Status == WorkflowStatus.WaitingExternal && instance.ActiveWaitGroups.Count > 0;
    }

    public static bool RequiresFreshInstance(WorkflowInstance instance)
    {
        return instance.Status == WorkflowStatus.Succeeded
            || (instance.Status == WorkflowStatus.Failed && !AssessFailedInstance(instance).IsRecoverable);
    }

    public static FailedResumeAssessment AssessFailedInstance(WorkflowInstance instance, string? requestedTransitionId = null)
    {
        if (instance.Status != WorkflowStatus.Failed)
        {
            return new FailedResumeAssessment(false, null, null, FailedResumeRejectionReason.NotFailed);
        }

        var failedEntry = instance.History.LastOrDefault(static entry =>
            entry.NodeType == TaskNodeType.Transition && entry.Status == ExecutionStatus.Failed);
        if (failedEntry is null)
        {
            return new FailedResumeAssessment(false, null, instance.CurrentNodeId, FailedResumeRejectionReason.NoFailedTransition);
        }

        var failedTransitionId = failedEntry.NodeId;
        if (!string.IsNullOrWhiteSpace(requestedTransitionId)
            && !string.Equals(failedTransitionId, requestedTransitionId, StringComparison.Ordinal))
        {
            return new FailedResumeAssessment(false, failedTransitionId, instance.CurrentNodeId, FailedResumeRejectionReason.RequestedTransitionMismatch);
        }

        if (!instance.Nodes.TryGetValue(instance.CurrentNodeId, out var currentNode) || currentNode is not StateNode state)
        {
            return new FailedResumeAssessment(false, failedTransitionId, instance.CurrentNodeId, FailedResumeRejectionReason.PreviousStateMissing);
        }

        if (!state.Groups.Any(group => group.TransitionIds.Contains(failedTransitionId, StringComparer.Ordinal))
            || !instance.Nodes.TryGetValue(failedTransitionId, out var failedNode)
            || failedNode is not TransitionBase)
        {
            return new FailedResumeAssessment(false, failedTransitionId, state.Id, FailedResumeRejectionReason.TransitionNotOwned);
        }

        return new FailedResumeAssessment(true, failedTransitionId, state.Id, null);
    }
}
