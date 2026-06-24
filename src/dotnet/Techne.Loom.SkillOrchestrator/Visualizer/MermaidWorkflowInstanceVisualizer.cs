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
        var phaseGroups = states
            .GroupBy(static state => NormalizePhase(state.WorkflowPhase), StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .ToList();
        var phaseGroupIds = BuildPhaseGroupIds(phaseGroups.Select(static group => group.Key));

        foreach (var phaseGroup in phaseGroups)
        {
            if (!string.IsNullOrWhiteSpace(phaseGroup.Key))
            {
                builder.AppendLine($"    subgraph {phaseGroupIds[phaseGroup.Key]}[\"{EscapeLabel(phaseGroup.Key)}\"]");
            }

            foreach (var state in phaseGroup)
            {
                builder.AppendLine($"    {state.Id}[\"{EscapeLabel(state.Name)}\"]");
            }

            if (!string.IsNullOrWhiteSpace(phaseGroup.Key))
            {
                builder.AppendLine("    end");
            }
        }

        foreach (var edge in edges)
        {
            if (!string.IsNullOrWhiteSpace(edge.TargetStateId))
            {
                builder.AppendLine($"    {edge.SourceStateId} -->|{EscapeLabel(edge.TransitionName)}| {edge.TargetStateId}");
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

    private static string NormalizePhase(string? phase)
        => string.IsNullOrWhiteSpace(phase) ? string.Empty : phase.Trim();

    private static IReadOnlyDictionary<string, string> BuildPhaseGroupIds(IEnumerable<string> phases)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var phase in phases)
        {
            var baseId = BuildPhaseGroupIdBase(phase);
            if (counts.TryGetValue(baseId, out var collisionCount))
            {
                collisionCount++;
                counts[baseId] = collisionCount;
                ids[phase] = $"{baseId}_{collisionCount}";
                continue;
            }

            counts[baseId] = 0;
            ids[phase] = baseId;
        }

        return ids;
    }

    private static string BuildPhaseGroupIdBase(string phase)
    {
        var builder = new StringBuilder("phase_");
        foreach (var ch in phase)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');
        }

        return builder.ToString();
    }

    private static string EscapeLabel(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
