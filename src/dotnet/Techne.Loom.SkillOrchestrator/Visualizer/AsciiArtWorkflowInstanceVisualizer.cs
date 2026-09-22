using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.Visualization;

namespace Techne.Loom.SkillOrchestrator.Visualizer;

public sealed class AsciiArtWorkflowInstanceVisualizer : WorkflowInstanceVisualizerBase
{
    public override Task<string> VisualizeToStringAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic)
    {
        var states = instance.Nodes.Values.OfType<StateNode>().OrderBy(static state => state.Id).ToList();
        var edges = WorkflowVisualizationGraph.GetEdges(instance);
        var builder = new StringBuilder();
        builder.AppendLine($"Workflow {instance.InstanceId}");
        builder.AppendLine("Legend:");
        foreach (var semanticKind in WorkflowVisualizationSemantics.LegendKinds)
        {
            var style = WorkflowVisualizationSemantics.GetStyle(semanticKind);
            builder.AppendLine($"  {style.Label}");
        }

        foreach (var state in states)
        {
            var kind = WorkflowVisualizationStyleMap.GetStateKind(instance, state, edges);
            var style = WorkflowVisualizationStyleMap.GetStyle(kind);
            var currentPrefix = string.Equals(state.Id, instance.CurrentNodeId, StringComparison.Ordinal) ? "* " : "  ";
            builder.AppendLine($"{currentPrefix}{style.Label}: State {state.Name} [{kind}]");
            builder.AppendLine($"  Wait: {state.WaitBehavior}");
            foreach (var group in state.Groups)
            {
                builder.AppendLine($"  Group {group.Id}");
            }
        }

        builder.AppendLine("Transitions:");
        foreach (var transition in edges.OrderBy(static edge => edge.TransitionId, StringComparer.Ordinal))
        {
            builder.AppendLine($"  -> {transition.SourceStateName} -[{transition.TransitionName}]-> {transition.TargetStateName}");
            builder.AppendLine($"     Step: {transition.StepKind}; Guard: {transition.GuardExpression}");
        }

        builder.AppendLine("Recent History:");
        foreach (var entry in instance.History.TakeLast(5))
        {
            builder.AppendLine($"- {entry.Message}");
        }

        return Task.FromResult(builder.ToString());
    }
}
