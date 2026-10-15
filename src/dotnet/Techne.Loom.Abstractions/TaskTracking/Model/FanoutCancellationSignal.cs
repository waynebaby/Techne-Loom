namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed record FanoutCancellationSignal(
    string SignalId,
    string InstanceId,
    string ConcurrencyGroupId,
    string TransitionId,
    string WaitId,
    bool CancellationRequested,
    bool LateResultsIgnored,
    string Reason,
    DateTimeOffset RequestedAtUtc);
