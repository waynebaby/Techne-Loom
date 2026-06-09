using System.Diagnostics;
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

    public async Task<AoControlPayload> RunAsync(
        string objective,
        Dictionary<string, object?> context,
        string sessionDirectory,
        AoInvocationContext? invocationContext = null)
    {
        var artifacts = AoSessionArtifactPaths.CreateNew(sessionDirectory);

        return await ExecuteWithWorkflowLockAsync(
            artifacts.WorkflowFile,
            async () =>
            {
                ValidateInvocationContext(invocationContext);
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

                await _workflowStore.SaveAsync(artifacts.WorkflowFile, snapshot).ConfigureAwait(false);
                await AppendStatusChangeAsync(artifacts, fromStatus: null, toStatus: "blocked").ConfigureAwait(false);
                await AppendBoundaryAsync(artifacts, plan.Reason, plan.TransitionId, correlationKey: null).ConfigureAwait(false);

                return new AoControlPayload(
                    Status: "blocked",
                    SessionId: artifacts.SessionId,
                    WorkflowFile: artifacts.WorkflowFile,
                    EventLogFile: artifacts.EventLogFile,
                    CurrentNodeId: plan.CurrentNodeId,
                    BoundaryReason: plan.Reason,
                    PendingRequirements: plan.PendingRequirements,
                    NextFrontier: plan.NextFrontier,
                    HumanOrAgentHint: plan.Hint,
                    WeaveOutRequest: plan.WeaveOutRequest);
            }).ConfigureAwait(false);
    }

    public async Task<AoControlPayload> ResumeAsync(
        string sessionDirectory,
        string sessionId,
        AoResumeEnvelope envelope,
        AoInvocationContext? invocationContext = null)
    {
        var artifacts = AoSessionArtifactPaths.ResolveExisting(sessionDirectory, sessionId);

        return await ExecuteWithWorkflowLockAsync(
            artifacts.WorkflowFile,
            async () =>
            {
                ValidateInvocationContext(invocationContext);

                if (string.IsNullOrWhiteSpace(envelope.TransitionId))
                {
                    throw new InvalidOperationException("Invalid result envelope: 'transition_id' is required.");
                }

                var snapshot = await _workflowStore.LoadAsync(artifacts.WorkflowFile).ConfigureAwait(false);
                EnsureResumableSnapshot(snapshot, envelope.TransitionId);

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

                    await _workflowStore.SaveAsync(artifacts.WorkflowFile, completedSnapshot).ConfigureAwait(false);
                    await AppendStatusChangeAsync(artifacts, snapshot.Status, "completed").ConfigureAwait(false);

                    return new AoControlPayload(
                        Status: "completed",
                        SessionId: artifacts.SessionId,
                        WorkflowFile: artifacts.WorkflowFile,
                        EventLogFile: artifacts.EventLogFile,
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

                await _workflowStore.SaveAsync(artifacts.WorkflowFile, blockedSnapshot).ConfigureAwait(false);
                if (!string.Equals(snapshot.Status, "blocked", StringComparison.Ordinal))
                {
                    await AppendStatusChangeAsync(artifacts, snapshot.Status, "blocked").ConfigureAwait(false);
                }

                await AppendBoundaryAsync(artifacts, plan.Reason, plan.TransitionId, envelope.CorrelationKey).ConfigureAwait(false);

                return new AoControlPayload(
                    Status: "blocked",
                    SessionId: artifacts.SessionId,
                    WorkflowFile: artifacts.WorkflowFile,
                    EventLogFile: artifacts.EventLogFile,
                    CurrentNodeId: plan.CurrentNodeId,
                    BoundaryReason: plan.Reason,
                    PendingRequirements: plan.PendingRequirements,
                    NextFrontier: plan.NextFrontier,
                    HumanOrAgentHint: plan.Hint,
                    WeaveOutRequest: plan.WeaveOutRequest);
            }).ConfigureAwait(false);
    }

    private async Task AppendStatusChangeAsync(AoSessionArtifacts artifacts, string? fromStatus, string toStatus)
    {
        await _eventLog.AppendAsync(
            artifacts.EventLogFile,
            new AoEventRecord(
                Timestamp: DateTimeOffset.UtcNow,
                EventType: "status_change",
                SessionId: artifacts.SessionId,
                WorkflowFile: artifacts.WorkflowFile,
                EventLogFile: artifacts.EventLogFile,
                FromStatus: fromStatus,
                ToStatus: toStatus)).ConfigureAwait(false);
    }

    private async Task AppendBoundaryAsync(AoSessionArtifacts artifacts, string boundaryReason, string transitionId, string? correlationKey)
    {
        await _eventLog.AppendAsync(
            artifacts.EventLogFile,
            new AoEventRecord(
                Timestamp: DateTimeOffset.UtcNow,
                EventType: "boundary",
                SessionId: artifacts.SessionId,
                WorkflowFile: artifacts.WorkflowFile,
                EventLogFile: artifacts.EventLogFile,
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

    private static void EnsureResumableSnapshot(AoWorkflowSnapshot snapshot, string transitionId)
    {
        if (!string.Equals(snapshot.Status, "blocked", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Workflow is not in a resumable state (status: {snapshot.Status}).");
        }

        if (string.IsNullOrWhiteSpace(snapshot.LastTransitionId))
        {
            throw new InvalidOperationException("Workflow is not in a resumable state: no pending transition is recorded.");
        }

        if (!string.Equals(snapshot.LastTransitionId, transitionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resume rejected: transition_id '{transitionId}' does not match the current workflow boundary '{snapshot.LastTransitionId}'.");
        }
    }

    private static void ValidateInvocationContext(AoInvocationContext? invocationContext)
    {
        if (invocationContext?.WeaveOut is not { } weaveOut)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(weaveOut.Route))
        {
            throw new InvalidOperationException(
                "Invalid invocation_context: 'weave_out.route' is required when weave-out context is provided.");
        }
    }

    private static async Task<T> ExecuteWithWorkflowLockAsync<T>(string workflowFile, Func<Task<T>> action)
    {
        await using var lockStream = await AcquireWorkflowLockAsync(workflowFile).ConfigureAwait(false);
        return await action().ConfigureAwait(false);
    }

    private static string GetWorkflowLockFile(string workflowFile)
    {
        return Path.GetFullPath(workflowFile) + ".lock";
    }

    private static async Task<FileStream> AcquireWorkflowLockAsync(string workflowFile)
    {
        var lockFile = GetWorkflowLockFile(workflowFile);
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            try
            {
                EnsureParentDirectory(lockFile);
                return new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (stopwatch.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(50).ConfigureAwait(false);
            }
        }
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
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
