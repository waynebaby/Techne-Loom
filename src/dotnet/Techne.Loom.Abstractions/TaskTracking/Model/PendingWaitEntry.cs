namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class PendingWaitEntry
{
    public string WaitId { get; init; } = string.Empty;

    public DateTimeOffset? ExpireAt { get; init; }

    public bool Completed { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public Dictionary<string, object?>? ResultContext { get; set; }

    public string? Error { get; set; }
}
