namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed record CommandTransition : TransitionBase
{
    public CommandInvocation Command { get; init; } = new();

    public PlanStepContract? Plan { get; init; }

    public TimeSpan? ExecutionTimeout { get; init; }

    public int CurrentRetryCount { get; set; }

    public int MaxRetry { get; set; } = 10;
}
