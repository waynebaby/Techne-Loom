using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Abstractions.TaskTracking.Runtime;

public interface IInstanceStore
{
    Task SaveNewAsync(WorkflowInstance instance, CancellationToken ct = default);

    Task<WorkflowInstance?> GetAsync(string instanceId, CancellationToken ct = default);

    Task<bool> TryUpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default);

    Task<bool> TryAppendHistoryAsync(string instanceId, WorkflowHistoryEntry entry, int expectedVersion, CancellationToken ct = default);

    Task<bool> TryAcquireLeaseAsync(string instanceId, string ownerId, TimeSpan ttl, CancellationToken ct = default);

    Task<bool> TryRenewLeaseAsync(string instanceId, string ownerId, TimeSpan ttl, CancellationToken ct = default);

    Task ReleaseLeaseAsync(string instanceId, string ownerId, CancellationToken ct = default);

    Task<WorkflowInstanceStatus?> GetStatusAsync(string instanceId, CancellationToken ct = default);

    Task<IReadOnlyList<WorkflowInstanceStatus>> ListStatusAsync(int? top = null, CancellationToken ct = default);

    Task<bool> TryCancelAsync(string instanceId, int expectedVersion, string? reason = null, CancellationToken ct = default);

    Task HeartbeatAsync(string instanceId, DateTimeOffset? now = null, CancellationToken ct = default);

    Task TouchActivityAsync(string instanceId, DateTimeOffset? when = null, CancellationToken ct = default);
}
