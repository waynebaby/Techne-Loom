namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class StateNode : ITaskNode
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? WorkflowPhase { get; set; }

    public List<TransitionGroup> Groups { get; set; } = [];

    public TimeSpan? Expiration { get; set; }

    public DateTimeOffset? EntranceTime { get; set; }

    public WaitBehavior WaitBehavior { get; init; } = WaitBehavior.BlockUntilComplete;

    public string? CorrelationKeyPath { get; init; }

    public ExpressionDefinition? StateFailedExpression { get; init; }
}
