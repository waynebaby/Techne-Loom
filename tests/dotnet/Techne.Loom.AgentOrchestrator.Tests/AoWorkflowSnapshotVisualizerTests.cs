using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.AgentOrchestrator.Cli;
using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.AgentOrchestrator.Runtime;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class AoWorkflowSnapshotVisualizerTests
{
    [Theory]
    [InlineData("state.start", "state_start")]
    [InlineData("boundary.review", "boundary_review")]
    public void MermaidCurrentNodeUsesTripleWidthDottedOutline(string nodeId, string renderedNodeId)
    {
        var mermaid = AoWorkflowSnapshotVisualizer.RenderMermaid(CreateSnapshot("blocked", nodeId, ["decision"]));

        Assert.Contains($"style {renderedNodeId} fill:#fee2e2,stroke:#ea580c,stroke-width:9px,stroke-dasharray:1 12,stroke-linecap:round", mermaid);
        Assert.Single(mermaid.Split(Environment.NewLine), static line => line.Contains("stroke-dasharray", StringComparison.Ordinal));
        Assert.Contains("style legend_intake fill:#e0f2fe,stroke:#0284c7,stroke-width:1px", mermaid);
    }

    [Fact]
    public void MermaidWorkflowInstanceUsesTripleWidthDottedOutline()
    {
        var instance = new WorkflowInstance { CurrentNodeId = "state.review" };

        var mermaid = AoCommandHandlers.RenderWorkflowInstanceMermaidForRuntime(instance);

        Assert.Contains("style state.review fill:#fff7ed,stroke:#ea580c,stroke-width:9px,stroke-dasharray:1 12,stroke-linecap:round", mermaid);
    }

    [Fact]
    public void SemanticFormatsExposeBlockedAndCompletionChannels()
    {
        var blocked = CreateSnapshot("blocked", "boundary.review", ["decision"]);
        var completed = CreateSnapshot("completed", "state.done", []);

        var blockedMermaid = AoWorkflowSnapshotVisualizer.RenderMermaid(blocked);
        var blockedHtml = AoWorkflowSnapshotVisualizer.RenderHtml(blocked);
        var blockedSvg = AoWorkflowSnapshotVisualizer.RenderSvg(blocked);
        var blockedAscii = AoWorkflowSnapshotVisualizer.RenderAscii(blocked);
        var completedMermaid = AoWorkflowSnapshotVisualizer.RenderMermaid(completed);

        Assert.Contains("legend_blocked[\"🚧 Blocked / boundary\"]", blockedMermaid);
        Assert.Contains("🚧 boundary.review", blockedMermaid);
        Assert.Contains("ao-legend", blockedHtml);
        Assert.Contains("🚧", blockedHtml);
        Assert.Contains("<g id=\"legend\">", blockedSvg);
        Assert.Contains("🚧", blockedSvg);
        Assert.Contains("Legend:", blockedAscii);
        Assert.Contains("Blocked / boundary", blockedAscii);
        Assert.Contains("✅ state.completed", completedMermaid);
        Assert.Contains("legend_completion[\"✅ Completion\"]", completedMermaid);
        Assert.DoesNotContain("stroke-dasharray", completedMermaid);
    }

    private static AoWorkflowSnapshot CreateSnapshot(string status, string currentNodeId, IReadOnlyList<string> pendingRequirements)
        => new(
            Objective: "visualization test",
            Context: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["decision"] = "approved",
            },
            Status: status,
            CurrentNodeId: currentNodeId,
            LastTransitionId: status == "completed" ? null : "transition.review",
            LastBoundaryReason: status == "completed" ? null : "review_required",
            UpdatedAt: DateTimeOffset.UtcNow,
            PendingRequirements: pendingRequirements,
            NextFrontier: status == "completed" ? null : ["review"],
            HumanOrAgentHint: "Provide the structured review decision.",
            WeaveOutRequest: null,
            AuditStepSequence: 1);
}
