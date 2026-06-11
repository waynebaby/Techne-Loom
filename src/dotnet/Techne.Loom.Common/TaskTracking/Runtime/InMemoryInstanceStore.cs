using Techne.Loom.Abstractions.TaskTracking;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Abstractions.TaskTracking.Runtime;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class InMemoryInstanceStore : IInstanceStore
{
    private readonly Dictionary<string, WorkflowInstance> _instances = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly ISystemClock _clock;

    public InMemoryInstanceStore(ISystemClock? clock = null)
    {
        _clock = clock ?? new SystemClock();
    }

    public Task SaveNewAsync(WorkflowInstance instance, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_instances.ContainsKey(instance.InstanceId))
            {
                throw new InvalidOperationException($"Workflow instance '{instance.InstanceId}' already exists.");
            }

            instance.Version = 0;
            _instances[instance.InstanceId] = WorkflowInstanceCloner.Clone(instance);
        }

        return Task.CompletedTask;
    }

    public Task<WorkflowInstance?> GetAsync(string instanceId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_instances.TryGetValue(instanceId, out var instance)
                ? WorkflowInstanceCloner.Clone(instance)
                : null);
        }
    }

    public Task<bool> TryUpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_instances.TryGetValue(instance.InstanceId, out var current) || current.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            var clone = WorkflowInstanceCloner.Clone(instance);
            clone.Version = expectedVersion + 1;
            _instances[instance.InstanceId] = clone;
            instance.Version = clone.Version;
            return Task.FromResult(true);
        }
    }

    public Task<bool> TryAppendHistoryAsync(string instanceId, WorkflowHistoryEntry entry, int expectedVersion, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_instances.TryGetValue(instanceId, out var current) || current.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            current.History.Add(entry);
            current.Version++;
            return Task.FromResult(true);
        }
    }

    public Task<bool> TryAcquireLeaseAsync(string instanceId, string ownerId, TimeSpan ttl, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var now = _clock.UtcNow;

        lock (_gate)
        {
            if (!_instances.TryGetValue(instanceId, out var current))
            {
                return Task.FromResult(false);
            }

            if (current.LeaseExpiresUtc is { } expiresUtc && expiresUtc > now && !string.Equals(current.LeaseOwner, ownerId, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            current.LeaseOwner = ownerId;
            current.LeaseExpiresUtc = now.Add(ttl);
            return Task.FromResult(true);
        }
    }

    public Task<bool> TryRenewLeaseAsync(string instanceId, string ownerId, TimeSpan ttl, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_instances.TryGetValue(instanceId, out var current) || !string.Equals(current.LeaseOwner, ownerId, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            current.LeaseExpiresUtc = _clock.UtcNow.Add(ttl);
            return Task.FromResult(true);
        }
    }

    public Task ReleaseLeaseAsync(string instanceId, string ownerId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_instances.TryGetValue(instanceId, out var current) && string.Equals(current.LeaseOwner, ownerId, StringComparison.Ordinal))
            {
                current.LeaseOwner = null;
                current.LeaseExpiresUtc = null;
            }
        }

        return Task.CompletedTask;
    }

    public Task<WorkflowInstanceStatus?> GetStatusAsync(string instanceId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_instances.TryGetValue(instanceId, out var current))
            {
                return Task.FromResult<WorkflowInstanceStatus?>(null);
            }

            return Task.FromResult<WorkflowInstanceStatus?>(CreateStatusProjection(current));
        }
    }

    public Task<IReadOnlyList<WorkflowInstanceStatus>> ListStatusAsync(int? top = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var list = _instances.Values
                .Select(CreateStatusProjection)
                .OrderByDescending(static status => status.LastActivityUtc)
                .ThenBy(static status => status.InstanceId, StringComparer.Ordinal)
                .ToList();

            if (top is { } limit)
            {
                list = list.Take(limit).ToList();
            }

            return Task.FromResult<IReadOnlyList<WorkflowInstanceStatus>>(list);
        }
    }

    public Task<bool> TryCancelAsync(string instanceId, int expectedVersion, string? reason = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_instances.TryGetValue(instanceId, out var current) || current.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            current.Status = WorkflowStatus.Failed;
            current.Context["cancelReason"] = reason;
            current.LastActivityUtc = _clock.UtcNow;
            current.Version++;
            return Task.FromResult(true);
        }
    }

    public Task HeartbeatAsync(string instanceId, DateTimeOffset? now = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_instances.TryGetValue(instanceId, out var current))
            {
                current.LastHeartbeatUtc = now ?? _clock.UtcNow;
            }
        }

        return Task.CompletedTask;
    }

    public Task TouchActivityAsync(string instanceId, DateTimeOffset? when = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_instances.TryGetValue(instanceId, out var current))
            {
                current.LastActivityUtc = when ?? _clock.UtcNow;
            }
        }

        return Task.CompletedTask;
    }

    private static WorkflowInstanceStatus CreateStatusProjection(WorkflowInstance instance)
        => new(
            instance.InstanceId,
            instance.Status,
            instance.StartNodeId,
            instance.CurrentNodeId,
            instance.EndNodeId,
            instance.Version,
            instance.LastActivityUtc,
            instance.ActiveWaitGroups.Count);
}