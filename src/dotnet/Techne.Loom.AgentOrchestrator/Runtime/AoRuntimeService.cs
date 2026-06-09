using Techne.Loom.AgentOrchestrator.Models;

namespace Techne.Loom.AgentOrchestrator.Runtime;

public sealed class AoRuntimeService
{
    private readonly AoWorkflowStore _workflowStore;
    private readonly AoEventLogWriter _eventLog;

    public AoRuntimeService()
        : this(new AoWorkflowStore(), new AoEventLogWriter())
    {
    }

    internal AoRuntimeService(AoWorkflowStore workflowStore, AoEventLogWriter eventLog)
    {
        _workflowStore = workflowStore;
        _eventLog = eventLog;
    }

    public async Task<AoControlPayload> RunAsync(string objective, Dictionary<string, object?> context, string workflowFile, string eventLogFile)
    {
        var normalizedContext = new Dictionary<string, object?>(context, StringComparer.Ordinal);
        var plan = AoBoundaryPlanner.CreatePlan(normalizedContext);
        var now = DateTimeOffset.UtcNow;

        var snapshot = new AoWorkflowSnapshot(
            Objective: objective,
            Context: normalizedContext,
            Status: "blocked",
            CurrentNodeId: plan.CurrentNodeId,
            LastTransitionId: plan.TransitionId,
            LastBoundaryReason: plan.Reason,
            UpdatedAt: now);

        await _workflowStore.SaveAsync(workflowFile, snapshot).ConfigureAwait(false);
        await AppendStatusChangeAsync(workflowFile, eventLogFile, fromStatus: null, toStatus: "blocked").ConfigureAwait(false);
        await AppendBoundaryAsync(workflowFile, eventLogFile, plan.Reason, plan.TransitionId, correlationKey: null).ConfigureAwait(false);

        return new AoControlPayload(
            Status: "blocked",
            WorkflowFile: workflowFile,
            EventLogFile: eventLogFile,
            CurrentNodeId: plan.CurrentNodeId,
            BoundaryReason: plan.Reason,
            PendingRequirements: plan.PendingRequirements,
            NextFrontier: plan.NextFrontier,
            HumanOrAgentHint: plan.Hint,
            SamplingRequest: plan.SamplingRequest);
    }

    public async Task<AoControlPayload> ResumeAsync(string workflowFile, string eventLogFile, AoResumeEnvelope envelope)
    {
        if (!File.Exists(workflowFile))
        {
            throw new InvalidOperationException($"Workflow file '{workflowFile}' was not found.");
        }

        var snapshot = await _workflowStore.LoadAsync(workflowFile).ConfigureAwait(false);
        var mergedContext = new Dictionary<string, object?>(snapshot.Context, StringComparer.Ordinal);
        if (envelope.Payload is not null)
        {
            foreach (var pair in envelope.Payload)
            {
                mergedContext[pair.Key] = pair.Value;
            }
        }

        if (ShouldMarkCompleted(mergedContext))
        {
            var completedSnapshot = snapshot with
            {
                Context = mergedContext,
                Status = "completed",
                CurrentNodeId = "state.completed",
                LastTransitionId = null,
                LastBoundaryReason = null,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            await _workflowStore.SaveAsync(workflowFile, completedSnapshot).ConfigureAwait(false);
            await AppendStatusChangeAsync(workflowFile, eventLogFile, snapshot.Status, "completed").ConfigureAwait(false);

            return new AoControlPayload(
                Status: "completed",
                WorkflowFile: workflowFile,
                EventLogFile: eventLogFile,
                CurrentNodeId: completedSnapshot.CurrentNodeId,
                HumanOrAgentHint: "AO completed with provided resume payload.");
        }

        var plan = AoBoundaryPlanner.CreatePlan(mergedContext);
        var blockedSnapshot = snapshot with
        {
            Context = mergedContext,
            Status = "blocked",
            CurrentNodeId = plan.CurrentNodeId,
            LastTransitionId = plan.TransitionId,
            LastBoundaryReason = plan.Reason,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _workflowStore.SaveAsync(workflowFile, blockedSnapshot).ConfigureAwait(false);
        if (!string.Equals(snapshot.Status, "blocked", StringComparison.Ordinal))
        {
            await AppendStatusChangeAsync(workflowFile, eventLogFile, snapshot.Status, "blocked").ConfigureAwait(false);
        }

        await AppendBoundaryAsync(workflowFile, eventLogFile, plan.Reason, plan.TransitionId, envelope.CorrelationKey).ConfigureAwait(false);

        return new AoControlPayload(
            Status: "blocked",
            WorkflowFile: workflowFile,
            EventLogFile: eventLogFile,
            CurrentNodeId: plan.CurrentNodeId,
            BoundaryReason: plan.Reason,
            PendingRequirements: plan.PendingRequirements,
            NextFrontier: plan.NextFrontier,
            HumanOrAgentHint: plan.Hint,
            SamplingRequest: plan.SamplingRequest);
    }

    private async Task AppendStatusChangeAsync(string workflowFile, string eventLogFile, string? fromStatus, string toStatus)
    {
        await _eventLog.AppendAsync(
            eventLogFile,
            new AoEventRecord(
                Timestamp: DateTimeOffset.UtcNow,
                EventType: "status_change",
                WorkflowFile: workflowFile,
                EventLogFile: eventLogFile,
                FromStatus: fromStatus,
                ToStatus: toStatus)).ConfigureAwait(false);
    }

    private async Task AppendBoundaryAsync(string workflowFile, string eventLogFile, string boundaryReason, string transitionId, string? correlationKey)
    {
        await _eventLog.AppendAsync(
            eventLogFile,
            new AoEventRecord(
                Timestamp: DateTimeOffset.UtcNow,
                EventType: "boundary",
                WorkflowFile: workflowFile,
                EventLogFile: eventLogFile,
                BoundaryReason: boundaryReason,
                TransitionId: transitionId,
                CorrelationKey: correlationKey)).ConfigureAwait(false);
    }

    private static bool ShouldMarkCompleted(Dictionary<string, object?> context)
    {
        return TryGetBoolean(context, "mark_completed")
            || TryGetBoolean(context, "completed")
            || TryGetBoolean(context, "is_completed");
    }

    private static bool TryGetBoolean(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        return value switch
        {
            bool boolValue => boolValue,
            string text when bool.TryParse(text, out var parsed) => parsed,
            _ => false,
        };
    }
}
