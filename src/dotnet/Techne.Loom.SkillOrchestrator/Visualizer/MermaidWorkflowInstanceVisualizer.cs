using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Visualizer;

public sealed class MermaidWorkflowInstanceVisualizer : WorkflowInstanceVisualizerBase
{
    public override Task<string> VisualizeToStringAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic)
    {
        var builder = new StringBuilder();
        builder.AppendLine("flowchart TD");
        var states = instance.Nodes.Values.OfType<StateNode>().OrderBy(static state => state.Id, StringComparer.Ordinal).ToList();
        var edges = WorkflowVisualizationGraph.GetEdges(instance);

        foreach (var state in states)
        {
            builder.AppendLine($"    {state.Id}[\"{state.Name}\"]");
        }

        foreach (var edge in edges)
        {
            if (!string.IsNullOrWhiteSpace(edge.TargetStateId))
            {
                builder.AppendLine($"    {edge.SourceStateId} -->|{edge.TransitionName}| {edge.TargetStateId}");
            }
        }

        foreach (var state in states)
        {
            var style = WorkflowVisualizationStyleMap.GetStyle(WorkflowVisualizationStyleMap.GetStateKind(instance, state, edges));
            builder.AppendLine($"    style {state.Id} fill:{style.Fill},stroke:{style.Stroke},stroke-width:1px");
        }

        if (!string.IsNullOrWhiteSpace(instance.CurrentNodeId))
        {
            builder.AppendLine($"    style {instance.CurrentNodeId} stroke:#ea580c,stroke-width:3px");
        }

        AppendLegend(builder);

        return Task.FromResult(builder.ToString());
    }

    private static void AppendLegend(StringBuilder builder)
    {
        builder.AppendLine("    subgraph legend[Legend]");
        AppendLegendNode(builder, "legend_ai", WorkflowVisualizationNodeKind.Ai);
        AppendLegendNode(builder, "legend_tool", WorkflowVisualizationNodeKind.Tool);
        AppendLegendNode(builder, "legend_branch", WorkflowVisualizationNodeKind.Branch);
        AppendLegendNode(builder, "legend_optional", WorkflowVisualizationNodeKind.OptionalUserInput);
        AppendLegendNode(builder, "legend_required", WorkflowVisualizationNodeKind.MandatoryUserInput);
        AppendLegendNode(builder, "legend_gate", WorkflowVisualizationNodeKind.Gate);
        builder.AppendLine("    end");
    }

    private static void AppendLegendNode(StringBuilder builder, string nodeId, WorkflowVisualizationNodeKind kind)
    {
        var style = WorkflowVisualizationStyleMap.GetStyle(kind);
        builder.AppendLine($"        {nodeId}[\"{style.Label}\"]");
        builder.AppendLine($"    style {nodeId} fill:{style.Fill},stroke:{style.Stroke},stroke-width:1px");
    }
}
