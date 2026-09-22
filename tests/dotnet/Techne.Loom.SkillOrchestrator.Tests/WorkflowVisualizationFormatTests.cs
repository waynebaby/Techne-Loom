using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.SkillOrchestrator.Visualizer;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowVisualizationFormatTests
{
    [Fact]
    public async Task AllSkillVisualizationFormatsExposeSemanticLegend()
    {
        var instance = CreateWorkflow();

        var mermaid = await new MermaidWorkflowInstanceVisualizer().VisualizeToStringAsync(instance);
        var html = await new HtmlWorkflowInstanceVisualizer().VisualizeToStringAsync(instance);
        var svg = await new SvgWorkflowInstanceVisualizer().VisualizeToStringAsync(instance);
        var ascii = await new AsciiArtWorkflowInstanceVisualizer().VisualizeToStringAsync(instance);

        Assert.Contains("subgraph legend[Legend]", mermaid);
        Assert.Contains("⚙️", mermaid);
        Assert.Contains("wf-legend", html);
        Assert.Contains("⚙️", html);
        Assert.Contains("<g id=\"legend\">", svg);
        Assert.Contains("⚙️", svg);
        Assert.Contains("Legend:", ascii);
        Assert.Contains("Runtime / tool", ascii);
        Assert.Contains("✅ Done", mermaid);
        Assert.Contains("data-semantic=\"Completion\"", html);
        Assert.Contains("✅ Done", html);
        Assert.Contains("data-semantic=\"Completion\"", svg);
        Assert.Contains("Completion: State Done [Completion]", ascii);
    }

    private static WorkflowInstance CreateWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "01 Intake",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.start",
                    Strategy = ConcurrencyStrategy.FirstSuccess,
                    TransitionIds = ["transition.tool"],
                },
            ],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "02 Complete",
            Groups = [],
        };
        var tool = new CommandTransition
        {
            Id = "transition.tool",
            Name = "Echo",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            GuardExpression = "true",
            SucceedExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "echo",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["message"] = "hello",
                },
            },
        };

        return new WorkflowInstance
        {
            InstanceId = "visualization-format-test",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [tool.Id] = tool,
            },
        };
    }
}
