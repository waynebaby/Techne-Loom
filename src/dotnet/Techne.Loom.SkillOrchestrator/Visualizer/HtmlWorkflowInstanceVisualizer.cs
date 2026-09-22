using System.Net;
using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.Visualization;

namespace Techne.Loom.SkillOrchestrator.Visualizer;

public sealed class HtmlWorkflowInstanceVisualizer : WorkflowInstanceVisualizerBase
{
    public override Task<string> VisualizeToStringAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic)
    {
        var states = instance.Nodes.Values.OfType<StateNode>().OrderBy(static node => node.Id).ToList();
        var transitions = WorkflowVisualizationGraph.GetEdges(instance);
        var builder = new StringBuilder();
        builder.AppendLine("<html><head><meta charset=\"utf-8\"><style>.wf-legend{display:flex;gap:12px;flex-wrap:wrap}.wf-legend-item{padding:4px 8px;border:1px solid var(--stroke);background:var(--fill);color:var(--text)}.wf-state{padding:8px;margin:6px 0;border:1px solid var(--stroke);background:var(--fill);color:var(--text)}.wf-state-active{outline:3px solid #ea580c}.wf-transitions{border-collapse:collapse}.wf-transitions td,.wf-transitions th{border:1px solid #cbd5e1;padding:4px}</style></head><body>");
        builder.AppendLine($"<h1>Workflow {HtmlEncode(instance.InstanceId)}</h1>");
        builder.AppendLine("<div class=\"wf-legend\" aria-label=\"Legend\">");
        foreach (var kind in WorkflowVisualizationSemantics.LegendKinds)
        {
            var style = WorkflowVisualizationSemantics.GetStyle(kind);
            builder.AppendLine($"<span class=\"wf-legend-item\" style=\"--fill:{style.Fill};--stroke:{style.Stroke};--text:{style.Text}\">{style.Emoji} {HtmlEncode(style.Label)}</span>");
        }
        builder.AppendLine("</div>");

        foreach (var state in states)
        {
            var kind = WorkflowVisualizationStyleMap.GetStateKind(instance, state, transitions);
            var style = WorkflowVisualizationStyleMap.GetStyle(kind);
            var activeClass = string.Equals(state.Id, instance.CurrentNodeId, StringComparison.Ordinal) ? " wf-state-active" : string.Empty;
            builder.AppendLine($"<section class=\"wf-state{activeClass}\" data-semantic=\"{kind}\" style=\"--fill:{style.Fill};--stroke:{style.Stroke};--text:{style.Text}\">{style.Emoji} {HtmlEncode(state.Name)}</section>");
            builder.AppendLine($"<div>Wait={HtmlEncode(state.WaitBehavior.ToString())}</div>");
            foreach (var group in state.Groups)
            {
                builder.AppendLine($"<div>Group {HtmlEncode(group.Id)}</div>");
            }
        }

        builder.AppendLine("<h2>Transitions</h2>");
        builder.AppendLine("<table class=\"wf-transitions\"><thead><tr><th>Source</th><th>Transition</th><th>Target</th><th>Step kind</th><th>Guard</th></tr></thead><tbody>");
        foreach (var transition in transitions)
        {
            builder.AppendLine($"<tr><td>{HtmlEncode(transition.SourceStateName)}</td><td>{HtmlEncode(transition.TransitionName)}</td><td>{HtmlEncode(transition.TargetStateName)}</td><td>{HtmlEncode(transition.StepKind.ToString())}</td><td>{HtmlEncode(transition.GuardExpression)}</td></tr>");
        }
        builder.AppendLine("</tbody></table>");

        foreach (var key in instance.Context.Keys.OrderBy(static key => key, StringComparer.Ordinal))
        {
            builder.AppendLine($"<div>{HtmlEncode(key)}</div>");
        }

        builder.AppendLine("</body></html>");
        return Task.FromResult(builder.ToString());
    }

    private static string HtmlEncode(object? value)
        => WebUtility.HtmlEncode(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
}
