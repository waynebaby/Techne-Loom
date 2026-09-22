using System.Net;
using System.Security;
using System.Text;
using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.Common.Visualization;

namespace Techne.Loom.AgentOrchestrator.Runtime;

internal static class AoWorkflowSnapshotVisualizer
{
    public static string RenderMermaid(AoWorkflowSnapshot snapshot)
    {
        var builder = new StringBuilder();
        var currentNodeId = SanitizeNodeId(snapshot.CurrentNodeId);
        var currentStyle = WorkflowVisualizationSemantics.GetStyle(GetCurrentSemanticKind(snapshot));
        var startStyle = WorkflowVisualizationSemantics.GetStyle(WorkflowVisualizationSemanticKind.Intake);
        builder.AppendLine("flowchart TD");
        var startLabel = startStyle.Decorate("state.start");
        builder.AppendLine($"    state_start[\"{startLabel}\"]");

        if (!string.Equals(currentNodeId, "state_start", StringComparison.Ordinal))
        {
            builder.AppendLine($"    {currentNodeId}[\"{currentStyle.Decorate(snapshot.CurrentNodeId)}\"]");
        }

        if (string.Equals(snapshot.Status, "completed", StringComparison.Ordinal))
        {
            var completedStyle = WorkflowVisualizationSemantics.GetStyle(WorkflowVisualizationSemanticKind.Completion);
            var completedNodeId = string.Equals(currentNodeId, "state_completed", StringComparison.Ordinal) ? "state_completed" : "state_completed";
            var completedLabel = completedStyle.Decorate("state.completed");
            builder.AppendLine($"    {completedNodeId}[\"{completedLabel}\"]");
            if (!string.Equals(currentNodeId, completedNodeId, StringComparison.Ordinal))
            {
                builder.AppendLine($"    {currentNodeId} --> {completedNodeId}");
            }
            builder.AppendLine($"    style {completedNodeId} fill:{completedStyle.Fill},stroke:{completedStyle.Stroke},stroke-width:3px");
        }
        else if (string.Equals(currentNodeId, "state_start", StringComparison.Ordinal))
        {
            builder.AppendLine($"    style state_start fill:{currentStyle.Fill},stroke:#ea580c,stroke-width:3px");
        }
        else
        {
            builder.AppendLine($"    state_start -->|{EscapeLabel(snapshot.LastTransitionId ?? "transition.pending")}| {currentNodeId}");
            builder.AppendLine($"    style {currentNodeId} fill:{currentStyle.Fill},stroke:#ea580c,stroke-width:3px");
        }

        if (!string.Equals(currentNodeId, "state_start", StringComparison.Ordinal)
            && !string.Equals(snapshot.Status, "completed", StringComparison.Ordinal))
        {
            builder.AppendLine($"    style state_start fill:{startStyle.Fill},stroke:{startStyle.Stroke},stroke-width:1px");
        }

        AppendLegend(builder);
        return builder.ToString();
    }

    public static string RenderHtml(AoWorkflowSnapshot snapshot)
    {
        var style = WorkflowVisualizationSemantics.GetStyle(GetCurrentSemanticKind(snapshot));
        var builder = new StringBuilder();
        builder.AppendLine("<html><head><meta charset=\"utf-8\"><style>.ao-legend{display:flex;gap:12px;flex-wrap:wrap}.ao-legend-item{padding:4px 8px;border:1px solid var(--stroke);background:var(--fill);color:var(--text)}.ao-current{padding:8px;border:3px solid #ea580c;background:var(--fill);color:var(--text)}</style></head><body>");
        builder.AppendLine($"<h1>AO Workflow {HtmlEncode(snapshot.CurrentNodeId)}</h1>");
        builder.AppendLine("<div class=\"ao-legend\" aria-label=\"Legend\">");
        foreach (var kind in WorkflowVisualizationSemantics.LegendKinds)
        {
            var legendStyle = WorkflowVisualizationSemantics.GetStyle(kind);
            builder.AppendLine($"<span class=\"ao-legend-item\" style=\"--fill:{legendStyle.Fill};--stroke:{legendStyle.Stroke};--text:{legendStyle.Text}\">{legendStyle.Emoji} {HtmlEncode(legendStyle.Label)}</span>");
        }
        builder.AppendLine("</div>");
        builder.AppendLine($"<div class=\"ao-current\" style=\"--fill:{style.Fill}\">{style.Emoji} {HtmlEncode(snapshot.CurrentNodeId)}</div>");
        builder.AppendLine($"<div><strong>Status:</strong> {HtmlEncode(snapshot.Status)}</div>");
        builder.AppendLine($"<div><strong>Current node:</strong> {HtmlEncode(snapshot.CurrentNodeId)}</div>");
        if (!string.IsNullOrWhiteSpace(snapshot.LastBoundaryReason))
        {
            builder.AppendLine($"<div><strong>Boundary reason:</strong> {HtmlEncode(snapshot.LastBoundaryReason)}</div>");
        }

        AppendList(builder, "Pending requirements", snapshot.PendingRequirements);
        AppendList(builder, "Next frontier", snapshot.NextFrontier);
        builder.AppendLine("<h2>Context keys</h2><ul>");
        foreach (var key in snapshot.Context.Keys.OrderBy(static item => item, StringComparer.Ordinal))
        {
            builder.AppendLine($"<li>{HtmlEncode(key)}</li>");
        }
        builder.AppendLine("</ul>");
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    public static string RenderSvg(AoWorkflowSnapshot snapshot)
    {
        var style = WorkflowVisualizationSemantics.GetStyle(GetCurrentSemanticKind(snapshot));
        var builder = new StringBuilder();
        builder.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1000\" height=\"520\" role=\"img\">");
        builder.AppendLine($"<title>{Escape(snapshot.CurrentNodeId)} AO workflow</title>");
        builder.AppendLine("<rect x=\"0\" y=\"0\" width=\"1000\" height=\"520\" fill=\"#ffffff\" />");
        builder.AppendLine($"<rect x=\"30\" y=\"40\" width=\"420\" height=\"90\" fill=\"{style.Fill}\" stroke=\"#ea580c\" stroke-width=\"3\" />");
        builder.AppendLine($"<text x=\"48\" y=\"78\" fill=\"{style.Text}\">{Escape(style.Decorate(snapshot.CurrentNodeId))}</text>");
        builder.AppendLine($"<text x=\"48\" y=\"108\" fill=\"{style.Text}\">Status: {Escape(snapshot.Status)}</text>");
        var y = 180;
        builder.AppendLine("<g id=\"legend\"><text x=\"30\" y=\"160\" fill=\"#334155\">Legend</text>");
        foreach (var kind in WorkflowVisualizationSemantics.LegendKinds)
        {
            var legendStyle = WorkflowVisualizationSemantics.GetStyle(kind);
            builder.AppendLine($"<rect x=\"30\" y=\"{y}\" width=\"22\" height=\"22\" fill=\"{legendStyle.Fill}\" stroke=\"{legendStyle.Stroke}\" />");
            builder.AppendLine($"<text x=\"60\" y=\"{y + 16}\" fill=\"{legendStyle.Text}\">{Escape(legendStyle.Decorate(legendStyle.Label))}</text>");
            y += 30;
        }
        builder.AppendLine("</g></svg>");
        return builder.ToString();
    }

    public static string RenderAscii(AoWorkflowSnapshot snapshot)
    {
        var style = WorkflowVisualizationSemantics.GetStyle(GetCurrentSemanticKind(snapshot));
        var builder = new StringBuilder();
        builder.AppendLine($"AO Workflow {snapshot.CurrentNodeId}");
        builder.AppendLine($"{style.Label}: {snapshot.CurrentNodeId} [{snapshot.Status}]");
        builder.AppendLine("Legend:");
        foreach (var kind in WorkflowVisualizationSemantics.LegendKinds)
        {
            var legendStyle = WorkflowVisualizationSemantics.GetStyle(kind);
            builder.AppendLine($"  {legendStyle.Label}");
        }
        AppendAsciiList(builder, "Pending requirements", snapshot.PendingRequirements);
        AppendAsciiList(builder, "Next frontier", snapshot.NextFrontier);
        builder.AppendLine("Context keys:");
        foreach (var key in snapshot.Context.Keys.OrderBy(static item => item, StringComparer.Ordinal))
        {
            builder.AppendLine($"  - {key}");
        }
        return builder.ToString();
    }

    private static void AppendLegend(StringBuilder builder)
    {
        builder.AppendLine("    subgraph legend[\"Legend\"]");
        foreach (var kind in WorkflowVisualizationSemantics.LegendKinds)
        {
            var style = WorkflowVisualizationSemantics.GetStyle(kind);
            var nodeId = $"legend_{kind.ToString().ToLowerInvariant()}";
            builder.AppendLine($"        {nodeId}[\"{EscapeLabel(style.Decorate(style.Label))}\"]");
            builder.AppendLine($"    style {nodeId} fill:{style.Fill},stroke:{style.Stroke},stroke-width:1px");
        }
        builder.AppendLine("    end");
    }

    private static void AppendList(StringBuilder builder, string title, IReadOnlyList<string>? values)
    {
        if (values is not { Count: > 0 })
        {
            return;
        }

        builder.AppendLine($"<h2>{title}</h2><ul>");
        foreach (var value in values)
        {
            builder.AppendLine($"<li>{HtmlEncode(value)}</li>");
        }
        builder.AppendLine("</ul>");
    }

    private static void AppendAsciiList(StringBuilder builder, string title, IReadOnlyList<string>? values)
    {
        if (values is not { Count: > 0 })
        {
            return;
        }

        builder.AppendLine(title + ":");
        foreach (var value in values)
        {
            builder.AppendLine($"  - {value}");
        }
    }

    private static WorkflowVisualizationSemanticKind GetCurrentSemanticKind(AoWorkflowSnapshot snapshot)
        => string.Equals(snapshot.Status, "completed", StringComparison.Ordinal)
            ? WorkflowVisualizationSemanticKind.Completion
            : snapshot.PendingRequirements is { Count: > 0 } || !string.IsNullOrWhiteSpace(snapshot.LastBoundaryReason)
                ? WorkflowVisualizationSemanticKind.Blocked
                : WorkflowVisualizationSemanticKind.Runtime;

    private static string SanitizeNodeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "state_unknown";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '_' ? character : '_');
        }

        return builder.ToString();
    }

    private static string EscapeLabel(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string HtmlEncode(object? value)
        => WebUtility.HtmlEncode(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);

    private static string Escape(object? value)
        => SecurityElement.Escape(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty) ?? string.Empty;
}
