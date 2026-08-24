using System.Diagnostics;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class EventSidecarLineageBehaviorTests
{
    [Fact]
    public async Task CliRun_RewritesEventSidecarWhenWorkflowInstanceChanges()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-event-lineage-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateWaitingWorkflow("lineage-original")));

        var firstRun = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\"");
        Assert.Equal(3, firstRun.ExitCode);

        var metadataPath = workflowPath + ".events.jsonl.meta.json";
        Assert.True(File.Exists(metadataPath));
        using (var firstMetadata = JsonDocument.Parse(await File.ReadAllTextAsync(metadataPath)))
        {
            Assert.Equal("lineage-original", firstMetadata.RootElement.GetProperty("instance_id").GetString());
        }

        var replacement = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowPath));
        replacement.InstanceId = "lineage-replacement";
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(replacement));

        var secondRun = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\"");
        Assert.Equal(3, secondRun.ExitCode);

        using var secondMetadata = JsonDocument.Parse(await File.ReadAllTextAsync(metadataPath));
        Assert.Equal("lineage-replacement", secondMetadata.RootElement.GetProperty("instance_id").GetString());
    }

    [Fact]
    public async Task CliRun_RewritesEventSidecarWhenHistoryContextChanges()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-event-history-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateWaitingWorkflow("history-lineage")));

        var firstRun = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\"");
        Assert.Equal(3, firstRun.ExitCode);

        var eventsPath = workflowPath + ".events.jsonl";
        var lines = await File.ReadAllLinesAsync(eventsPath);
        var firstEntry = JsonSerializer.Deserialize<WorkflowHistoryEntry>(
            lines[0],
            WorkflowJsonSerializer.CreateDefaultOptions(indented: false));
        Assert.NotNull(firstEntry);
        var tamperedEntry = new WorkflowHistoryEntry(
            firstEntry!.Timestamp,
            firstEntry.NodeId,
            firstEntry.NodeType,
            firstEntry.Status,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tampered"] = true,
            },
            firstEntry.Message);
        lines[0] = JsonSerializer.Serialize(tamperedEntry, WorkflowJsonSerializer.CreateDefaultOptions(indented: false));
        await File.WriteAllLinesAsync(eventsPath, lines);

        var secondRun = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\"");
        Assert.Equal(3, secondRun.ExitCode);
        Assert.DoesNotContain("tampered", await File.ReadAllTextAsync(eventsPath), StringComparison.Ordinal);
    }

    private static WorkflowInstance CreateWaitingWorkflow(string instanceId)
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Test",
            Groups = [new TransitionGroup { Id = "group.ask", TransitionIds = ["transition.ask"] }],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "Done",
            Groups = [],
        };
        var transition = new CommandTransition
        {
            Id = "transition.ask",
            Name = "Ask",
            TargetNodeId = done.Id,
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
