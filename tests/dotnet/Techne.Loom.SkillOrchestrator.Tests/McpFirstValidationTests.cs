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
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-mcp-first-invalid-{Guid.NewGuid():N}.json");
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
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(workflow));

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repoRoot,
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

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("MCP-first", stdout + stderr, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(workflowFile))
            {
                File.Delete(workflowFile);
            }
        }
    }


    [Fact]
    public async Task Compile_RejectsMcpFirstTransitionWithUnboundSuccessPredicate()
    {
        var repoRoot = FindRepositoryRoot();
        var sourceWorkflowFile = Path.Combine(repoRoot, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "so-template.json");
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-mcp-first-predicate-{Guid.NewGuid():N}.json");
        var workflow = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(sourceWorkflowFile));
        var mcp = Assert.IsType<CommandTransition>(workflow.Nodes["transition.start_mcp"]);
        workflow.Nodes[mcp.Id] = mcp with { SucceedExpression = "true" };
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(workflow));

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repoRoot,
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

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("project mcp_startup_evidence", stdout + stderr, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(workflowFile))
            {
                File.Delete(workflowFile);
            }
        }
    }    private static string FindRepositoryRoot()
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
