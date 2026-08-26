using Techne.Loom.Abstractions.TaskTracking;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Abstractions.TaskTracking.Runtime;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Visualizer;

namespace Techne.Loom.SkillOrchestrator.TaskTracking;

public sealed class DefaultWorkflowTaskTrackingService : IWorkflowTaskTrackingService
{
    private readonly ITaskTrackingEngine _engine;
    private readonly IReadOnlyDictionary<WorkflowInstanceVisualizerType, IWorkflowInstanceVisualizer> _visualizers;
    private readonly ISystemClock _clock;

    public DefaultWorkflowTaskTrackingService(
        ITaskTrackingEngine engine,
        IReadOnlyDictionary<WorkflowInstanceVisualizerType, IWorkflowInstanceVisualizer>? visualizers = null,
        ISystemClock? clock = null)
    {
        _engine = engine;
        _clock = clock ?? new SystemClock();
        _visualizers = visualizers ?? new Dictionary<WorkflowInstanceVisualizerType, IWorkflowInstanceVisualizer>
        {
            [WorkflowInstanceVisualizerType.Mermaid] = new MermaidWorkflowInstanceVisualizer(),
            [WorkflowInstanceVisualizerType.Html] = new HtmlWorkflowInstanceVisualizer(),
            [WorkflowInstanceVisualizerType.AsciiArt] = new AsciiArtWorkflowInstanceVisualizer(),
            [WorkflowInstanceVisualizerType.Svg] = new SvgWorkflowInstanceVisualizer(),
        };
    }

    public DefaultWorkflowTaskTrackingService(IServiceProvider serviceProvider)
        : this(
            GetRequiredService<ITaskTrackingEngine>(serviceProvider),
            clock: serviceProvider.GetService(typeof(ISystemClock)) as ISystemClock)
    {
    }

    public async Task<string> GetVisualAsync(
        string instanceId,
        WorkflowInstanceVisualizerType visualType = WorkflowInstanceVisualizerType.Mermaid,
        CancellationToken ct = default)
    {
        var instance = await GetRequiredInstanceAsync(instanceId, ct).ConfigureAwait(false);
        if (!_visualizers.TryGetValue(visualType, out var visualizer))
        {
            throw new InvalidOperationException($"No visualizer is registered for '{visualType}'.");
        }

        return await visualizer.VisualizeToStringAsync(instance, VisualizerLevel.Detailed).ConfigureAwait(false);
    }

    public async Task<WorkflowInstanceStatus> DraftAndSaveWorkflowAsync(
        string userDescription,
        Dictionary<string, object?>? initialContext = null,
        CancellationToken ct = default)
    {
        var instance = await _engine.GenerateWorkflowAsync(userDescription, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Engine did not generate a workflow instance.");

        if (initialContext is not null)
        {
            foreach (var pair in initialContext)
            {
                instance.Context[pair.Key] = pair.Value;
            }
        }

        await _engine.InstanceStore.SaveNewAsync(instance, ct).ConfigureAwait(false);
        return CreateStatusProjection(instance);
    }

    public async Task<WorkflowInstanceStatus> SaveWorkflowAsync(WorkflowInstance draft, bool autoStart = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(draft.InstanceId))
        {
            draft.InstanceId = Guid.NewGuid().ToString("N");
        }

        if (autoStart && draft.Status == WorkflowStatus.ReadyToStart)
        {
            draft.Status = WorkflowStatus.Running;
            draft.CurrentNodeId = draft.StartNodeId;
        }

        draft.LastActivityUtc = _clock.UtcNow;

        var existing = await _engine.InstanceStore.GetAsync(draft.InstanceId, ct).ConfigureAwait(false);
        if (existing is null)
        {
            await _engine.InstanceStore.SaveNewAsync(draft, ct).ConfigureAwait(false);
            return CreateStatusProjection(draft);
        }

        if (!await _engine.InstanceStore.TryUpdateAsync(draft, draft.Version, ct).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Workflow instance '{draft.InstanceId}' update failed optimistic concurrency validation.");
        }

        return CreateStatusProjection(draft);
    }

    public async Task<WorkflowTickResult> StartOrAdvanceAsync(
        string instanceId,
        Dictionary<string, object?>? contextDelta = null,
        CancellationToken ct = default)
    {
        var instance = await GetRequiredInstanceAsync(instanceId, ct).ConfigureAwait(false);
        var expectedVersion = instance.Version;
        var originalStatus = instance.Status;
        var originalCurrentNodeId = instance.CurrentNodeId;
        var originalHistoryCount = instance.History.Count;
        var originalActiveWaitGroupCount = instance.ActiveWaitGroups.Count;

        if (contextDelta is not null)
        {
            foreach (var pair in contextDelta)
            {
                instance.Context[pair.Key] = pair.Value;
            }
        }

        var outcome = await _engine.TickAsync(instance, ct).ConfigureAwait(false);
        var runtimeEvidenceWasAlreadyObserved = WorkflowRuntimeEvidenceRegistry.IsObserved(instance);
        var shouldMarkRuntimeEvidence = outcome.Progressed || outcome.Moved || outcome.Suspended;
        var runtimeEvidenceMarkedForOperation = shouldMarkRuntimeEvidence && !runtimeEvidenceWasAlreadyObserved;
        if (shouldMarkRuntimeEvidence)
        {
            WorkflowRuntimeEvidenceRegistry.MarkObserved(instance);
        }

        var shouldPersist = contextDelta is not null
            || outcome.Progressed
            || outcome.Moved
            || outcome.Suspended
            || outcome.Failed
            || instance.Status != originalStatus
            || !string.Equals(instance.CurrentNodeId, originalCurrentNodeId, StringComparison.Ordinal)
            || instance.History.Count != originalHistoryCount
            || instance.ActiveWaitGroups.Count != originalActiveWaitGroupCount;

        try
        {
            if (shouldPersist)
            {
                instance.LastActivityUtc = _clock.UtcNow;
                if (!await _engine.InstanceStore.TryUpdateAsync(instance, expectedVersion, ct).ConfigureAwait(false))
                {
                    throw new InvalidOperationException($"Workflow instance '{instanceId}' update failed optimistic concurrency validation.");
                }
            }
        }
        catch
        {
            if (runtimeEvidenceMarkedForOperation)
            {
                WorkflowRuntimeEvidenceRegistry.RemoveObserved(instance);
            }

            throw;
        }

        var projection = CreateStatusProjection(instance);
        return new WorkflowTickResult(
            instance.InstanceId,
            outcome.Progressed,
            outcome.Moved,
            outcome.Suspended,
            outcome.Failed,
            outcome.NextNodeId,
            instance.Version,
            outcome.Backoff,
            outcome.ErrorMessage,
            projection);
    }

    public async Task<WorkflowInstanceStatus> GetStatusAsync(string instanceId, CancellationToken ct = default)
    {
        return await _engine.InstanceStore.GetStatusAsync(instanceId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Workflow instance '{instanceId}' was not found.");
    }

    public Task<WorkflowInstance?> GetInstanceAsync(string instanceId, CancellationToken ct = default)
    {
        return _engine.InstanceStore.GetAsync(instanceId, ct);
    }

    public async Task<WorkflowInstanceStatus> CancelAsync(string instanceId, string? reason = null, CancellationToken ct = default)
    {
        var instance = await GetRequiredInstanceAsync(instanceId, ct).ConfigureAwait(false);
        if (!await _engine.InstanceStore.TryCancelAsync(instanceId, instance.Version, reason, ct).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Workflow instance '{instanceId}' cancel failed optimistic concurrency validation.");
        }

        return await GetStatusAsync(instanceId, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<WorkflowInstanceStatus>> ListAsync(int? top = null, CancellationToken ct = default)
    {
        return _engine.InstanceStore.ListStatusAsync(top, ct);
    }

    public async Task<WorkflowInstanceStatus> ResumeAsync(
        string instanceId,
        string transitionId,
        string? correlationKey = null,
        Dictionary<string, object?>? payload = null,
        CancellationToken ct = default)
    {
        var instance = await GetRequiredInstanceAsync(instanceId, ct).ConfigureAwait(false);
        var expectedVersion = instance.Version;
        await _engine.ResumeAsync(instance, transitionId, correlationKey, payload, ct).ConfigureAwait(false);
        var runtimeEvidenceWasAlreadyObserved = WorkflowRuntimeEvidenceRegistry.IsObserved(instance);
        var shouldMarkRuntimeEvidence = instance.Status is WorkflowStatus.Running or WorkflowStatus.Succeeded;
        var runtimeEvidenceMarkedForOperation = shouldMarkRuntimeEvidence && !runtimeEvidenceWasAlreadyObserved;
        if (shouldMarkRuntimeEvidence)
        {
            WorkflowRuntimeEvidenceRegistry.MarkObserved(instance);
        }

        try
        {
            instance.LastActivityUtc = _clock.UtcNow;

            if (!await _engine.InstanceStore.TryUpdateAsync(instance, expectedVersion, ct).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"Workflow instance '{instanceId}' resume failed optimistic concurrency validation.");
            }
        }
        catch
        {
            if (runtimeEvidenceMarkedForOperation)
            {
                WorkflowRuntimeEvidenceRegistry.RemoveObserved(instance);
            }

            throw;
        }

        return CreateStatusProjection(instance);
    }

    private async Task<WorkflowInstance> GetRequiredInstanceAsync(string instanceId, CancellationToken ct)
    {
        return await _engine.InstanceStore.GetAsync(instanceId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Workflow instance '{instanceId}' was not found.");
    }

    private static T GetRequiredService<T>(IServiceProvider serviceProvider)
        where T : class
    {
        return serviceProvider.GetService(typeof(T)) as T
            ?? throw new InvalidOperationException($"Missing required service '{typeof(T).FullName}'.");
    }

    private static WorkflowInstanceStatus CreateStatusProjection(WorkflowInstance instance)
    {
        return new WorkflowInstanceStatus(
            instance.InstanceId,
            instance.Status,
            instance.StartNodeId,
            instance.CurrentNodeId,
            instance.EndNodeId,
            instance.Version,
            instance.LastActivityUtc,
            instance.ActiveWaitGroups.Count);
    }
}
