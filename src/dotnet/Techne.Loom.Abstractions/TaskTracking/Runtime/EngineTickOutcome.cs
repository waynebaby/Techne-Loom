namespace Techne.Loom.Abstractions.TaskTracking.Runtime;

public sealed record EngineTickOutcome(
    bool Progressed,
    bool Moved,
    bool Suspended,
    bool Failed,
    string? NextNodeId,
    TimeSpan? Backoff,
    string? ErrorMessage)
{
    public static EngineTickOutcome NoProgress(string? nextNodeId = null, TimeSpan? backoff = null)
    {
        return new EngineTickOutcome(false, false, false, false, nextNodeId, backoff, null);
    }

    public static EngineTickOutcome ProgressedTo(string? nextNodeId)
    {
        return new EngineTickOutcome(true, true, false, false, nextNodeId, null, null);
    }

    public static EngineTickOutcome SuspendedAt(string? nextNodeId)
    {
        return new EngineTickOutcome(false, false, true, false, nextNodeId, null, null);
    }

    public static EngineTickOutcome FailedWith(string errorMessage)
    {
        return new EngineTickOutcome(false, false, false, true, null, null, errorMessage);
    }
}
