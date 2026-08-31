using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Abstractions.TaskTracking.Runtime;

public interface ITaskTrackingEngine
{
    IInstanceStore InstanceStore { get; set; }

    Task<WorkflowInstance?> GenerateWorkflowAsync(string workflowUserDescription, CancellationToken ct = default);

    Task<EngineTickOutcome> TickAsync(WorkflowInstance instance, CancellationToken ct = default);

    Task ResumeAsync(
        WorkflowInstance instance,
        string transitionId,
        string? correlationKey,
        Dictionary<string, object?>? payload,
        string? resultId = null,
        CancellationToken ct = default);
}
