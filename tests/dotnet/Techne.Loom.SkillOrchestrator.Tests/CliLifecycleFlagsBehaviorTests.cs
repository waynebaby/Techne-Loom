using System.Diagnostics;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class CliLifecycleFlagsBehaviorTests
{
    [Fact]
    public async Task CliRun_AskUserBoundaryIncludesAutopilotAndAuthoredHints()
    {
        var repoRoot = FindRepositoryRoot();
        var testRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-so-hint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        try
        {
            var workflowPath = Path.Combine(testRoot, "workflow.json");
            var workflow = CreateWaitingWorkflow();
            var transition = Assert.IsType<CommandTransition>(workflow.Nodes["transition.ask"]);
            var parameters = Assert.IsType<Dictionary<string, object?>>(transition.Command.Parameters);
            parameters["skillHint"] = "Keep the authored instruction.";
            await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(workflow));

            var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\" --audit-output \"{testRoot}\"");
            Assert.Equal(3, run.ExitCode);
            using var envelope = ReadEnvelope(run.StdOut);
            Assert.Equal("boundary", envelope.RootElement.GetProperty("type").GetString());
            var hint = envelope.RootElement.GetProperty("payload").GetProperty("skill_hint").GetString();
            Assert.NotNull(hint);
            Assert.Contains("Keep the authored instruction.", hint, StringComparison.Ordinal);
            Assert.Contains("AO ask_user_endpoints or SO ask_user envelope endpoints", hint, StringComparison.Ordinal);
            Assert.Contains("do not submit an agent-generated approval via HTTP", hint, StringComparison.Ordinal);
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
    public async Task CliRun_ReportsAdditionalAskUserWithoutChangingSelectedTransition()
    {
        var repoRoot = FindRepositoryRoot();
        var testRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-so-parallel-hint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        try
        {
            var workflowPath = Path.Combine(testRoot, "workflow.json");
            var workflow = CreateWaitingWorkflow();
            var ask = Assert.IsType<CommandTransition>(workflow.Nodes["transition.ask"]);
            Assert.IsType<Dictionary<string, object?>>(ask.Command.Parameters)["skillHint"] = "Keep the additional ask instruction.";
            var selected = CreateNoopTransition();
            workflow.Nodes[selected.Id] = selected;
            Assert.IsType<StateNode>(workflow.Nodes[workflow.StartNodeId]).Groups[0].TransitionIds.Insert(0, selected.Id);
            var selectedWait = new PendingWaitGroup
            {
                InstanceId = workflow.InstanceId,
                TransitionId = selected.Id,
                TargetStateId = selected.TargetNodeId,
            };
            selectedWait.AddEntry(null);
            workflow.ActiveWaitGroups.Insert(0, selectedWait);
            await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(workflow));

            var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\" --audit-output \"{testRoot}\"");
            Assert.Equal(3, run.ExitCode);
            using var envelope = ReadEnvelope(run.StdOut);
            var payload = envelope.RootElement.GetProperty("payload");
            Assert.Equal("ToolCall", payload.GetProperty("current_step_kind").GetString());
            var hint = Assert.IsType<string>(payload.GetProperty("skill_hint").GetString());
            Assert.Contains("Additional active AskUser transition 'transition.ask'", hint, StringComparison.Ordinal);
            Assert.Contains("Keep the additional ask instruction.", hint, StringComparison.Ordinal);
            Assert.Contains("do not call a blocking user-input tool", hint, StringComparison.Ordinal);
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
    public async Task CliInspectWorkflowFragment_DefaultsToSummaryAndSupportsJsonPointer()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-cli-fragment-{Guid.NewGuid():N}.json");
        var workflow = CreateFailedWorkflow();
        workflow.Context["secret"] = "do-not-return";
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(workflow));

        try
        {
            var summaryRun = await RunCliAsync(repoRoot, $"inspect-workflow-fragment --workflow-file \"{workflowPath}\"");
            Assert.Equal(0, summaryRun.ExitCode);
            using var summary = JsonDocument.Parse(summaryRun.StdOut);
            Assert.Equal(JsonValueKind.Null, summary.RootElement.GetProperty("fragment").ValueKind);
            Assert.DoesNotContain("do-not-return", summaryRun.StdOut, StringComparison.Ordinal);

            var fragmentRun = await RunCliAsync(repoRoot, $"inspect-workflow-fragment --workflow-file \"{workflowPath}\" --json-pointer /context/secret");
            Assert.Equal(0, fragmentRun.ExitCode);
            using var fragment = JsonDocument.Parse(fragmentRun.StdOut);
            Assert.Equal("/context/secret", fragment.RootElement.GetProperty("jsonPointer").GetString());
            Assert.Equal(JsonValueKind.String, fragment.RootElement.GetProperty("fragment").ValueKind);
            Assert.Equal("do-not-return", fragment.RootElement.GetProperty("fragment").GetString());
        }
        finally
        {
            DeleteFile(workflowPath);
        }
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

    [Fact]
    public async Task CliRun_InvalidAskUserHintDoesNotStartOperationOrMutateWorkflow()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-invalid-hint-{Guid.NewGuid():N}.json");
        try
        {
            var instance = CreateWaitingWorkflow();
            instance.Status = WorkflowStatus.ReadyToStart;
            instance.ActiveWaitGroups.Clear();
            var transition = Assert.IsType<CommandTransition>(instance.Nodes["transition.ask"]);
            Assert.IsType<Dictionary<string, object?>>(Assert.IsType<CommandInvocation>(transition.Command).Parameters)["skillHint"] = 42;
            await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(instance));
            var before = await File.ReadAllTextAsync(workflowFile);

            var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowFile}\" --operation-id invalid-hint");

            Assert.Equal(2, run.ExitCode);
            Assert.Contains("string skillHint", run.StdOut + run.StdErr, StringComparison.Ordinal);
            Assert.Equal(before, await File.ReadAllTextAsync(workflowFile));
            Assert.False(File.Exists(WorkflowOperationLedger.GetPath(workflowFile)));
        }
        finally
        {
            foreach (var path in new[]
            {
                workflowFile,
                workflowFile + ".lock",
                WorkflowOperationLedger.GetPath(workflowFile),
                WorkflowOperationLedger.GetPath(workflowFile) + ".lock",
            })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }

    [Fact]
    public async Task CliResume_OfflineAskUserSubmissionAppliesAndReplays()
    {
        var repoRoot = FindRepositoryRoot();
        var testRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-so-offline-ask-{Guid.NewGuid():N}");
        var workflowFile = Path.Combine(testRoot, "workflow.json");
        var offlineSubmissionFile = Path.Combine(testRoot, "offline-submission.json");
        var rawResultFile = Path.Combine(testRoot, "raw-resume.json");
        var storeRoot = Path.Combine(testRoot, "asks");
        Directory.CreateDirectory(testRoot);
        try
        {
            var (_, store, launch) = await CreateStructuredAskFixtureAsync(workflowFile, storeRoot, "so-offline-ask");
            await File.WriteAllTextAsync(rawResultFile, JsonSerializer.Serialize(new
            {
                transition_id = "transition.ask",
                correlation_key = "so-ask-correlation",
                payload = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["answers"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["displayName"] = "Grace" },
                },
            }));

            var rawResume = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --result-file \"{rawResultFile}\"", storeRoot);
            Assert.Equal(2, rawResume.ExitCode);
            Assert.Contains("validated submission receipt", rawResume.StdOut + rawResume.StdErr, StringComparison.Ordinal);
            Assert.False(File.Exists(WorkflowOperationLedger.GetPath(workflowFile)));

            var submission = new AskScopedOfflineSubmission(
                SchemaVersion: 1,
                AskId: launch.AskId,
                ExpectedGeneration: launch.Generation,
                OperationId: "so-offline-submit",
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

            var invalidInstance = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile));
            var invalidTransition = Assert.IsType<CommandTransition>(invalidInstance.Nodes["transition.ask"]);
            var invalidParameters = Assert.IsType<Dictionary<string, object?>>(Assert.IsType<CommandInvocation>(invalidTransition.Command).Parameters);
            invalidParameters["skillHint"] = 42;
            await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(invalidInstance));
            var beforeInvalidResume = await File.ReadAllTextAsync(workflowFile);

            var invalidRun = await RunCliAsync(
                repoRoot,
                $"resume --workflow-file \"{workflowFile}\" --offline-submission-file \"{offlineSubmissionFile}\"",
                storeRoot);
            Assert.Equal(2, invalidRun.ExitCode);
            Assert.Contains("string skillHint", invalidRun.StdOut + invalidRun.StdErr, StringComparison.Ordinal);
            Assert.Equal(beforeInvalidResume, await File.ReadAllTextAsync(workflowFile));
            Assert.Null((await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability)).Receipt);
            Assert.False(File.Exists(WorkflowOperationLedger.GetPath(workflowFile)));

            invalidParameters.Remove("skillHint");
            await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(invalidInstance));

            var appliedRun = await RunCliAsync(
                repoRoot,
                $"resume --workflow-file \"{workflowFile}\" --offline-submission-file \"{offlineSubmissionFile}\"",
                storeRoot);
            Assert.True(appliedRun.ExitCode == 0, appliedRun.StdOut + appliedRun.StdErr);
            using (var result = ReadFinalEnvelope(appliedRun.StdOut))
            {
                Assert.Equal("result", result.RootElement.GetProperty("type").GetString());
                Assert.Equal("completed", result.RootElement.GetProperty("payload").GetProperty("status").GetString());
            }

            var persisted = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile));
            var snapshot = await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability);
            Assert.Equal(WorkflowStatus.Succeeded, persisted.Status);
            Assert.Equal("Ada", Assert.IsType<string>(PathValueAccessor.GetValue(persisted.Context, "answers.displayName")));
            Assert.NotNull(snapshot.AppliedAtUtc);
            Assert.Contains(
                AskScopedSubmissionWorkflow.CreateResumeOperationId(snapshot.Receipt!),
                await File.ReadAllTextAsync(WorkflowOperationLedger.GetPath(workflowFile)),
                StringComparison.Ordinal);

            var versionAfterApply = persisted.Version;
            var replayRun = await RunCliAsync(
                repoRoot,
                $"resume --workflow-file \"{workflowFile}\" --offline-submission-file \"{offlineSubmissionFile}\"",
                storeRoot);
            Assert.Equal(0, replayRun.ExitCode);
            var afterReplay = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile));
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
        var testRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-so-ask-id-{Guid.NewGuid():N}");
        var workflowFile = Path.Combine(testRoot, "workflow.json");
        var storeRoot = Path.Combine(testRoot, "asks");
        Directory.CreateDirectory(testRoot);
        try
        {
            var (_, store, launch) = await CreateStructuredAskFixtureAsync(workflowFile, storeRoot, "so-ask-id");
            var receipt = await store.SubmitAsync(
                launch.AskId,
                launch.MachineCapability,
                launch.Generation,
                "so-ask-id-submit",
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.displayName"] = new AskScopedAnswerValue
                    {
                        Value = JsonSerializer.SerializeToElement("Ada"),
                    },
                });

            var run = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --ask-id \"{launch.AskId}\"", storeRoot);
            Assert.True(run.ExitCode == 0, run.StdOut + run.StdErr);
            var persisted = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile));
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

    private static async Task<(WorkflowInstance Instance, AskScopedSubmissionStore Store, AskScopedLaunch Launch)> CreateStructuredAskFixtureAsync(
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
        var instance = CreateBaseWorkflow(instanceId, WorkflowStatus.WaitingExternal, transition);
        var waitGroup = new PendingWaitGroup
        {
            InstanceId = instanceId,
            TransitionId = transition.Id,
            TargetStateId = transition.TargetNodeId,
            CorrelationKey = "so-ask-correlation",
        };
        waitGroup.AddEntry(null);
        instance.ActiveWaitGroups = [waitGroup];
        var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = storeRoot });
        var launch = await store.GetOrCreateForWaitAsync(instance, waitGroup);
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(instance));
        return (instance, store, launch);
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
            ContractBinding = SkillOrchestratorBehaviorTestBase.CreateTestContextContractBinding(),
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

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(
        string repoRoot,
        string arguments,
        string? askStoreRoot = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "\"" + typeof(CliLifecycleFlagsBehaviorTests).Assembly.Location.Replace("Techne.Loom.SkillOrchestrator.Tests.dll", "so.dll", StringComparison.Ordinal) + "\" " + arguments,
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
    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var lockFile = path + ".lock";
        if (File.Exists(lockFile))
        {
            File.Delete(lockFile);
        }
    }
}
