using System.Text.Json.Serialization;
namespace Techne.Loom.Abstractions.TaskTracking.Model;

public abstract record TransitionBase : ITaskNode
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? WorkflowPhase { get; init; }

    public string? TargetNodeId { get; init; }

    public string? OutputPath { get; init; }

    public int Priority { get; init; } = 100;

    public ExpressionDefinition SucceedExpression { get; init; } = new();

    public ExpressionDefinition GuardExpression { get; init; } = new();


    [JsonIgnore]
    public bool GuardExpressionWasExplicitlyDeclared { get; set; } = true;

    [JsonIgnore]
    public bool SucceedExpressionWasExplicitlyDeclared { get; set; } = true;
    public WorkflowStepKind StepKind { get; init; } = WorkflowStepKind.ToolCall;

    public List<string>? TerminalRoutes { get; init; }

    public List<string>? BlockedRoutes { get; init; }

    public List<string>? SatisfiesGateIds { get; init; }

    public List<string>? PublishesOutputFamilies { get; init; }

    public List<string>? PublishesBlockedOutputFamilies { get; init; }

    public string? OwnedInputMode { get; init; }

}
