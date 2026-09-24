using System.Net;
using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Visualizer;

public sealed class WorkflowBusinessSummaryMarkdownRenderer
{
    public string Render(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var states = instance.Nodes.Values
            .OfType<StateNode>()
            .OrderBy(static state => NormalizePhase(state.WorkflowPhase), StringComparer.Ordinal)
            .ThenBy(static state => state.Id, StringComparer.Ordinal)
            .ToList();
        var builder = new StringBuilder();
        builder.AppendLine("## Workflow Business Summary / 工作流业务说明");
        builder.AppendLine();
        builder.AppendLine("| Phase / 阶段 | State / 节点 | Node ID | Business purpose / 业务目的 |");
        builder.AppendLine("| --- | --- | --- | --- |");

        foreach (var state in states)
        {
            var phase = string.IsNullOrWhiteSpace(state.WorkflowPhase) ? "Not assigned / 未分配" : state.WorkflowPhase.Trim();
            var name = string.IsNullOrWhiteSpace(state.Name) ? state.Id : state.Name.Trim();
            var description = string.IsNullOrWhiteSpace(state.Description) ? "Not provided / 未提供" : state.Description.Trim();
            builder.Append("| ")
                .Append(EscapeCell(phase)).Append(" | ")
                .Append(EscapeCell(name)).Append(" | ")
                .Append(EscapeCell(state.Id)).Append(" | ")
                .Append(EscapeCell(description)).AppendLine(" |");
        }

        return builder.ToString().TrimEnd();
    }

    private static string NormalizePhase(string? phase)
        => string.IsNullOrWhiteSpace(phase) ? string.Empty : phase.Trim();

    private static string EscapeCell(string value)
    {
        var normalized = value.Replace('\r', ' ').Replace('\n', ' ');
        var escaped = normalized
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("!", "\\!", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("~", "\\~", StringComparison.Ordinal);
        return WebUtility.HtmlEncode(escaped);
    }
}
