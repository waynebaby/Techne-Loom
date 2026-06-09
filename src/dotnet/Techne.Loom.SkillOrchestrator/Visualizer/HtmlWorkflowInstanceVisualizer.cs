using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Visualizer;

public sealed class HtmlWorkflowInstanceVisualizer : WorkflowInstanceVisualizerBase
{
    public override Task<string> VisualizeToStringAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic)
    {
        var states = instance.Nodes.Values.OfType<StateNode>().OrderBy(static node => node.Id).ToList();
        var transitions = instance.Nodes.Values.OfType<TransitionBase>().OrderBy(static node => node.Id).ToList();
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

        foreach (var transition in transitions)
        {
            builder.AppendLine($"<div><strong>Guard:</strong> {transition.GuardExpression}</div>");
            if (transition is CommandTransition commandTransition)
            {
                builder.AppendLine($"<div>RetryCount {commandTransition.CurrentRetryCount}</div>");
            }
        }

        foreach (var key in instance.Context.Keys.OrderBy(static key => key, StringComparer.Ordinal))
        {
            builder.AppendLine($"<div>{key}</div>");
        }

        builder.AppendLine("</body></html>");
        return Task.FromResult(builder.ToString());
    }
}
