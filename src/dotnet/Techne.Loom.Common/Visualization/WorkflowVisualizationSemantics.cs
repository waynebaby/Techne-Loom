namespace Techne.Loom.Common.Visualization;

public enum WorkflowVisualizationSemanticKind
{
    Default,
    Intake,
    Research,
    Runtime,
    Decision,
    OptionalChoice,
    RequiredInput,
    Blocked,
    Contract,
    Evidence,
    Completion,
}

public sealed record WorkflowVisualizationSemanticStyle(
    string Fill,
    string Stroke,
    string Text,
    string Emoji,
    string Label)
{
    public string Decorate(string value)
        => string.IsNullOrWhiteSpace(Emoji) ? value : $"{Emoji} {value}";
}

public static class WorkflowVisualizationSemantics
{
    public static IReadOnlyList<WorkflowVisualizationSemanticKind> LegendKinds { get; } =
    [
        WorkflowVisualizationSemanticKind.Intake,
        WorkflowVisualizationSemanticKind.Research,
        WorkflowVisualizationSemanticKind.Runtime,
        WorkflowVisualizationSemanticKind.Decision,
        WorkflowVisualizationSemanticKind.OptionalChoice,
        WorkflowVisualizationSemanticKind.RequiredInput,
        WorkflowVisualizationSemanticKind.Blocked,
        WorkflowVisualizationSemanticKind.Contract,
        WorkflowVisualizationSemanticKind.Evidence,
        WorkflowVisualizationSemanticKind.Completion,
    ];

    public static WorkflowVisualizationSemanticStyle GetStyle(WorkflowVisualizationSemanticKind kind)
    {
        return kind switch
        {
            WorkflowVisualizationSemanticKind.Intake => new("#e0f2fe", "#0284c7", "#0c4a6e", "🧭", "Intake / navigation"),
            WorkflowVisualizationSemanticKind.Research => new("#dcfce7", "#16a34a", "#14532d", "🔎", "Research / planning"),
            WorkflowVisualizationSemanticKind.Runtime => new("#dbeafe", "#2563eb", "#1e3a8a", "⚙️", "Runtime / tool"),
            WorkflowVisualizationSemanticKind.Decision => new("#fef3c7", "#a16207", "#713f12", "❓", "Decision / branch"),
            WorkflowVisualizationSemanticKind.OptionalChoice => new("#fef3c7", "#d97706", "#78350f", "💬", "Optional user choice"),
            WorkflowVisualizationSemanticKind.RequiredInput => new("#fee2e2", "#dc2626", "#7f1d1d", "🚧", "Required user input"),
            WorkflowVisualizationSemanticKind.Blocked => new("#fee2e2", "#dc2626", "#7f1d1d", "🚧", "Blocked / boundary"),
            WorkflowVisualizationSemanticKind.Contract => new("#f8fafc", "#94a3b8", "#334155", "📜", "Contract / gate"),
            WorkflowVisualizationSemanticKind.Evidence => new("#ede9fe", "#7c3aed", "#4c1d95", "🧾", "Evidence"),
            WorkflowVisualizationSemanticKind.Completion => new("#dcfce7", "#15803d", "#14532d", "✅", "Completion"),
            _ => new("#f9fafb", "#9ca3af", "#374151", "", "Default"),
        };
    }
}
