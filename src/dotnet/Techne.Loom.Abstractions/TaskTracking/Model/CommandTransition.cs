namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed record CommandTransition : TransitionBase
{
    public CommandInvocation Command { get; init; } = new();

    public PlanStepContract? Plan { get; init; }

    public UserInputContract? UserInput { get; init; }

    public TimeSpan? ExecutionTimeout { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public int MaxRetry { get => MaxRetries; set => MaxRetries = value; }

    public int CurrentRetryCount { get; set; }

    public int MaxRetries { get; set; } = 10;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool LegacyMaxRetryWasDeclared { get; set; }
}
