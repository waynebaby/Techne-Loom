using System.Diagnostics;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class CliLifecycleFlagsBehaviorTests
{
    [Fact]
    public async Task CliResumeError_FromWaitingExternal_ReportsCanResumeWithoutFreshInstance()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-cli-flags-waiting-{Guid.NewGuid():N}.json");
        var resultPath = Path.Combine(Path.GetTempPath(), $"techne-loom-cli-flags-result-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateWaitingWorkflow()));
        await File.WriteAllTextAsync(resultPath, "{\"transition_id\":\"transition.ask\",\"payload\":{}}");

        var run = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowPath}\" --result-file \"{resultPath}\"");

        Assert.Equal(2, run.ExitCode);
        using var envelope = ReadEnvelope(run.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");
        Assert.True(payload.GetProperty("can_resume").GetBoolean());
        Assert.False(payload.GetProperty("fresh_instance_required").GetBoolean());
        Assert.Equal("waiting-flags", payload.GetProperty("instance_id").GetString());
    }

    [Fact]
    public async Task CliStatus_FailedInstanceReportsCanResumeWithoutFreshInstance()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-cli-flags-failed-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateFailedWorkflow()));

        var run = await RunCliAsync(repoRoot, $"status --workflow-file \"{workflowPath}\"");

        Assert.Equal(0, run.ExitCode);
        using var envelope = ReadEnvelope(run.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");
        Assert.True(payload.GetProperty("can_resume").GetBoolean());
        Assert.False(payload.GetProperty("fresh_instance_required").GetBoolean());
    }

    [Fact]
    public async Task CliStatus_UnrecoverableFailedInstanceRequiresFreshInstance()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-cli-flags-unrecoverable-{Guid.NewGuid():N}.json");
        var instance = CreateFailedWorkflow();
        instance.History.Clear();
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(instance));

        var run = await RunCliAsync(repoRoot, $"status --workflow-file \"{workflowPath}\"");

        Assert.Equal(0, run.ExitCode);
        using var envelope = ReadEnvelope(run.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");
        Assert.False(payload.GetProperty("can_resume").GetBoolean());
        Assert.True(payload.GetProperty("fresh_instance_required").GetBoolean());
    }

    [Fact]
    public async Task CliResume_UnrecoverableFailedInstanceReportsFreshInstanceRequired()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-cli-resume-unrecoverable-{Guid.NewGuid():N}.json");
        var resultPath = Path.Combine(Path.GetTempPath(), $"techne-loom-cli-resume-unrecoverable-result-{Guid.NewGuid():N}.json");
        var instance = CreateFailedWorkflow();
        instance.History.Clear();
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(instance));
        await File.WriteAllTextAsync(resultPath, "{\"transition_id\":\"transition.noop\",\"payload\":{}}");

        var run = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowPath}\" --result-file \"{resultPath}\"");

        Assert.Equal(2, run.ExitCode);
        using var envelope = ReadEnvelope(run.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");
        Assert.False(payload.GetProperty("can_resume").GetBoolean());
        Assert.True(payload.GetProperty("fresh_instance_required").GetBoolean());
    }

    [Fact]
    public async Task CliResume_FailedInstanceRetriesFromPreviousState()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-cli-resume-failed-{Guid.NewGuid():N}.json");
        var resultPath = Path.Combine(Path.GetTempPath(), $"techne-loom-cli-resume-failed-result-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateFailedWorkflow()));
        await File.WriteAllTextAsync(resultPath, "{\"transition_id\":\"transition.noop\",\"payload\":{}}");

        var run = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowPath}\" --result-file \"{resultPath}\"");

        Assert.Equal(0, run.ExitCode);
        using var envelope = ReadFinalEnvelope(run.StdOut);
        Assert.Equal("result", envelope.RootElement.GetProperty("type").GetString());
        Assert.Equal("completed", envelope.RootElement.GetProperty("payload").GetProperty("status").GetString());
        var persisted = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowPath));
        Assert.Equal(WorkflowStatus.Succeeded, persisted.Status);
        Assert.Contains(persisted.History, entry => entry.NodeId == "transition.noop" && entry.Status == ExecutionStatus.Failed);
        Assert.Contains(persisted.History, entry => entry.NodeId == "state.start" && entry.Message is not null && entry.Message.Contains("Recovered from failed transition", StringComparison.Ordinal));
        var eventLines = await File.ReadAllLinesAsync(workflowPath + ".events.jsonl");
        Assert.Contains(eventLines, line => line.Contains("\"nodeId\":\"transition.noop\"", StringComparison.Ordinal) && line.Contains("\"status\":\"failed\"", StringComparison.Ordinal));
        Assert.Contains(eventLines, line => line.Contains("Recovered from failed transition", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CliResume_ConcurrentFailedInstanceAllowsOnlyOneRecovery()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-cli-resume-concurrent-{Guid.NewGuid():N}.json");
        var firstResultPath = Path.Combine(Path.GetTempPath(), $"techne-loom-cli-resume-concurrent-first-{Guid.NewGuid():N}.json");
        var secondResultPath = Path.Combine(Path.GetTempPath(), $"techne-loom-cli-resume-concurrent-second-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateFailedWorkflow()));
        await File.WriteAllTextAsync(firstResultPath, "{\"transition_id\":\"transition.noop\",\"payload\":{}}");
        await File.WriteAllTextAsync(secondResultPath, "{\"transition_id\":\"transition.noop\",\"payload\":{}}");

        var runs = await Task.WhenAll(
            RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowPath}\" --result-file \"{firstResultPath}\""),
            RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowPath}\" --result-file \"{secondResultPath}\""));

        Assert.Equal(1, runs.Count(run => run.ExitCode == 0));
        Assert.Equal(1, runs.Count(run => run.ExitCode == 2));
        var persisted = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowPath));
        Assert.Equal(WorkflowStatus.Succeeded, persisted.Status);
        Assert.Equal(1, persisted.History.Count(entry => entry.NodeId == "transition.noop" && entry.Status == ExecutionStatus.Failed));
        Assert.Equal(1, persisted.History.Count(entry => entry.NodeId == "state.start" && entry.Message is not null && entry.Message.Contains("Recovered from failed transition", StringComparison.Ordinal)));
        var eventLines = await File.ReadAllLinesAsync(workflowPath + ".events.jsonl");
        Assert.Equal(1, eventLines.Count(line => line.Contains("Recovered from failed transition", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CliInspectWorkflow_WaitsForWorkflowFileLock()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-cli-inspect-lock-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateFailedWorkflow()));

        await using var heldLock = await WorkflowFileLock.AcquireAsync(workflowPath);
        var inspectTask = RunCliAsync(repoRoot, $"inspect-workflow --workflow-file \"{workflowPath}\"");
        await Task.Delay(100);

        Assert.False(inspectTask.IsCompleted);
        await heldLock.DisposeAsync();
        var inspect = await inspectTask;

        Assert.Equal(0, inspect.ExitCode);
        var inspected = WorkflowJsonSerializer.Deserialize(inspect.StdOut);
        Assert.Equal("failed-flags", inspected.InstanceId);
        Assert.Equal(WorkflowStatus.Failed, inspected.Status);
    }

    [Fact]
    public async Task CliStatus_SucceededInstance_ReportsFreshInstanceRequired()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-cli-flags-succeeded-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateSucceededWorkflow()));

        var run = await RunCliAsync(repoRoot, $"status --workflow-file \"{workflowPath}\"");

        Assert.Equal(0, run.ExitCode);
        using var envelope = ReadEnvelope(run.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");
        Assert.False(payload.GetProperty("can_resume").GetBoolean());
        Assert.True(payload.GetProperty("fresh_instance_required").GetBoolean());
    }

    [Fact]
    public async Task CliRun_ResultReportsTerminalLifecycleFlags()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-cli-flags-result-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateRunnableWorkflow()));

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\"");

        Assert.Equal(0, run.ExitCode);
        using var envelope = ReadFinalEnvelope(run.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");
        Assert.False(payload.GetProperty("can_resume").GetBoolean());
        Assert.True(payload.GetProperty("fresh_instance_required").GetBoolean());
    }

    private static WorkflowInstance CreateWaitingWorkflow()
    {
        var transition = new CommandTransition
        {
            Id = "transition.ask",
            Name = "Ask",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.AskUser,
            SucceedExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new[] { "answer" },
                },
            },
        };
        var instance = CreateBaseWorkflow("waiting-flags", WorkflowStatus.WaitingExternal, transition);
        var waitGroup = new PendingWaitGroup
        {
            InstanceId = instance.InstanceId,
            TransitionId = transition.Id,
        };
        waitGroup.AddEntry(null);
        instance.ActiveWaitGroups = [waitGroup];
        return instance;
    }

    private static WorkflowInstance CreateSucceededWorkflow()
        => CreateBaseWorkflow("succeeded-flags", WorkflowStatus.Succeeded, CreateNoopTransition());

    private static WorkflowInstance CreateFailedWorkflow()
    {
        var transition = CreateNoopTransition();
        var instance = CreateBaseWorkflow("failed-flags", WorkflowStatus.Failed, transition);
        instance.History.Add(new WorkflowHistoryEntry(DateTimeOffset.UtcNow, transition.Id, TaskNodeType.Transition, ExecutionStatus.Failed, Message: "test failure"));
        return instance;
    }

    private static WorkflowInstance CreateRunnableWorkflow()
        => CreateBaseWorkflow("result-flags", WorkflowStatus.ReadyToStart, CreateNoopTransition());

    private static CommandTransition CreateNoopTransition()
        => new()
        {
            Id = "transition.noop",
            Name = "Noop",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            SucceedExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

    private static WorkflowInstance CreateBaseWorkflow(string instanceId, WorkflowStatus status, CommandTransition transition)
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Test",
            Groups = [new TransitionGroup { Id = "group.start", TransitionIds = [transition.Id] }],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "Done",
            Groups = [],
        };

        return new WorkflowInstance
        {
            InstanceId = instanceId,
            StartNodeId = start.Id,
            CurrentNodeId = status == WorkflowStatus.Succeeded ? done.Id : start.Id,
            EndNodeId = done.Id,
            Status = status,
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
            Arguments = $"\"{typeof(CliLifecycleFlagsBehaviorTests).Assembly.Location.Replace("Techne.Loom.SkillOrchestrator.Tests.dll", "so.dll", StringComparison.Ordinal)}\" {arguments}",
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

    private static JsonDocument ReadEnvelope(string stdout)
    {
        const string startTag = "<so_property>";
        const string endTag = "</so_property>";
        var start = stdout.IndexOf(startTag, StringComparison.Ordinal);
        var end = stdout.IndexOf(endTag, start + startTag.Length, StringComparison.Ordinal);
        if (start < 0 || end < 0)
        {
            throw new InvalidOperationException("SO CLI output did not contain a so_property block.");
        }

        return JsonDocument.Parse(stdout[(start + startTag.Length)..end].Trim());
    }

    private static JsonDocument ReadFinalEnvelope(string stdout)
    {
        const string startTag = "<so_property>";
        const string endTag = "</so_property>";
        var index = 0;
        JsonDocument? final = null;
        while (true)
        {
            var start = stdout.IndexOf(startTag, index, StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            var end = stdout.IndexOf(endTag, start + startTag.Length, StringComparison.Ordinal);
            if (end < 0)
            {
                break;
            }

            var document = JsonDocument.Parse(stdout[(start + startTag.Length)..end].Trim());
            var type = document.RootElement.GetProperty("type").GetString();
            if (!string.Equals(type, "progress", StringComparison.Ordinal))
            {
                final?.Dispose();
                final = document;
            }
            else
            {
                document.Dispose();
            }

            index = end + endTag.Length;
        }

        return final ?? ReadEnvelope(stdout);
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
