using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using System.Diagnostics;
using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.Common.TaskTracking.Runtime;

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
        string? initialInstanceFile = null,
        string? auditOutputRoot = null)
    {
        var artifacts = AoSessionArtifactPaths.CreateNew(sessionDirectory);

        return await ExecuteWithWorkflowLockAsync(
            artifacts.WorkflowFile,
            async () =>
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
                    UpdatedAt: now,
                    PendingRequirements: plan.PendingRequirements,
                    NextFrontier: plan.NextFrontier,
                    HumanOrAgentHint: plan.Hint,
                    WeaveOutRequest: plan.WeaveOutRequest,
                    AuditStepSequence: 1);

                await _workflowStore.SaveAsync(artifacts.WorkflowFile, snapshot).ConfigureAwait(false);
                var runtimeWorkflow = await CreateInitialRuntimeWorkflowAsync(artifacts, snapshot, initialInstanceFile).ConfigureAwait(false);
                await _workflowStore.SaveRuntimeWorkflowAsync(artifacts.RuntimeWorkflowFile, runtimeWorkflow).ConfigureAwait(false);
                await AppendStatusChangeAsync(artifacts, fromStatus: null, toStatus: "blocked").ConfigureAwait(false);
                await AppendBoundaryAsync(artifacts, plan.Reason, plan.TransitionId, correlationKey: null).ConfigureAwait(false);
                var auditArtifacts = await WriteAuditArtifactsAsync(runtimeWorkflow, snapshot, artifacts.RuntimeWorkflowFile, auditOutputRoot, $"blocked-{plan.Reason}").ConfigureAwait(false);

                var runtimeWorkflowFile = await ResolveCurrentRuntimeWorkflowFileAsync(artifacts).ConfigureAwait(false);

                return new AoControlPayload(
                    Status: "blocked",
                    SessionId: artifacts.SessionId,
                    WorkflowFile: artifacts.WorkflowFile,
                    WorkflowInstanceFile: runtimeWorkflowFile,
                    EventLogFile: artifacts.EventLogFile,
                    CurrentNodeId: plan.CurrentNodeId,
                    BoundaryReason: plan.Reason,
                    PendingRequirements: plan.PendingRequirements,
                    NextFrontier: plan.NextFrontier,
                    HumanOrAgentHint: plan.Hint,
                    WeaveOutRequest: plan.WeaveOutRequest,
                    AuditArtifacts: auditArtifacts);
            }).ConfigureAwait(false);
    }

    public async Task<AoControlPayload> ResumeAsync(
        string sessionDirectory,
        string sessionId,
        AoResumeEnvelope envelope,
        string? auditOutputRoot = null)
    {
        var artifacts = AoSessionArtifactPaths.ResolveExisting(sessionDirectory, sessionId);

        return await ExecuteWithWorkflowLockAsync(
            artifacts.WorkflowFile,
            async () =>
            {
                if (string.IsNullOrWhiteSpace(envelope.TransitionId))
                {
                    throw new InvalidOperationException("Invalid result envelope: 'transition_id' is required.");
                }

                var snapshot = await _workflowStore.LoadAsync(artifacts.WorkflowFile).ConfigureAwait(false);
                EnsureResumableSnapshot(snapshot, envelope.TransitionId);

                var runtimeWorkflow = await LoadRuntimeWorkflowAsync(artifacts, snapshot).ConfigureAwait(false);

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
                        PendingRequirements = null,
                        NextFrontier = null,
                        HumanOrAgentHint = "AO completed with provided resume payload.",
                        WeaveOutRequest = null,
                        AuditStepSequence = snapshot.AuditStepSequence + 1,
                    };

                    await _workflowStore.SaveAsync(artifacts.WorkflowFile, completedSnapshot).ConfigureAwait(false);
                    var completedRuntimeWorkflow = AoRuntimeWorkflowBridge.UpdateRuntimeWorkflow(runtimeWorkflow, completedSnapshot);
                    await _workflowStore.SaveRuntimeWorkflowAsync(artifacts.RuntimeWorkflowFile, completedRuntimeWorkflow).ConfigureAwait(false);
                    await AppendStatusChangeAsync(artifacts, snapshot.Status, "completed").ConfigureAwait(false);
                    var auditArtifacts = await WriteAuditArtifactsAsync(completedRuntimeWorkflow, completedSnapshot, artifacts.RuntimeWorkflowFile, auditOutputRoot, "completed").ConfigureAwait(false);

                    var runtimeWorkflowFile = await ResolveCurrentRuntimeWorkflowFileAsync(artifacts).ConfigureAwait(false);

                    return new AoControlPayload(
                        Status: "completed",
                        SessionId: artifacts.SessionId,
                        WorkflowFile: artifacts.WorkflowFile,
                        WorkflowInstanceFile: runtimeWorkflowFile,
                        EventLogFile: artifacts.EventLogFile,
                        CurrentNodeId: completedSnapshot.CurrentNodeId,
                        HumanOrAgentHint: completedSnapshot.HumanOrAgentHint,
                        AuditArtifacts: auditArtifacts);
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
                    PendingRequirements = plan.PendingRequirements,
                    NextFrontier = plan.NextFrontier,
                    HumanOrAgentHint = plan.Hint,
                    WeaveOutRequest = plan.WeaveOutRequest,
                    AuditStepSequence = snapshot.AuditStepSequence + 1,
                };

                await _workflowStore.SaveAsync(artifacts.WorkflowFile, blockedSnapshot).ConfigureAwait(false);
                var blockedRuntimeWorkflow = AoRuntimeWorkflowBridge.UpdateRuntimeWorkflow(runtimeWorkflow, blockedSnapshot);
                await _workflowStore.SaveRuntimeWorkflowAsync(artifacts.RuntimeWorkflowFile, blockedRuntimeWorkflow).ConfigureAwait(false);
                if (!string.Equals(snapshot.Status, "blocked", StringComparison.Ordinal))
                {
                    await AppendStatusChangeAsync(artifacts, snapshot.Status, "blocked").ConfigureAwait(false);
                }

                await AppendBoundaryAsync(artifacts, plan.Reason, plan.TransitionId, envelope.CorrelationKey).ConfigureAwait(false);
                var blockedAuditArtifacts = await WriteAuditArtifactsAsync(blockedRuntimeWorkflow, blockedSnapshot, artifacts.RuntimeWorkflowFile, auditOutputRoot, $"blocked-{plan.Reason}").ConfigureAwait(false);

                var blockedRuntimeWorkflowFile = await ResolveCurrentRuntimeWorkflowFileAsync(artifacts).ConfigureAwait(false);

                return new AoControlPayload(
                    Status: "blocked",
                    SessionId: artifacts.SessionId,
                    WorkflowFile: artifacts.WorkflowFile,
                    WorkflowInstanceFile: blockedRuntimeWorkflowFile,
                    EventLogFile: artifacts.EventLogFile,
                    CurrentNodeId: plan.CurrentNodeId,
                    BoundaryReason: plan.Reason,
                    PendingRequirements: plan.PendingRequirements,
                    NextFrontier: plan.NextFrontier,
                    HumanOrAgentHint: plan.Hint,
                    WeaveOutRequest: plan.WeaveOutRequest,
                    AuditArtifacts: blockedAuditArtifacts);
            }).ConfigureAwait(false);
    }

    private static async Task<WorkflowAuditArtifacts> WriteAuditArtifactsAsync(
        WorkflowInstance runtimeWorkflow,
        AoWorkflowSnapshot snapshot,
        string workflowFile,
        string? auditOutputRoot,
        string action)
    {
        var workflowJson = WorkflowJsonSerializer.Serialize(runtimeWorkflow);
        var mermaid = AoCommandHandlersAccessor.RenderWorkflowInstanceMermaid(runtimeWorkflow);
        var html = AoCommandHandlersAccessor.RenderWorkflowInstanceHtml(runtimeWorkflow);
        return await WorkflowAuditArtifactWriter.WriteAsync(
            runtimeWorkflow.InstanceId,
            snapshot.AuditStepSequence,
            action,
            workflowJson,
            mermaid,
            html,
            auditOutputRoot).ConfigureAwait(false);
    }

    private async Task<WorkflowInstance> LoadRuntimeWorkflowAsync(AoSessionArtifacts artifacts, AoWorkflowSnapshot snapshot)
    {
        var runtimeWorkflow = File.Exists(artifacts.RuntimeWorkflowFile)
            ? await _workflowStore.LoadRuntimeWorkflowAsync(artifacts.RuntimeWorkflowFile).ConfigureAwait(false)
            : AoRuntimeWorkflowBridge.CreateInitialRuntimeWorkflow(artifacts.SessionId, snapshot);

        var pointerPath = await ReadRuntimeWorkflowPointerAsync(artifacts.RuntimeWorkflowPointerFile).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(pointerPath) && File.Exists(pointerPath))
        {
            var externalWorkflow = await _workflowStore.LoadRuntimeWorkflowAsync(pointerPath).ConfigureAwait(false);
            runtimeWorkflow = AoRuntimeWorkflowBridge.MergeExternalRuntimeWorkflow(runtimeWorkflow, externalWorkflow, snapshot);
        }

        return runtimeWorkflow;
    }

    private async Task<WorkflowInstance> CreateInitialRuntimeWorkflowAsync(AoSessionArtifacts artifacts, AoWorkflowSnapshot snapshot, string? initialInstanceFile)
    {
        if (string.IsNullOrWhiteSpace(initialInstanceFile))
        {
            return AoRuntimeWorkflowBridge.CreateInitialRuntimeWorkflow(artifacts.SessionId, snapshot);
        }

        await WriteRuntimeWorkflowPointerAsync(artifacts.RuntimeWorkflowPointerFile, initialInstanceFile).ConfigureAwait(false);
        var authoredRuntime = await _workflowStore.LoadRuntimeWorkflowAsync(initialInstanceFile).ConfigureAwait(false);
        return AoRuntimeWorkflowBridge.SeedRuntimeWorkflow(authoredRuntime, artifacts.SessionId, snapshot);
    }

    private static async Task<string> ResolveCurrentRuntimeWorkflowFileAsync(AoSessionArtifacts artifacts)
    {
        var pointerPath = await ReadRuntimeWorkflowPointerAsync(artifacts.RuntimeWorkflowPointerFile).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(pointerPath) && File.Exists(pointerPath))
        {
            return pointerPath;
        }

        return artifacts.RuntimeWorkflowFile;
    }

    private static async Task<string?> ReadRuntimeWorkflowPointerAsync(string pointerFile)
    {
        if (!File.Exists(pointerFile))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(pointerFile).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("workflow_instance_file", out var pathProperty)
            ? pathProperty.GetString()
            : null;
    }

    private static async Task WriteRuntimeWorkflowPointerAsync(string pointerFile, string instanceFile)
    {
        var payload = JsonSerializer.Serialize(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["workflow_instance_file"] = Path.GetFullPath(instanceFile),
                ["updated_at"] = DateTimeOffset.UtcNow,
            },
            WorkflowJsonSerializer.CreateDefaultOptions(indented: true));

        var directory = Path.GetDirectoryName(pointerFile);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(pointerFile, payload).ConfigureAwait(false);
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

    private static class AoCommandHandlersAccessor
    {
        public static string RenderWorkflowInstanceMermaid(WorkflowInstance instance)
            => Cli.AoCommandHandlers.RenderWorkflowInstanceMermaidForRuntime(instance);

        public static string RenderWorkflowInstanceHtml(WorkflowInstance instance)
            => Cli.AoCommandHandlers.RenderWorkflowInstanceHtmlForRuntime(instance);
    }
}
