using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Visualizer;

internal sealed record WorkflowVisualizationEdge(
    string SourceStateId,
    string SourceStateName,
    string? SourceWorkflowPhase,
    string TransitionId,
    string TransitionName,
    string? TargetStateId,
    string TargetStateName,
    string GuardExpression,
    WorkflowStepKind StepKind,
    string? OwnedInputMode);
