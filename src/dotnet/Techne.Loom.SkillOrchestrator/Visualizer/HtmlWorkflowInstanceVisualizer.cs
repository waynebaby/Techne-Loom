using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Visualizer;

public sealed class HtmlWorkflowInstanceVisualizer : WorkflowInstanceVisualizerBase
{
    public override Task<string> VisualizeToStringAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic)
    {
        var states = instance.Nodes.Values.OfType<StateNode>().OrderBy(static node => node.Id).ToList();
        var transitions = WorkflowVisualizationGraph.GetEdges(instance);
        var builder = new StringBuilder();
        builder.AppendLine("<html><body>");
        builder.AppendLine($"<h1>Workflow {instance.InstanceId}</h1>");
        builder.AppendLine("<div class=\"wf-legend\">Legend</div>");

        foreach (var state in states)
        {
            var activeClass = string.Equals(state.Id, instance.CurrentNodeId, StringComparison.Ordinal) ? " wf-state-active" : string.Empty;
            var icon = string.Equals(state.Id, instance.CurrentNodeId, StringComparison.Ordinal) ? "🔥 " : string.Empty;
            builder.AppendLine($"<section class=\"wf-state{activeClass}\">{icon}{state.Name}</section>");
            builder.AppendLine($"<div>Wait={state.WaitBehavior}</div>");
            foreach (var group in state.Groups)
            {
                builder.AppendLine($"<div>Group {group.Id}</div>");
            }
        }

        builder.AppendLine("<h2>Transitions</h2>");
        builder.AppendLine("<table class=\"wf-transitions\"><thead><tr><th>Source</th><th>Transition</th><th>Target</th><th>Step kind</th><th>Guard</th></tr></thead><tbody>");
        foreach (var transition in transitions)
        {
            builder.AppendLine($"<tr><td>{transition.SourceStateName}</td><td>{transition.TransitionName}</td><td>{transition.TargetStateName}</td><td>{transition.StepKind}</td><td>{transition.GuardExpression}</td></tr>");
        }
        builder.AppendLine("</tbody></table>");

        foreach (var key in instance.Context.Keys.OrderBy(static key => key, StringComparer.Ordinal))
        {
            builder.AppendLine($"<div>{key}</div>");
        }

        builder.AppendLine("</body></html>");
        return Task.FromResult(builder.ToString());
    }
}
