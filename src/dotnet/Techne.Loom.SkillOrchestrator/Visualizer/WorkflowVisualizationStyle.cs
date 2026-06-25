using Techne.Loom.Abstractions.TaskTracking.Model;

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
}

internal sealed record WorkflowVisualizationStyle(string Fill, string Stroke, string Emoji, string Label)
{
    public string LegendLabel => string.IsNullOrWhiteSpace(Emoji) ? Label : $"{Emoji} {Label}";

    public string DecorateNodeLabel(string value)
        => string.IsNullOrWhiteSpace(Emoji) ? value : $"{Emoji} {value}";
}

internal static class WorkflowVisualizationStyleMap
{
    public static WorkflowVisualizationNodeKind GetStateKind(WorkflowInstance instance, StateNode state, IReadOnlyList<WorkflowVisualizationEdge> edges)
    {
        if (string.Equals(instance.EndNodeId, state.Id, StringComparison.Ordinal) || state.Groups.Count == 0)
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
        return kind switch
        {
            WorkflowVisualizationNodeKind.Ai => new WorkflowVisualizationStyle("#dcfce7", "#16a34a", "🔎", "AI"),
            WorkflowVisualizationNodeKind.Tool => new WorkflowVisualizationStyle("#dbeafe", "#2563eb", "⚙️", "Code/Tool"),
            WorkflowVisualizationNodeKind.Branch => new WorkflowVisualizationStyle("#fef3c7", "#a16207", "❓", "Conditional branch"),
            WorkflowVisualizationNodeKind.OptionalUserInput => new WorkflowVisualizationStyle("#fef3c7", "#d97706", "💬", "Optional user choice"),
            WorkflowVisualizationNodeKind.MandatoryUserInput => new WorkflowVisualizationStyle("#fee2e2", "#dc2626", "🚧", "Required user input"),
            WorkflowVisualizationNodeKind.Gate => new WorkflowVisualizationStyle("#f8fafc", "#94a3b8", "📜", "Gate"),
            _ => new WorkflowVisualizationStyle("#f9fafb", "#9ca3af", "", "Default"),
        };
    }

    private static WorkflowVisualizationNodeKind GetStepKind(WorkflowStepKind stepKind, string? ownedInputMode)
    {
        return stepKind switch
        {
            WorkflowStepKind.ModelThink or WorkflowStepKind.McpCall or WorkflowStepKind.SubagentCall => WorkflowVisualizationNodeKind.Ai,
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