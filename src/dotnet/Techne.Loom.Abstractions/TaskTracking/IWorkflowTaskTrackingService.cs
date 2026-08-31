using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Abstractions.TaskTracking;

public interface IWorkflowTaskTrackingService
{
    Task<string> GetVisualAsync(
        string instanceId,
        WorkflowInstanceVisualizerType visualType = WorkflowInstanceVisualizerType.Mermaid,
        CancellationToken ct = default);

    Task<WorkflowInstanceStatus> DraftAndSaveWorkflowAsync(
        string userDescription,
        Dictionary<string, object?>? initialContext = null,
        CancellationToken ct = default);

    Task<WorkflowInstanceStatus> SaveWorkflowAsync(
        WorkflowInstance draft,
        bool autoStart = false,
        CancellationToken ct = default);

    Task<WorkflowTickResult> StartOrAdvanceAsync(
        string instanceId,
        Dictionary<string, object?>? contextDelta = null,
        CancellationToken ct = default);

    Task<WorkflowInstanceStatus> GetStatusAsync(string instanceId, CancellationToken ct = default);

    Task<WorkflowInstance?> GetInstanceAsync(string instanceId, CancellationToken ct = default);

    Task<WorkflowInstanceStatus> CancelAsync(string instanceId, string? reason = null, CancellationToken ct = default);

    Task<IReadOnlyList<WorkflowInstanceStatus>> ListAsync(int? top = null, CancellationToken ct = default);

    Task<WorkflowInstanceStatus> ResumeAsync(
        string instanceId,
        string transitionId,
        string? correlationKey = null,
        Dictionary<string, object?>? payload = null,
        string? resultId = null,
        CancellationToken ct = default);
}

public sealed record WorkflowInstanceStatus(
    string InstanceId,
    WorkflowStatus Status,
    string StartNodeId,
    string CurrentNodeId,
    string? EndNodeId,
    int Version,
    DateTimeOffset? LastActivityUtc,
    int ActiveWaitGroupCount);

public sealed record WorkflowTickResult(
    string InstanceId,
    bool Progressed,
    bool Moved,
    bool Suspended,
    bool Failed,
    string? NextNodeId,
    int Version,
    TimeSpan? Backoff,
    string? ErrorMessage,
    WorkflowInstanceStatus StatusProjection);
