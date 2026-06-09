using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Visualizer;

public sealed class MermaidWorkflowInstanceVisualizer : WorkflowInstanceVisualizerBase
{
    public override Task<string> VisualizeToStringAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic)
    {
        var builder = new StringBuilder();
        builder.AppendLine("flowchart TD");

        foreach (var state in instance.Nodes.Values.OfType<StateNode>().OrderBy(static state => state.Id))
        {
            builder.AppendLine($"    {state.Id}[\"{state.Name}\"]");
        }

        foreach (var transition in instance.Nodes.Values.OfType<TransitionBase>().OrderBy(static transition => transition.Id))
        {
            if (!string.IsNullOrWhiteSpace(transition.TargetNodeId))
            {
                builder.AppendLine($"    {instance.StartNodeId} -->|{transition.Name}| {transition.TargetNodeId}");
            }
        }

        if (!string.IsNullOrWhiteSpace(instance.CurrentNodeId))
        {
            builder.AppendLine($"    style {instance.CurrentNodeId} fill:#fff7ed,stroke:#ea580c,stroke-width:3px");
        }

        return Task.FromResult(builder.ToString());
    }
}
