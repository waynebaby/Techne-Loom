using System.Diagnostics;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class SoSessionlessPlanResultIdTests
{
    [Fact]
    public async Task CliRunAndResumeWithWorkflowFile_DeduplicatesPlanResultAcrossProcesses()
    {
        var repoRoot = FindRepositoryRoot();
        var directory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-plan-result-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var workflowFile = Path.Combine(directory, "workflow.json");
        var resultFile = Path.Combine(directory, "result.json");
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, CreateWorkflow());
        await File.WriteAllTextAsync(
            resultFile,
            "{\"transition_id\":\"transition.plan\",\"correlation_key\":null,\"result_id\":\"so-plan-result-1\",\"payload\":{\"plan\":{\"answer\":\"approved\"}}}");

        try
        {
            var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowFile}\"");
            Assert.Equal(3, run.ExitCode);

            var resume = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --result-file \"{resultFile}\"");
            Assert.Equal(0, resume.ExitCode);
            var terminal = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var versionBeforeDuplicate = terminal.Version;
            var historyBeforeDuplicate = terminal.History.Count;
            Assert.Equal(WorkflowStatus.Succeeded, terminal.Status);
            Assert.Contains("so-plan-result-1", JsonSerializer.Serialize(terminal.Context), StringComparison.Ordinal);

            var duplicate = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --result-file \"{resultFile}\"");
            Assert.Equal(0, duplicate.ExitCode);
            var afterDuplicate = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.Succeeded, afterDuplicate.Status);
            Assert.Equal(versionBeforeDuplicate, afterDuplicate.Version);
            Assert.Equal(historyBeforeDuplicate, afterDuplicate.History.Count);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static WorkflowInstance CreateWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "01 Start",
            Groups = [new TransitionGroup { Id = "group.start", TransitionIds = ["transition.plan"] }],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "02 Done",
            Groups = [],
        };
        var plan = new CommandTransition
        {
            Id = "transition.plan",
            Name = "Plan",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.Plan,
            Plan = new PlanStepContract
            {
                InputPaths = ["objective"],
                ResultFile = "plan.result.json",
                RequiredEvidence = ["plan.evidence"],
                WeaveBackTargetNodeId = done.Id,
            },
            GuardExpression = "true",
            SucceedExpression = "context.Get<string>(\"plan.answer\") == \"approved\"",
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        return new WorkflowInstance
        {
            InstanceId = "so-sessionless-plan-result",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [plan.Id] = plan,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["objective"] = "approve this plan",
            },
        };
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string repoRoot, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{typeof(DefaultWorkflowTaskTrackingService).Assembly.Location}\" {arguments}",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start SO CLI process.");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout, stderr);
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
