using System.Diagnostics;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class AoSessionlessWorkflowTests
{
    [Fact]
    public async Task CliRun_WithWorkflowFileExecutesWithoutSessionArtifacts()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-sessionless-{Guid.NewGuid():N}.json");
        var instance = CreateWorkflow("sessionless-terminal", new CommandTransition
        {
            Id = "transition.echo",
            Name = "Echo",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            OutputPath = "result.message",
            GuardExpression = "true",
            SucceedExpression = "context.Get<string>(\"result.message\") == \"hello\"",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "echo",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { ["message"] = "hello" },
            },
        });
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, instance);

        try
        {
            var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowFile}\"");
            Assert.Equal(0, run.ExitCode);
            Assert.DoesNotContain("session_id", run.StdOut, StringComparison.Ordinal);
            Assert.Contains("\"status\":\"completed\"", run.StdOut, StringComparison.Ordinal);
            var persisted = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.Succeeded, persisted.Status);
            Assert.Equal("hello", PathValueAccessor.GetValue(persisted.Context, "result.message"));
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task CliRunAndResume_WithWorkflowFileRecoverFromPlanWithoutSession()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-sessionless-plan-{Guid.NewGuid():N}.json");
        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-sessionless-plan-result-{Guid.NewGuid():N}.json");
        var plan = new CommandTransition
        {
            Id = "transition.plan",
            Name = "Plan",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.Plan,
            Plan = new PlanStepContract
            {
                InputPaths = ["objective"],
                ResultFile = "plan.result.json",
                RequiredEvidence = ["plan.evidence"],
                WeaveBackTargetNodeId = "state.done",
            },
            GuardExpression = "true",
            SucceedExpression = "context.Get<string>(\"plan.answer\") == \"approved\"",
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, CreateWorkflow("sessionless-plan", plan));
        await File.WriteAllTextAsync(resultFile, JsonSerializer.Serialize(new
        {
            transition_id = plan.Id,
            result_id = "ao-plan-result-1",
            correlation_key = (string?)null,
            payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["plan"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "approved" },
            },
        }));

        try
        {
            var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowFile}\"");
            Assert.Equal(3, run.ExitCode);
            Assert.Contains("\"status\":\"blocked\"", run.StdOut, StringComparison.Ordinal);
            Assert.DoesNotContain("session_id", run.StdOut, StringComparison.Ordinal);

            var resume = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --result-file \"{resultFile}\"");
            Assert.Equal(0, resume.ExitCode);
            Assert.Contains("\"status\":\"completed\"", resume.StdOut, StringComparison.Ordinal);
            Assert.DoesNotContain("session_id", resume.StdOut, StringComparison.Ordinal);
            var versionBeforeDuplicate = (await CanonicalWorkflowFileStore.LoadAsync(workflowFile)).Version;
            var duplicate = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --result-file \"{resultFile}\"");
            Assert.Equal(0, duplicate.ExitCode);
            Assert.Contains("\"status\":\"completed\"", duplicate.StdOut, StringComparison.Ordinal);
            var afterDuplicate = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(versionBeforeDuplicate, afterDuplicate.Version);
            Assert.Equal(WorkflowStatus.Succeeded, afterDuplicate.Status);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
            if (File.Exists(resultFile)) File.Delete(resultFile);
        }
    }

    private static WorkflowInstance CreateWorkflow(string instanceId, CommandTransition transition)
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "01 Start",
            Groups = [new TransitionGroup { Id = "group.start", TransitionIds = [transition.Id] }],
        };
        var done = new StateNode { Id = "state.done", Name = "Done", WorkflowPhase = "02 Done", Groups = [] };
        return new WorkflowInstance
        {
            InstanceId = instanceId,
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [transition.Id] = transition,
            },
        };
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string repoRoot, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{typeof(AoSessionlessWorkflowTests).Assembly.Location.Replace("Techne.Loom.AgentOrchestrator.Tests.dll", "ao.dll", StringComparison.Ordinal)}\" {arguments}",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start AO CLI process.");
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
            if (File.Exists(Path.Combine(current.FullName, "Techne.Loom.sln"))) return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("Repository root not found.");
    }

    private static void DeleteWorkflowFiles(string workflowFile)
    {
        foreach (var path in new[] { workflowFile, workflowFile + ".lock" })
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}