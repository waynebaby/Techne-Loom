using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Visualizer;

public sealed class AsciiArtWorkflowInstanceVisualizer : WorkflowInstanceVisualizerBase
{
    public override Task<string> VisualizeToStringAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Workflow {instance.InstanceId}");

        foreach (var state in instance.Nodes.Values.OfType<StateNode>().OrderBy(static state => state.Id))
        {
            var prefix = string.Equals(state.Id, instance.CurrentNodeId, StringComparison.Ordinal) ? "🔥 " : string.Empty;
            builder.AppendLine($"{prefix}State {state.Name}");
            builder.AppendLine($"Wait: {state.WaitBehavior}");
            foreach (var group in state.Groups)
            {
                builder.AppendLine($"Group {group.Id}");
            }
        }

        foreach (var transition in instance.Nodes.Values.OfType<TransitionBase>().OrderBy(static transition => transition.Id))
        {
            builder.AppendLine($"-> [{transition.GetType().Name.Replace("Transition", string.Empty)}]");
            builder.AppendLine($"Guard: {transition.GuardExpression}");
        }

        builder.AppendLine("Recent History:");
        foreach (var entry in instance.History.TakeLast(5))
        {
            builder.AppendLine($"- {entry.Message}");
        }

        return Task.FromResult(builder.ToString());
    }
}
