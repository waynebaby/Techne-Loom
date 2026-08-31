using System.Diagnostics;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class AoPlanContractValidationTests
{
    [Fact]
    public async Task CliPromptReplan_InvalidPlanContractFailsBeforeWritingPointer()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-invalid-plan-{Guid.NewGuid():N}.json");
        var sessionDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-invalid-plan-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sessionDirectory);
        var sessionId = "prompt-invalid-plan";
        var snapshotPath = Path.Combine(sessionDirectory, $"session_{sessionId}_workflow.json");
        var eventPath = Path.Combine(sessionDirectory, $"session_{sessionId}_events.jsonl");
        await File.WriteAllTextAsync(snapshotPath, "{\"objective\":\"invalid plan\",\"context\":{},\"status\":\"blocked\",\"current_node_id\":\"state.review\",\"last_transition_id\":\"transition.main_tbr\",\"last_boundary_reason\":\"replan_required\",\"updated_at\":\"2026-08-29T00:00:00Z\",\"audit_step_sequence\":1}");
        await File.WriteAllTextAsync(eventPath, string.Empty);

        var invalidPlan = new CommandTransition
        {
            Id = "transition.invalid_plan",
            Name = "Invalid plan",
            WorkflowPhase = "Review",
            TargetNodeId = "state.end",
            StepKind = WorkflowStepKind.Plan,
            Plan = new PlanStepContract
            {
                InputPaths = [],
                ResultFile = "",
                RequiredEvidence = [],
                ApplyMode = "manual",
            },
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var tbr = new ToBeRefinedTransition
        {
            Id = "transition.main_tbr",
            Name = "Main TBR",
            WorkflowPhase = "Review",
            TargetNodeId = "state.end",
            StepKind = WorkflowStepKind.ModelThink,
            DesignNotes = "Replan target",
        };
        var start = new StateNode
        {
            Id = "state.review",
            Name = "Review",
            WorkflowPhase = "Review",
            Groups = [new TransitionGroup { Id = "group.review", TransitionIds = [tbr.Id, invalidPlan.Id] }],
        };
        var end = new StateNode { Id = "state.end", Name = "End", WorkflowPhase = "Done", Groups = [] };
        var instance = new WorkflowInstance
        {
            InstanceId = "prompt-invalid-plan",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = end.Id,
            Status = WorkflowStatus.Running,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [end.Id] = end,
                [tbr.Id] = tbr,
                [invalidPlan.Id] = invalidPlan,
            },
        };
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(instance));

        try
        {
            var run = await RunCliAsync(repoRoot, $"prompt-replan --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --instance-file \"{workflowFile}\" --tbr-id \"{tbr.Id}\"");

            Assert.Equal(2, run.ExitCode);
            Assert.Contains("plan/inputPaths", run.StdOut, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(sessionDirectory, $"session_{sessionId}_runtime.workflow.pointer.json")));
        }
        finally
        {
            DeleteDirectory(sessionDirectory);
            DeleteFile(workflowFile);
        }
    }

    [Fact]
    public async Task CliCompile_InvalidPlanContractWritesFailureAudit()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-invalid-plan-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-invalid-plan-audit-{Guid.NewGuid():N}");
        var transition = new CommandTransition
        {
            Id = "transition.plan",
            Name = "Invalid plan",
            WorkflowPhase = "01 Planning",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.Plan,
            Plan = new PlanStepContract
            {
                InputPaths = [""],
                ResultFile = "",
                RequiredEvidence = [""],
                ApplyMode = "manual",
            },
            GuardExpression = "true",
            SucceedExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
            },
        };
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "01 Planning",
            Groups = [new TransitionGroup { Id = "group.start", TransitionIds = [transition.Id] }],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "02 Done",
            Groups = [],
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "invalid-plan",
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
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(instance));

        try
        {
            var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");

            Assert.Equal(2, run.ExitCode);
            Assert.Contains("plan/inputPaths", run.StdOut, StringComparison.Ordinal);
            Assert.Contains("plan/resultFile", run.StdOut, StringComparison.Ordinal);
            Assert.Contains("plan/requiredEvidence", run.StdOut, StringComparison.Ordinal);
            Assert.Contains("plan/applyMode", run.StdOut, StringComparison.Ordinal);
            var failureFeedbackFile = Assert.Single(Directory.GetFiles(auditDirectory, "workflow.compile-feedback.json", SearchOption.AllDirectories));
            using var failureFeedback = JsonDocument.Parse(await File.ReadAllTextAsync(failureFeedbackFile));
            Assert.Equal("failed", failureFeedback.RootElement.GetProperty("status").GetString());
            Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.json", SearchOption.AllDirectories).Single()));
            Assert.Empty(Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories));
            Assert.Empty(Directory.GetFiles(auditDirectory, "workflow.html", SearchOption.AllDirectories));
        }
        finally
        {
            DeleteDirectory(auditDirectory);
            DeleteFile(workflowFile);
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string repoRoot, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{typeof(AoPlanContractValidationTests).Assembly.Location.Replace("Techne.Loom.AgentOrchestrator.Tests.dll", "ao.dll", StringComparison.Ordinal)}\" {arguments}",
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
            if (File.Exists(Path.Combine(current.FullName, "Techne.Loom.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}