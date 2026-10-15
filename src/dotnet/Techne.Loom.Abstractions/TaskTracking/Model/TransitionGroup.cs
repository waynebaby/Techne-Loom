namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class TransitionGroup
{
    public string Id { get; set; } = string.Empty;

    public ConcurrencyStrategy Strategy { get; set; } = ConcurrencyStrategy.FirstPass;

    public ExpressionDefinition? PassExpression { get; set; }

    public TimeSpan? GroupTimeout { get; set; }

    public bool CancelLosers { get; set; } = true;

    public TransitionBase? TimeoutTransition { get; set; }

    public string? TimeoutTargetStateId { get; set; }

    public string? JoinTargetStateId { get; set; }

    public string? NoPassTargetStateId { get; set; }

    public string? ErrorHandlingTargetStateId { get; set; }

    public List<string> TransitionIds { get; set; } = [];
}
