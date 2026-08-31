using System.Diagnostics;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class AoSessionlessPlanningTests
{
    [Fact]
    public async Task PromptReplanWithWorkflowFile_DoesNotCreateSessionStateOrModifyCanonicalWorkflow()
    {
        var repoRoot = FindRepositoryRoot();
        var directory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-sessionless-replan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var workflowFile = Path.Combine(directory, "workflow.json");
        var objectiveFile = Path.Combine(directory, "objective.md");
        var instance = CreateWorkflow();
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, instance);
        await File.WriteAllTextAsync(objectiveFile, "Replace the selected planning seam while preserving the terminal path.");
        var before = await File.ReadAllTextAsync(workflowFile);

        try
        {
            var result = await RunCliAsync(
                repoRoot,
                $"prompt-replan --workflow-file \"{workflowFile}\" --objective-file \"{objectiveFile}\" --tbr-id \"transition.main_tbr\"");

            Assert.Equal(0, result.ExitCode);
            using var envelope = ReadAoEnvelope(result.StdOut);
            var payload = envelope.RootElement.GetProperty("payload");
            Assert.Equal("prompt-replan", payload.GetProperty("command").GetString());
            Assert.Equal(workflowFile, payload.GetProperty("workflow_file").GetString());
            Assert.Equal(workflowFile, payload.GetProperty("workflow_instance_file").GetString());
            Assert.Equal(JsonValueKind.Null, payload.GetProperty("session_id").ValueKind);
            Assert.Equal("transition.main_tbr", payload.GetProperty("selected_tbr_id").GetString());
            Assert.Equal(before, await File.ReadAllTextAsync(workflowFile));
            Assert.DoesNotContain(Directory.EnumerateFiles(directory), path => Path.GetFileName(path).Contains("session_", StringComparison.Ordinal));
            Assert.DoesNotContain(Directory.EnumerateFiles(directory), path => path.EndsWith(".pointer.json", StringComparison.Ordinal));
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
        var review = new StateNode
        {
            Id = "state.review",
            Name = "Review",
            WorkflowPhase = "01 Review",
            Groups = [new TransitionGroup { Id = "group.review", TransitionIds = ["transition.main_tbr"] }],
        };
        var end = new StateNode
        {
            Id = "state.end",
            Name = "End",
            WorkflowPhase = "02 Complete",
            Groups = [],
        };
        var tbr = new ToBeRefinedTransition
        {
            Id = "transition.main_tbr",
            Name = "Main planning seam",
            TargetNodeId = end.Id,
            StepKind = WorkflowStepKind.WaitResume,
            DesignNotes = "Await the replan authored by the agent.",
        };
        return new WorkflowInstance
        {
            InstanceId = "sessionless-replan",
            StartNodeId = review.Id,
            CurrentNodeId = review.Id,
            EndNodeId = end.Id,
            Status = WorkflowStatus.WaitingExternal,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [review.Id] = review,
                [end.Id] = end,
                [tbr.Id] = tbr,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["objective"] = "Replace the selected planning seam while preserving the terminal path.",
                ["plan_meta"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["selected_frontier_action"] = "continue_with_confirmed_plan",
                },
            },
        };
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string repoRoot, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{Path.Combine(repoRoot, "src", "dotnet", "Techne.Loom.AgentOrchestrator", "bin", "Debug", "net9.0", "ao.dll")}\" {arguments}",
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

    private static JsonDocument ReadAoEnvelope(string stdout)
    {
        const string startTag = "<ao_property>";
        const string endTag = "</ao_property>";
        var start = stdout.IndexOf(startTag, StringComparison.Ordinal);
        var end = stdout.IndexOf(endTag, start + startTag.Length, StringComparison.Ordinal);
        if (start < 0 || end < 0)
        {
            throw new InvalidOperationException("AO output did not contain an ao_property envelope.");
        }

        return JsonDocument.Parse(stdout.Substring(start + startTag.Length, end - start - startTag.Length).Trim());
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
