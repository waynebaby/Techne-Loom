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

        return Task.FromResult(builder.ToString());
    }
}
