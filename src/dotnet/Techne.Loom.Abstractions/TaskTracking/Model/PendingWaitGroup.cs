namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class PendingWaitGroup
{
    public string InstanceId { get; init; } = string.Empty;

    public string TransitionId { get; init; } = string.Empty;

    public string? ConcurrencyGroupId { get; init; }

    public List<string> ExpectedTransitionIds { get; init; } = [];

    public string? CorrelationKey { get; init; }

    public string? TargetStateId { get; init; }

    public string? TimeoutTargetStateId { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public ConcurrencyStrategy OriginStrategy { get; init; } = ConcurrencyStrategy.All;

    public bool Completed { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public bool CompletionLogged { get; set; }

    public bool TimedOut { get; set; }

    public Dictionary<string, object?> AggregatedContext { get; init; } = [];

    public List<PendingWaitEntry> Entries { get; init; } = [];

    public PendingWaitEntry AddEntry(DateTimeOffset? expireAt)
    {
        var entry = new PendingWaitEntry
        {
            WaitId = Guid.NewGuid().ToString("N"),
            ExpireAt = expireAt,
        };

        Entries.Add(entry);
        return entry;
    }

    public PendingWaitEntry? GetNextPendingEntry()
    {
        return Entries.FirstOrDefault(static item => !item.Completed);
    }

    public bool TryCompleteEntry(string waitId, Dictionary<string, object?>? payload)
    {
        var entry = Entries.FirstOrDefault(item => string.Equals(item.WaitId, waitId, StringComparison.Ordinal));
        if (entry is null || entry.Completed)
        {
            return false;
        }

        entry.Completed = true;
        entry.CompletedAt = DateTimeOffset.UtcNow;
        entry.ResultContext = payload is null ? null : new Dictionary<string, object?>(payload);

        if (payload is not null)
        {
            foreach (var pair in payload)
            {
                AggregatedContext[pair.Key] = pair.Value;
            }
        }

        if (OriginStrategy is ConcurrencyStrategy.FirstSuccess or ConcurrencyStrategy.FirstResponse)
        {
            Completed = true;
            CompletedAt = DateTimeOffset.UtcNow;
        }
        else if (OriginStrategy == ConcurrencyStrategy.All && Entries.All(static item => item.Completed))
        {
            Completed = true;
            CompletedAt = DateTimeOffset.UtcNow;
        }

        return true;
    }
}
