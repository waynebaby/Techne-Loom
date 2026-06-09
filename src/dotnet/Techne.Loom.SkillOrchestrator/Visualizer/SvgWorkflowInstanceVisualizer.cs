using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Visualizer;

public sealed class SvgWorkflowInstanceVisualizer : WorkflowInstanceVisualizerBase
{
    public override Task<string> VisualizeToStringAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"900\" height=\"600\">");
        builder.AppendLine("<rect x=\"0\" y=\"0\" width=\"900\" height=\"600\" fill=\"#ffffff\" />");

        var y = 40;
        foreach (var state in instance.Nodes.Values.OfType<StateNode>().OrderBy(static state => state.Id))
        {
            var fill = string.Equals(state.Id, instance.CurrentNodeId, StringComparison.Ordinal) ? "#fff7ed" : "#f8fafc";
            var label = string.Equals(state.Id, instance.CurrentNodeId, StringComparison.Ordinal) ? $"🔥 State {state.Name}" : $"State {state.Name}";
            builder.AppendLine($"<rect x=\"20\" y=\"{y}\" width=\"260\" height=\"60\" fill=\"{fill}\" stroke=\"#1f2937\" />");
            builder.AppendLine($"<text x=\"30\" y=\"{y + 24}\">{System.Security.SecurityElement.Escape(label)}</text>");
            builder.AppendLine($"<text x=\"30\" y=\"{y + 44}\">Wait={state.WaitBehavior}</text>");

            foreach (var group in state.Groups)
            {
                builder.AppendLine($"<text x=\"320\" y=\"{y + 24}\">Group {group.Id}</text>");
            }

            y += 90;
        }

        builder.AppendLine("</svg>");
        return Task.FromResult(builder.ToString());
    }
}
