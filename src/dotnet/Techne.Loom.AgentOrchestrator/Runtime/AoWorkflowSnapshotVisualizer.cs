using System.Text;
using Techne.Loom.AgentOrchestrator.Models;

namespace Techne.Loom.AgentOrchestrator.Runtime;

internal static class AoWorkflowSnapshotVisualizer
{
    public static string RenderMermaid(AoWorkflowSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("flowchart TD");
        builder.AppendLine("    state_start[\"state.start\"]");
        builder.AppendLine($"    {SanitizeNodeId(snapshot.CurrentNodeId)}[\"{snapshot.CurrentNodeId}\"]");

        if (string.Equals(snapshot.Status, "completed", StringComparison.Ordinal))
        {
            builder.AppendLine("    state_completed[\"state.completed\"]");
            builder.AppendLine($"    {SanitizeNodeId(snapshot.CurrentNodeId)} --> state_completed");
            builder.AppendLine("    style state_completed fill:#dcfce7,stroke:#16a34a,stroke-width:3px");
        }
        else
        {
            builder.AppendLine($"    state_start -->|{snapshot.LastTransitionId ?? "transition.pending"}| {SanitizeNodeId(snapshot.CurrentNodeId)}");
        }

        builder.AppendLine($"    style {SanitizeNodeId(snapshot.CurrentNodeId)} fill:#fff7ed,stroke:#ea580c,stroke-width:3px");
        return builder.ToString();
    }

    public static string RenderHtml(AoWorkflowSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<html><body>");
        builder.AppendLine($"<h1>AO Workflow {snapshot.CurrentNodeId}</h1>");
        builder.AppendLine($"<div><strong>Status:</strong> {snapshot.Status}</div>");
        builder.AppendLine($"<div><strong>Current node:</strong> {snapshot.CurrentNodeId}</div>");
        if (!string.IsNullOrWhiteSpace(snapshot.LastBoundaryReason))
        {
            builder.AppendLine($"<div><strong>Boundary reason:</strong> {snapshot.LastBoundaryReason}</div>");
        }

        if (snapshot.PendingRequirements is { Count: > 0 })
        {
            builder.AppendLine("<h2>Pending requirements</h2><ul>");
            foreach (var item in snapshot.PendingRequirements)
            {
                builder.AppendLine($"<li>{item}</li>");
            }
            builder.AppendLine("</ul>");
        }

        if (snapshot.NextFrontier is { Count: > 0 })
        {
            builder.AppendLine("<h2>Next frontier</h2><ul>");
            foreach (var item in snapshot.NextFrontier)
            {
                builder.AppendLine($"<li>{item}</li>");
            }
            builder.AppendLine("</ul>");
        }

        builder.AppendLine("<h2>Context keys</h2><ul>");
        foreach (var key in snapshot.Context.Keys.OrderBy(static item => item, StringComparer.Ordinal))
        {
            builder.AppendLine($"<li>{key}</li>");
        }
        builder.AppendLine("</ul>");
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

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
}
