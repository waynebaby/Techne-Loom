using System.Diagnostics;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpFirstValidationTests
{
    [Fact]
    public async Task Compile_RejectsGovernedWorkflowWithoutMcpFirstTransition()
    {
        var workflow = new WorkflowInstance
        {
            InstanceId = "mcp-first-invalid",
            TemplateKind = "so-governed-target-skill",
            TaskType = "skill_enhancement",
            WorkflowKind = "target_skill_enhancement",
            CaseId = "test-case",
            RunId = "test-run",
            StartNodeId = "state.start",
            CurrentNodeId = "state.start",
            EndNodeId = "state.done",
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["state.start"] = new StateNode
                {
                    Id = "state.start",
                    Name = "Start",
                    WorkflowPhase = "Start",
                    Groups = [new TransitionGroup { Id = "group.start", TransitionIds = ["transition.done"] }],
                },
                ["transition.done"] = new CommandTransition
                {
                    Id = "transition.done",
                    Name = "Complete",
                    TargetNodeId = "state.done",
                    StepKind = WorkflowStepKind.ToolCall,
                    GuardExpression = "true",
                    SucceedExpression = "true",
                    Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
                },
                ["state.done"] = new StateNode
                {
                    Id = "state.done",
                    Name = "Done",
                    WorkflowPhase = "Done",
                    Groups = [],
                },
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
            Validation = new WorkflowValidationContract
            {
                Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal),
                Routes = new Dictionary<string, WorkflowRouteValidationProfile>(StringComparer.Ordinal),
            },
        };

        var output = await CompileAsync(workflow, "mcp-first-invalid");

        Assert.NotEqual(0, output.ExitCode);
        Assert.Contains("exactly one MCP-first", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compile_RejectsMcpFirstWithUnboundSuccessPredicate()
    {
        var workflow = await ReadTemplateAsync();
        var mcp = Assert.IsType<CommandTransition>(workflow.Nodes["transition.start_mcp"]);
        workflow.Nodes[mcp.Id] = mcp with { SucceedExpression = "true" };

        var output = await CompileAsync(workflow, "mcp-first-predicate");

        Assert.NotEqual(0, output.ExitCode);
        Assert.Contains("MCP-first transition", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compile_RejectsMcpFirstWithWrongRuntimeCommand()
    {
        var workflow = await ReadTemplateAsync();
        var mcp = Assert.IsType<CommandTransition>(workflow.Nodes["transition.start_mcp"]);
        var command = (CommandInvocation)mcp.Command.Clone();
        command.Parameters!["runtimeCommand"] = "dotnet so.dll mcp wrong";
        workflow.Nodes[mcp.Id] = mcp with { Command = command };

        var output = await CompileAsync(workflow, "mcp-first-runtime-command");

        Assert.NotEqual(0, output.ExitCode);
        Assert.Contains("MCP-first transition", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compile_RejectsMcpFirstWithoutResultProjection()
    {
        var workflow = await ReadTemplateAsync();
        var mcp = Assert.IsType<CommandTransition>(workflow.Nodes["transition.start_mcp"]);
        var command = (CommandInvocation)mcp.Command.Clone();
        command.Parameters!.Remove("outputBindings");
        workflow.Nodes[mcp.Id] = mcp with { Command = command };

        var output = await CompileAsync(workflow, "mcp-first-result-projection");

        Assert.NotEqual(0, output.ExitCode);
        Assert.Contains("MCP-first transition", output.Text, StringComparison.Ordinal);
    }

    private static async Task<WorkflowInstance> ReadTemplateAsync()
    {
        var workflowFile = Path.Combine(FindRepositoryRoot(), ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "so-template.json");
        return WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile));
    }

    private static async Task<(int ExitCode, string Text)> CompileAsync(WorkflowInstance workflow, string name)
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-{name}-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(workflow));
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = FindRepositoryRoot(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(typeof(DefaultWorkflowTaskTrackingService).Assembly.Location);
            startInfo.ArgumentList.Add("compile");
            startInfo.ArgumentList.Add("--workflow-file");
            startInfo.ArgumentList.Add(workflowFile);

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start SO compile process.");
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, stdout + Environment.NewLine + stderr);
        }
        finally
        {
            if (File.Exists(workflowFile))
            {
                File.Delete(workflowFile);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Techne.Loom.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
