using System.Security;
using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.Visualization;

namespace Techne.Loom.SkillOrchestrator.Visualizer;

public sealed class SvgWorkflowInstanceVisualizer : WorkflowInstanceVisualizerBase
{
    public override Task<string> VisualizeToStringAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic)
    {
        var states = instance.Nodes.Values.OfType<StateNode>().OrderBy(static state => state.Id).ToList();
        var edges = WorkflowVisualizationGraph.GetEdges(instance);
        var builder = new StringBuilder();
        builder.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1100\" height=\"700\" role=\"img\">");
        builder.AppendLine($"<title>{Escape(instance.InstanceId)} workflow</title>");
        builder.AppendLine("<rect x=\"0\" y=\"0\" width=\"1100\" height=\"700\" fill=\"#ffffff\" />");

        var y = 40;
        foreach (var state in states)
        {
            var kind = WorkflowVisualizationStyleMap.GetStateKind(instance, state, edges);
            var style = WorkflowVisualizationStyleMap.GetStyle(kind);
            var active = string.Equals(state.Id, instance.CurrentNodeId, StringComparison.Ordinal);
            var stroke = active ? "#ea580c" : style.Stroke;
            var strokeWidth = active ? 3 : 1;
            var label = style.Decorate(state.Name);
            builder.AppendLine($"<rect x=\"20\" y=\"{y}\" width=\"360\" height=\"60\" fill=\"{style.Fill}\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" data-semantic=\"{kind}\" />");
            builder.AppendLine($"<text x=\"30\" y=\"{y + 24}\" fill=\"{style.Text}\">{Escape(label)}</text>");
            builder.AppendLine($"<text x=\"30\" y=\"{y + 44}\" fill=\"{style.Text}\">Wait={Escape(state.WaitBehavior)}</text>");
            foreach (var group in state.Groups)
            {
                builder.AppendLine($"<text x=\"430\" y=\"{y + 24}\" fill=\"#334155\">Group {Escape(group.Id)}</text>");
            }

            y += 90;
        }

        var legendY = 40;
        builder.AppendLine("<g id=\"legend\"><text x=\"760\" y=\"22\" fill=\"#334155\">Legend</text>");
        foreach (var semanticKind in WorkflowVisualizationSemantics.LegendKinds)
        {
            var style = WorkflowVisualizationSemantics.GetStyle(semanticKind);
            builder.AppendLine($"<rect x=\"760\" y=\"{legendY}\" width=\"22\" height=\"22\" fill=\"{style.Fill}\" stroke=\"{style.Stroke}\" />");
            builder.AppendLine($"<text x=\"790\" y=\"{legendY + 16}\" fill=\"{style.Text}\">{Escape(style.Decorate(style.Label))}</text>");
            legendY += 30;
        }
        builder.AppendLine("</g>");
        builder.AppendLine("</svg>");
        return Task.FromResult(builder.ToString());
    }

    private static string Escape(object? value)
        => SecurityElement.Escape(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty) ?? string.Empty;
}
