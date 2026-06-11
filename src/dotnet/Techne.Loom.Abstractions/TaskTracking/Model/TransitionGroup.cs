namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class TransitionGroup
{
    public string Id { get; set; } = string.Empty;

    public ConcurrencyStrategy Strategy { get; set; } = ConcurrencyStrategy.FirstSuccess;

    public TimeSpan? GroupTimeout { get; set; }

    public bool CancelLosers { get; set; } = true;

    public TransitionBase? TimeoutTransition { get; set; }

    public string? TimeoutTargetStateId { get; set; }

    public List<string> TransitionIds { get; set; } = [];
}
