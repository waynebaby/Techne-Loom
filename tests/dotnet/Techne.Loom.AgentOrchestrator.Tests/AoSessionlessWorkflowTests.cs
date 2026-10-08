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

    [Fact]
    public async Task CliResume_OfflineAskUserSubmissionAppliesAndReplays()
    {
        var repoRoot = FindRepositoryRoot();
        var testRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-offline-ask-{Guid.NewGuid():N}");
        var workflowFile = Path.Combine(testRoot, "workflow.json");
        var offlineSubmissionFile = Path.Combine(testRoot, "offline-submission.json");
        var rawResultFile = Path.Combine(testRoot, "raw-resume.json");
        var storeRoot = Path.Combine(testRoot, "asks");
        Directory.CreateDirectory(testRoot);
        try
        {
            var (store, launch) = await CreateStructuredAskFixtureAsync(workflowFile, storeRoot, "ao-offline-ask");
            await File.WriteAllTextAsync(rawResultFile, JsonSerializer.Serialize(new
            {
                transition_id = "transition.ask",
                correlation_key = "ao-ask-correlation",
                payload = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["answers"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["displayName"] = "Grace" },
                },
            }));

            var rawResume = await RunCliAsync(
                repoRoot,
                $"resume --workflow-file \"{workflowFile}\" --result-file \"{rawResultFile}\"",
                storeRoot);
            Assert.Equal(2, rawResume.ExitCode);
            Assert.Contains("validated submission receipt", rawResume.StdOut + rawResume.StdErr, StringComparison.Ordinal);
            Assert.False(File.Exists(WorkflowOperationLedger.GetPath(workflowFile)));

            var submission = new AskScopedOfflineSubmission(
                SchemaVersion: 1,
                AskId: launch.AskId,
                ExpectedGeneration: launch.Generation,
                OperationId: "ao-offline-submit",
                Answers: new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.displayName"] = new AskScopedAnswerValue
                    {
                        Value = JsonSerializer.SerializeToElement("Ada"),
                    },
                });
            await File.WriteAllTextAsync(
                offlineSubmissionFile,
                JsonSerializer.Serialize(submission, WorkflowJsonSerializer.CreateDefaultOptions(indented: false)));

            var applied = await RunCliAsync(
                repoRoot,
                $"resume --workflow-file \"{workflowFile}\" --offline-submission-file \"{offlineSubmissionFile}\"",
                storeRoot);
            Assert.Equal(0, applied.ExitCode);
            Assert.Contains("\"status\":\"completed\"", applied.StdOut, StringComparison.Ordinal);
            var persisted = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var snapshot = await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability);
            Assert.Equal(WorkflowStatus.Succeeded, persisted.Status);
            Assert.Equal("Ada", Assert.IsType<string>(PathValueAccessor.GetValue(persisted.Context, "answers.displayName")));
            Assert.NotNull(snapshot.AppliedAtUtc);
            Assert.Contains(
                AskScopedSubmissionWorkflow.CreateResumeOperationId(snapshot.Receipt!),
                await File.ReadAllTextAsync(WorkflowOperationLedger.GetPath(workflowFile)),
                StringComparison.Ordinal);

            var versionAfterApply = persisted.Version;
            var replay = await RunCliAsync(
                repoRoot,
                $"resume --workflow-file \"{workflowFile}\" --offline-submission-file \"{offlineSubmissionFile}\"",
                storeRoot);
            Assert.Equal(0, replay.ExitCode);
            Assert.Contains("\"status\":\"completed\"", replay.StdOut, StringComparison.Ordinal);
            var afterReplay = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(versionAfterApply, afterReplay.Version);
            Assert.Equal(WorkflowStatus.Succeeded, afterReplay.Status);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CliResume_AskIdAppliesSubmittedAskUserReceipt()
    {
        var repoRoot = FindRepositoryRoot();
        var testRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-ask-id-{Guid.NewGuid():N}");
        var workflowFile = Path.Combine(testRoot, "workflow.json");
        var storeRoot = Path.Combine(testRoot, "asks");
        Directory.CreateDirectory(testRoot);
        try
        {
            var (store, launch) = await CreateStructuredAskFixtureAsync(workflowFile, storeRoot, "ao-ask-id");
            var receipt = await store.SubmitAsync(
                launch.AskId,
                launch.MachineCapability,
                launch.Generation,
                "ao-ask-id-submit",
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.displayName"] = new AskScopedAnswerValue
                    {
                        Value = JsonSerializer.SerializeToElement("Ada"),
                    },
                });

            var run = await RunCliAsync(
                repoRoot,
                $"resume --workflow-file \"{workflowFile}\" --ask-id \"{launch.AskId}\"",
                storeRoot);
            Assert.Equal(0, run.ExitCode);
            Assert.Contains("\"status\":\"completed\"", run.StdOut, StringComparison.Ordinal);
            var persisted = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var snapshot = await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability);
            Assert.Equal(WorkflowStatus.Succeeded, persisted.Status);
            Assert.Equal("Ada", Assert.IsType<string>(PathValueAccessor.GetValue(persisted.Context, "answers.displayName")));
            Assert.Equal(receipt.Generation, snapshot.Receipt!.Generation);
            Assert.NotNull(snapshot.AppliedAtUtc);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private static async Task<(AskScopedSubmissionStore Store, AskScopedLaunch Launch)> CreateStructuredAskFixtureAsync(
        string workflowFile,
        string storeRoot,
        string instanceId)
    {
        var transition = new CommandTransition
        {
            Id = "transition.ask",
            Name = "Ask for display name",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.AskUser,
            GuardExpression = "true",
            SucceedExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "ask_user",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new[] { "answers.displayName" },
                },
            },
            UserInput = new UserInputContract
            {
                Version = 1,
                QuestionGroups =
                [
                    new UserInputQuestionGroup
                    {
                        Id = "group.identity",
                        Title = "Identity",
                        Questions =
                        [
                            new UserInputQuestion
                            {
                                Id = "question.displayName",
                                Context = "Collect the user's preferred display name.",
                                Intent = "Use the name in the workflow context.",
                                Prompt = "What name should we use?",
                                ContextPath = "answers.displayName",
                                Type = UserInputQuestionTypes.Text,
                                Required = true,
                                Constraints = new UserInputQuestionConstraints { MinLength = 1 },
                            },
                        ],
                    },
                ],
            },
        };
        var instance = CreateWorkflow(instanceId, transition);
        instance.Status = WorkflowStatus.WaitingExternal;
        var waitGroup = new PendingWaitGroup
        {
            InstanceId = instanceId,
            TransitionId = transition.Id,
            CorrelationKey = "ao-ask-correlation",
            TargetStateId = transition.TargetNodeId,
        };
        waitGroup.AddEntry(null);
        instance.ActiveWaitGroups = [waitGroup];
        var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = storeRoot });
        var launch = await store.GetOrCreateForWaitAsync(instance, waitGroup);
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(instance));
        return (store, launch);
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

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string repoRoot, string arguments, string? askStoreRoot = null)
    {
        var apphostPath = typeof(AoSessionlessWorkflowTests).Assembly.Location.Replace("Techne.Loom.AgentOrchestrator.Tests.dll", "ao.dll", StringComparison.Ordinal);
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{apphostPath}\" {arguments}",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (!string.IsNullOrWhiteSpace(askStoreRoot))
        {
            startInfo.Environment["TECHNE_LOOM_ASK_STORE_ROOT"] = askStoreRoot;
        }

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