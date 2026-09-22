using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.Visualization;

namespace Techne.Loom.SkillOrchestrator.Visualizer;

internal enum WorkflowVisualizationNodeKind
{
    Default,
    Ai,
    Tool,
    Branch,
    OptionalUserInput,
    MandatoryUserInput,
    Gate,
    Completion,
}

internal sealed record WorkflowVisualizationStyle(string Fill, string Stroke, string Emoji, string Label, string Text)
{
    public string LegendLabel => string.IsNullOrWhiteSpace(Emoji) ? Label : $"{Emoji} {Label}";

    public string Decorate(string value)
        => string.IsNullOrWhiteSpace(Emoji) ? value : $"{Emoji} {value}";

    public string DecorateNodeLabel(string value)
        => Decorate(value);
}

internal static class WorkflowVisualizationStyleMap
{
    public static WorkflowVisualizationNodeKind GetStateKind(WorkflowInstance instance, StateNode state, IReadOnlyList<WorkflowVisualizationEdge> edges)
    {
        if (string.Equals(instance.EndNodeId, state.Id, StringComparison.Ordinal))
        {
            return WorkflowVisualizationNodeKind.Completion;
        }

        if (state.Groups.Count == 0)
        {
            return WorkflowVisualizationNodeKind.Gate;
        }

        var outgoingKinds = edges
            .Where(edge => string.Equals(edge.SourceStateId, state.Id, StringComparison.Ordinal))
            .Select(static edge => GetStepKind(edge.StepKind, edge.OwnedInputMode))
            .ToList();

        if (outgoingKinds.Contains(WorkflowVisualizationNodeKind.MandatoryUserInput))
        {
            return WorkflowVisualizationNodeKind.MandatoryUserInput;
        }

        if (outgoingKinds.Contains(WorkflowVisualizationNodeKind.OptionalUserInput))
        {
            return WorkflowVisualizationNodeKind.OptionalUserInput;
        }

        if (outgoingKinds.Contains(WorkflowVisualizationNodeKind.Branch))
        {
            return WorkflowVisualizationNodeKind.Branch;
        }

        if (outgoingKinds.Contains(WorkflowVisualizationNodeKind.Tool))
        {
            return WorkflowVisualizationNodeKind.Tool;
        }

        if (outgoingKinds.Contains(WorkflowVisualizationNodeKind.Ai))
        {
            return WorkflowVisualizationNodeKind.Ai;
        }

        if (outgoingKinds.Contains(WorkflowVisualizationNodeKind.Gate))
        {
            return WorkflowVisualizationNodeKind.Gate;
        }

        return WorkflowVisualizationNodeKind.Default;
    }

    public static WorkflowVisualizationStyle GetStyle(WorkflowVisualizationNodeKind kind)
    {
        var style = WorkflowVisualizationSemantics.GetStyle(ToSemanticKind(kind));
        return new WorkflowVisualizationStyle(style.Fill, style.Stroke, style.Emoji, GetLocalLabel(kind, style.Label), style.Text);
    }

    private static WorkflowVisualizationSemanticKind ToSemanticKind(WorkflowVisualizationNodeKind kind)
    {
        return kind switch
        {
            WorkflowVisualizationNodeKind.Ai => WorkflowVisualizationSemanticKind.Research,
            WorkflowVisualizationNodeKind.Tool => WorkflowVisualizationSemanticKind.Runtime,
            WorkflowVisualizationNodeKind.Branch => WorkflowVisualizationSemanticKind.Decision,
            WorkflowVisualizationNodeKind.OptionalUserInput => WorkflowVisualizationSemanticKind.OptionalChoice,
            WorkflowVisualizationNodeKind.MandatoryUserInput => WorkflowVisualizationSemanticKind.RequiredInput,
            WorkflowVisualizationNodeKind.Gate => WorkflowVisualizationSemanticKind.Contract,
            WorkflowVisualizationNodeKind.Completion => WorkflowVisualizationSemanticKind.Completion,
            _ => WorkflowVisualizationSemanticKind.Default,
        };
    }

    private static string GetLocalLabel(WorkflowVisualizationNodeKind kind, string fallback)
    {
        return kind switch
        {
            WorkflowVisualizationNodeKind.Ai => "AI",
            WorkflowVisualizationNodeKind.Tool => "Code/Tool",
            WorkflowVisualizationNodeKind.Branch => "Conditional branch",
            WorkflowVisualizationNodeKind.OptionalUserInput => "Optional user choice",
            WorkflowVisualizationNodeKind.MandatoryUserInput => "Required user input",
            WorkflowVisualizationNodeKind.Gate => "Gate",
            WorkflowVisualizationNodeKind.Completion => "Completion",
            _ => fallback,
        };
    }

    private static WorkflowVisualizationNodeKind GetStepKind(WorkflowStepKind stepKind, string? ownedInputMode)
    {
        return stepKind switch
        {
            WorkflowStepKind.ModelThink or WorkflowStepKind.Plan or WorkflowStepKind.McpCall or WorkflowStepKind.SubagentCall => WorkflowVisualizationNodeKind.Ai,
            WorkflowStepKind.ToolCall => WorkflowVisualizationNodeKind.Tool,
            WorkflowStepKind.ConditionBranch => string.Equals(ownedInputMode, "user", StringComparison.OrdinalIgnoreCase)
                ? WorkflowVisualizationNodeKind.OptionalUserInput
                : WorkflowVisualizationNodeKind.Branch,
            WorkflowStepKind.AskUser => WorkflowVisualizationNodeKind.MandatoryUserInput,
            WorkflowStepKind.StateUpdate or WorkflowStepKind.ArtifactEmit or WorkflowStepKind.MemoryRead or WorkflowStepKind.MemoryWrite or WorkflowStepKind.WaitResume => WorkflowVisualizationNodeKind.Gate,
            _ => WorkflowVisualizationNodeKind.Default,
        };
    }
}