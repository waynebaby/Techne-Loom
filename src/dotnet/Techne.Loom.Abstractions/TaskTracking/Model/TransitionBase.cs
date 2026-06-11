namespace Techne.Loom.Abstractions.TaskTracking.Model;

public abstract record TransitionBase : ITaskNode
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? TargetNodeId { get; init; }

    public string? OutputPath { get; init; }

    public int Priority { get; init; } = 100;

    public string SucceedExpression { get; init; } = "true";

    public string GuardExpression { get; init; } = "true";

    public WorkflowStepKind StepKind { get; init; } = WorkflowStepKind.ToolCall;
}
