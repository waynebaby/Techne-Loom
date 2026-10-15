using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowFileExecutionServiceTests
{
    [Fact]
    public async Task RunAsync_InvalidUserInputContractRejectsBeforeContextMutation()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-invalid-user-input-{Guid.NewGuid():N}.json");
        try
        {
            var transition = new CommandTransition
            {
                Id = "transition.invalid-ask",
                Name = "Invalid AskUser",
                TargetNodeId = "state.done",
                StepKind = WorkflowStepKind.AskUser,
                Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "ask_user" },
                UserInput = new UserInputContract { Version = 2 },
            };
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, CreateWorkflow("invalid-user-input", transition));
            var before = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new WorkflowFileExecutionService().RunAsync(
                    workflowFile,
                    contextDelta: new Dictionary<string, object?>(StringComparer.Ordinal) { ["answers.displayName"] = "Grace" },
                    operationId: "invalid-user-input-op"));
            var after = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            Assert.Contains("userInput/version", error.Message, StringComparison.Ordinal);
            Assert.Equal(WorkflowStatus.ReadyToStart, after.Status);
            Assert.Equal(before.Version, after.Version);
            Assert.Empty(after.Context);
            Assert.Equal(before.History.Count, after.History.Count);
            Assert.False(File.Exists(WorkflowOperationLedger.GetPath(workflowFile)));
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task ResumeAsync_InvalidUserInputContractRejectsBeforeContextMutationAndLedgerWrite()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-invalid-user-input-resume-{Guid.NewGuid():N}.json");
        try
        {
            var transition = new CommandTransition
            {
                Id = "transition.invalid-ask",
                Name = "Invalid AskUser",
                TargetNodeId = "state.done",
                StepKind = WorkflowStepKind.AskUser,
                Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "ask_user" },
                UserInput = new UserInputContract { Version = 2 },
            };
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, CreateWorkflow("invalid-user-input-resume", transition));
            var before = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new WorkflowFileExecutionService().ResumeAsync(
                    workflowFile,
                    transition.Id,
                    correlationKey: null,
                    payload: new Dictionary<string, object?>(StringComparer.Ordinal) { ["answers.displayName"] = "Grace" },
                    operationId: "invalid-user-input-resume-op"));
            var after = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            Assert.Contains("userInput/version", error.Message, StringComparison.Ordinal);
            Assert.Equal(WorkflowStatus.ReadyToStart, after.Status);
            Assert.Equal(before.Version, after.Version);
            Assert.Empty(after.Context);
            Assert.Equal(before.History.Count, after.History.Count);
            Assert.False(File.Exists(WorkflowOperationLedger.GetPath(workflowFile)));
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task RunAsync_ReturnsAskUserEndpointWithoutPersistingPairingCredentials()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ask-endpoint-{Guid.NewGuid():N}.json");
        const string pairingUrl = "http://127.0.0.1:43127/#pair=one-use-pairing-code";
        try
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
                                    Intent = "Use the name in the next workflow step.",
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
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, CreateWorkflow("ask-endpoint", transition));
            var service = new WorkflowFileExecutionService(
                core: null,
                startAskWorkers: (instance, _, _) =>
                {
                    Assert.Single(AskScopedSubmissionWorkflow.GetActiveStructuredWaitGroups(instance));
                    return Task.FromResult<IReadOnlyList<AskScopedWorkerEndpoint>>(
                    [
                        new AskScopedWorkerEndpoint("ask-test", pairingUrl, DateTimeOffset.UtcNow.AddMinutes(10)),
                    ]);
                });

            var result = await service.RunAsync(workflowFile, operationId: "ask-endpoint-run");
            var workflowJson = await File.ReadAllTextAsync(workflowFile);
            var eventJson = await File.ReadAllTextAsync(result.EventLogFile);
            var ledgerJson = await File.ReadAllTextAsync(WorkflowOperationLedger.GetPath(workflowFile));
            var resultJson = System.Text.Json.JsonSerializer.Serialize(result, WorkflowJsonSerializer.CreateDefaultOptions(indented: false));

            Assert.Equal(WorkflowStatus.WaitingExternal, result.Status.Status);
            Assert.Equal(pairingUrl, Assert.Single(result.AskUserEndpoints).Url);
            Assert.DoesNotContain("one-use-pairing-code", workflowJson, StringComparison.Ordinal);
            Assert.DoesNotContain("one-use-pairing-code", eventJson, StringComparison.Ordinal);
            Assert.DoesNotContain("one-use-pairing-code", ledgerJson, StringComparison.Ordinal);
            Assert.DoesNotContain("one-use-pairing-code", resultJson, StringComparison.Ordinal);
            Assert.DoesNotContain("askUserEndpoints", resultJson, StringComparison.Ordinal);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task ResumeFromOfflineAskUserSubmissionAsync_AppliesAfterPersistenceAndReplaysIdempotently()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-ask-receipt-resume-{Guid.NewGuid():N}");
        var workflowFile = Path.Combine(testRoot, "workflow.json");
        var askRoot = Path.Combine(testRoot, "asks");
        try
        {
            Directory.CreateDirectory(testRoot);
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
                                    Intent = "Use the name in the next workflow step.",
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
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, CreateWorkflow("ask-receipt-resume", transition));
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = askRoot });
            var service = new WorkflowFileExecutionService(
                core: null,
                startAskWorkers: (_, _, _) => Task.FromResult<IReadOnlyList<AskScopedWorkerEndpoint>>([]));
            var waiting = await service.RunAsync(workflowFile);
            var waitingInstance = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var waitGroup = Assert.Single(AskScopedSubmissionWorkflow.GetActiveStructuredWaitGroups(waitingInstance));
            var launch = await store.GetOrCreateForWaitAsync(waitingInstance, waitGroup);

            var rawResumeError = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(
                workflowFile,
                transition.Id,
                correlationKey: null,
                payload: new Dictionary<string, object?>(StringComparer.Ordinal) { ["answers.displayName"] = "Grace" }));
            Assert.Contains("validated submission receipt", rawResumeError.Message, StringComparison.Ordinal);
            var stillWaiting = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.WaitingExternal, stillWaiting.Status);
            Assert.Empty(stillWaiting.Context);
            Assert.False(File.Exists(WorkflowOperationLedger.GetPath(workflowFile)));

            var submission = new AskScopedOfflineSubmission(
                SchemaVersion: 1,
                AskId: launch.AskId,
                ExpectedGeneration: launch.Generation,
                OperationId: "ask-offline-submit",
                Answers: new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.displayName"] = new AskScopedAnswerValue
                    {
                        Value = System.Text.Json.JsonSerializer.SerializeToElement("Ada"),
                    },
                });
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ResumeFromAskUserOfflineSubmissionAsync(workflowFile, submission with { SchemaVersion = 2 }, store));
            Assert.Null((await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability)).Receipt);

            var applied = await service.ResumeFromAskUserOfflineSubmissionAsync(workflowFile, submission, store);
            var persisted = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var appliedSnapshot = await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability);
            var receipt = appliedSnapshot.Receipt!;
            var resumeOperationId = AskScopedSubmissionWorkflow.CreateResumeOperationId(receipt);
            var ledgerJson = await File.ReadAllTextAsync(WorkflowOperationLedger.GetPath(workflowFile));

            Assert.Equal(WorkflowStatus.WaitingExternal, waiting.Status.Status);
            Assert.Equal(WorkflowStatus.Succeeded, applied.Status.Status);
            Assert.Equal("Ada", Assert.IsType<string>(PathValueAccessor.GetValue(persisted.Context, "answers.displayName")));
            Assert.NotNull(appliedSnapshot.AppliedAtUtc);
            Assert.Contains(resumeOperationId, ledgerJson, StringComparison.Ordinal);

            var versionAfterApply = persisted.Version;
            var replay = await service.ResumeFromAskUserOfflineSubmissionAsync(workflowFile, submission, store);
            var afterReplay = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.Succeeded, replay.Status.Status);
            Assert.Equal(versionAfterApply, afterReplay.Version);
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
    public async Task ResumeFromStandaloneAskReceipt_RejectsWithoutMutatingWorkflow()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-standalone-receipt-resume-{Guid.NewGuid():N}");
        var workflowFile = Path.Combine(testRoot, "workflow.json");
        var askRoot = Path.Combine(testRoot, "asks");
        try
        {
            Directory.CreateDirectory(testRoot);
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
                                    Intent = "Use the name in the next workflow step.",
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
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, CreateWorkflow("standalone-receipt-resume", transition));
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = askRoot });
            var service = new WorkflowFileExecutionService(
                core: null,
                startAskWorkers: (_, _, _) => Task.FromResult<IReadOnlyList<AskScopedWorkerEndpoint>>([]));
            var waiting = await service.RunAsync(workflowFile);
            Assert.Equal(WorkflowStatus.WaitingExternal, waiting.Status.Status);
            var beforeWorkflow = await File.ReadAllTextAsync(workflowFile);
            var eventLogPath = CanonicalWorkflowFileStore.GetEventLogPath(workflowFile);
            var beforeEvents = await File.ReadAllTextAsync(eventLogPath);
            var beforeInstance = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            var standaloneContract = new UserInputContract
            {
                Version = 1,
                QuestionGroups =
                [
                    new UserInputQuestionGroup
                    {
                        Id = "group.standalone",
                        Title = "Standalone questions",
                        Questions =
                        [
                            new UserInputQuestion
                            {
                                Id = "question.standalone",
                                Context = "Collect an answer without workflow identity.",
                                Intent = "Keep the standalone consumer independent.",
                                Prompt = "What is your preferred display name?",
                                Type = UserInputQuestionTypes.Text,
                                Required = true,
                            },
                        ],
                    },
                ],
            };
            var standaloneLaunch = await store.CreateStandaloneAsync(standaloneContract);
            await store.SubmitAsync(
                standaloneLaunch.AskId,
                standaloneLaunch.MachineCapability,
                standaloneLaunch.Generation,
                "standalone-resume-attempt",
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.standalone"] = new()
                    {
                        Value = System.Text.Json.JsonSerializer.SerializeToElement("Grace"),
                    },
                });

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ResumeFromAskUserReceiptAsync(workflowFile, standaloneLaunch.AskId, store));
            Assert.Contains("does not match its persisted ask identity", error.Message, StringComparison.Ordinal);

            var afterInstance = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var afterSnapshot = await store.GetSnapshotAsync(standaloneLaunch.AskId, standaloneLaunch.MachineCapability);
            Assert.Equal(beforeWorkflow, await File.ReadAllTextAsync(workflowFile));
            Assert.Equal(beforeEvents, await File.ReadAllTextAsync(eventLogPath));
            Assert.Equal(WorkflowStatus.WaitingExternal, afterInstance.Status);
            Assert.Equal(beforeInstance.Version, afterInstance.Version);
            Assert.Equal(beforeInstance.History.Count, afterInstance.History.Count);
            Assert.Empty(afterInstance.Context);
            Assert.Null(afterSnapshot.AppliedAtUtc);
            Assert.False(File.Exists(WorkflowOperationLedger.GetPath(workflowFile)));
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
    public async Task RunAsync_ExecutesDeterministicWorkflowAndPersistsTerminalState()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-file-core-{Guid.NewGuid():N}.json");
        try
        {
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, CreateWorkflow(
                "file-core-terminal",
                new CommandTransition
                {
                    Id = "transition.echo",
                    Name = "Echo",
                    TargetNodeId = "state.done",
                    StepKind = WorkflowStepKind.ToolCall,
                    OutputPath = "result.message",
                    GuardExpression = "true",
                    SucceedExpression = "context.Get<string?>(\"result.message\") == \"hello\"",
                    Command = new CommandInvocation
                    {
                        Kind = CommandInvocationKind.Tool,
                        Name = "echo",
                        Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["message"] = "hello",
                        },
                    },
                }));

            var service = new WorkflowFileExecutionService();
            var first = await service.RunAsync(workflowFile);
            var persisted = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var second = await new WorkflowFileExecutionService().GetStatusAsync(workflowFile);

            Assert.Equal(WorkflowStatus.Succeeded, first.Status.Status);
            Assert.True(File.Exists(first.EventLogFile));
            Assert.Contains("execution", await File.ReadAllTextAsync(first.EventLogFile), StringComparison.Ordinal);
            Assert.Equal(WorkflowStatus.Succeeded, persisted.Status);
            Assert.Equal(WorkflowStatus.Succeeded, second.Status.Status);
            Assert.Equal("hello", PathValueAccessor.GetValue(persisted.Context, "result.message"));
            Assert.Equal(2, persisted.History.Count(entry => entry.Status == ExecutionStatus.Succeeded));
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task RunAsync_UsesWorkflowExpressionBindingForBoundedCollectionPredicates()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-file-expression-binding-{Guid.NewGuid():N}.json");
        var workflow = CreateWorkflow("file-core-expression-binding", new CommandTransition
        {
            Id = "transition.state-update",
            Name = "Write and verify state",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.StateUpdate,
            GuardExpression = "true",
            SucceedExpression = "context.Get<IReadOnlyList<string>>(\"values\").Count > 0",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.NativeCode,
                Name = "state.update",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["test_result"] = "written" },
                },
            },
        });
        workflow.ExpressionBinding.RequiredExpressionCapabilities.Add(ExpressionCapabilityIds.BoundedCollections);
        workflow.Context["values"] = new[] { "accepted" };

        try
        {
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);
            var result = await new WorkflowFileExecutionService().RunAsync(workflowFile);
            var persisted = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            Assert.Equal(WorkflowStatus.Succeeded, result.Status.Status);
            Assert.Equal("state.done", persisted.CurrentNodeId);
            Assert.Equal("written", PathValueAccessor.GetValue(persisted.Context, "test_result"));
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task RunAndResumeAsync_WhenAnyResponseSelectsWinnerAndIgnoresLoser()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-file-fanout-{Guid.NewGuid():N}.json");
        var first = new CommandTransition
        {
            Id = "transition.first",
            Name = "First",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ModelThink,
            OutputPath = "result.answer",
            PassExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "external",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { ["resumeOutputKey"] = "answer" },
            },
        };
        var second = new CommandTransition
        {
            Id = "transition.second",
            Name = "Second",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ModelThink,
            OutputPath = "result.answer",
            PassExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "external",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { ["resumeOutputKey"] = "answer" },
            },
        };
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.race",
                    Strategy = ConcurrencyStrategy.WhenAnyResponse,
                    TransitionIds = [first.Id, second.Id],
                },
            ],
        };
        var done = new StateNode { Id = "state.done", Name = "Done" };
        var workflow = new WorkflowInstance
        {
            InstanceId = "workflow-file-fanout",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [first.Id] = first,
                [second.Id] = second,
            },
        };

        try
        {
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);
            var service = new WorkflowFileExecutionService();
            var blocked = await service.RunAsync(workflowFile);
            var waiting = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var loserWaitId = Assert.Single(waiting.ActiveWaitGroups.Single(group => group.TransitionId == first.Id).Entries).WaitId;

            var winner = await ResumeWithActiveWaitIdAsync(service,
                workflowFile,
                second.Id,
                correlationKey: null,
                payload: new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "second" });
            var completed = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var signal = Assert.Single(completed.FanoutCancellationSignals);

            Assert.Equal(WorkflowStatus.WaitingExternal, blocked.Status.Status);
            Assert.Equal(2, waiting.ActiveWaitGroups.Count);
            Assert.Equal(WorkflowStatus.Succeeded, winner.Status.Status);
            Assert.Equal("second", PathValueAccessor.GetValue(completed.Context, "result.answer"));
            Assert.Equal(first.Id, signal.TransitionId);
            Assert.Equal(loserWaitId, signal.WaitId);
            Assert.Equal($"{completed.InstanceId}:{loserWaitId}", signal.SignalId);
            Assert.True(signal.CancellationRequested);
            Assert.True(signal.LateResultsIgnored);

            var versionAfterWinner = completed.Version;
            await service.ResumeAsync(
                workflowFile,
                first.Id,
                correlationKey: null,
                payload: new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "late" },
                waitId: loserWaitId);
            var afterLateResult = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(versionAfterWinner, afterLateResult.Version);
            Assert.Equal("second", PathValueAccessor.GetValue(afterLateResult.Context, "result.answer"));
            Assert.Single(afterLateResult.FanoutCancellationSignals);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task RunAndResumeAsync_WhenAnyPassWaitsForPassingBranch()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-file-any-pass-{Guid.NewGuid():N}.json");
        var first = CreateFanoutTransition("transition.reject", "result.reject.answer", "context.Get<string>(\"result.reject.answer\") == \"accepted\"");
        var second = CreateFanoutTransition("transition.accept", "result.accept.answer", "context.Get<string>(\"result.accept.answer\") == \"accepted\"");
        var workflow = CreateFanoutWorkflow("file-any-pass", new TransitionGroup
        {
            Id = "group.any-pass",
            Strategy = ConcurrencyStrategy.WhenAnyPass,
            TransitionIds = [first.Id, second.Id],
        }, "state.done", first, second);

        try
        {
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);
            var service = new WorkflowFileExecutionService();
            var blocked = await service.RunAsync(workflowFile);
            var firstResult = await ResumeWithActiveWaitIdAsync(service, workflowFile, first.Id, null,
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "rejected" });
            var afterFailure = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            Assert.Equal(WorkflowStatus.WaitingExternal, blocked.Status.Status);
            Assert.Equal(WorkflowStatus.WaitingExternal, firstResult.Status.Status);
            Assert.Equal(2, afterFailure.ActiveWaitGroups.Count);
            Assert.Null(PathValueAccessor.GetValue(afterFailure.Context, "result.reject.answer"));
            Assert.Null(PathValueAccessor.GetValue(afterFailure.Context, "result.accept.answer"));

            var winner = await ResumeWithActiveWaitIdAsync(service, workflowFile, second.Id, null,
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "accepted" });
            var completed = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            Assert.Equal(WorkflowStatus.Succeeded, winner.Status.Status);
            Assert.Equal("accepted", PathValueAccessor.GetValue(completed.Context, "result.accept.answer"));
            Assert.Empty(completed.ActiveWaitGroups);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Theory]
    [InlineData(true, "state.single-all-join")]
    [InlineData(false, "state.single-no-pass")]
    public async Task RunAndResumeAsync_WhenAllPassHasOneEligibleBranchUsesGroupOutcome(bool branchPasses, string expectedTarget)
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-file-single-all-pass-{Guid.NewGuid():N}.json");
        var active = CreateFanoutTransition("transition.single-active", "result.active", branchPasses ? "true" : "false") with
        {
            TargetNodeId = "state.branch-done",
        };
        var excluded = CreateFanoutTransition("transition.single-excluded", "result.excluded") with
        {
            GuardExpression = "false",
        };
        var group = new TransitionGroup
        {
            Id = "group.single-all-pass",
            Strategy = ConcurrencyStrategy.WhenAllPass,
            JoinTargetStateId = "state.single-all-join",
            NoPassTargetStateId = "state.single-no-pass",
            TransitionIds = [active.Id, excluded.Id],
        };
        var workflow = CreateFanoutWorkflow("file-single-all-pass", group, expectedTarget, active, excluded);

        try
        {
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);
            var service = new WorkflowFileExecutionService();
            var initial = await service.RunAsync(workflowFile);
            var waiting = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var pending = Assert.Single(waiting.ActiveWaitGroups);

            Assert.Equal(WorkflowStatus.WaitingExternal, initial.Status.Status);
            Assert.Equal(group.Id, pending.ConcurrencyGroupId);
            Assert.Equal(new[] { active.Id }, pending.ExpectedTransitionIds.ToArray());

            var resumed = await ResumeWithActiveWaitIdAsync(service, workflowFile, active.Id, null,
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "response" });
            var completed = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            Assert.Equal(WorkflowStatus.Succeeded, resumed.Status.Status);
            Assert.Equal(expectedTarget, completed.CurrentNodeId);
            Assert.Empty(completed.ActiveWaitGroups);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Theory]
    [InlineData("accepted", "state.single-expression-join")]
    [InlineData("rejected", "state.single-expression-no-pass")]
    public async Task RunAndResumeAsync_WhenExpressionHasOneEligibleBranchEvaluatesGroupPredicate(string answer, string expectedTarget)
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-file-single-expression-{Guid.NewGuid():N}.json");
        var active = CreateFanoutTransition("transition.single-expression-active", "result.active") with
        {
            TargetNodeId = "state.branch-done",
        };
        var excluded = CreateFanoutTransition("transition.single-expression-excluded", "result.excluded") with
        {
            GuardExpression = "false",
        };
        var group = new TransitionGroup
        {
            Id = "group.single-expression",
            Strategy = ConcurrencyStrategy.WhenExpression,
            PassExpression = "context.Get<string>(\"result.active\") == \"accepted\"",
            JoinTargetStateId = "state.single-expression-join",
            NoPassTargetStateId = "state.single-expression-no-pass",
            TransitionIds = [active.Id, excluded.Id],
        };
        var workflow = CreateFanoutWorkflow("file-single-expression", group, expectedTarget, active, excluded);

        try
        {
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);
            var service = new WorkflowFileExecutionService();
            await service.RunAsync(workflowFile);
            var waiting = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var pending = Assert.Single(waiting.ActiveWaitGroups);
            Assert.Equal(group.Id, pending.ConcurrencyGroupId);
            Assert.Equal(new[] { active.Id }, pending.ExpectedTransitionIds.ToArray());

            var resumed = await ResumeWithActiveWaitIdAsync(service, workflowFile, active.Id, null,
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = answer });
            var completed = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            Assert.Equal(WorkflowStatus.Succeeded, resumed.Status.Status);
            Assert.Equal(expectedTarget, completed.CurrentNodeId);
            Assert.Empty(completed.ActiveWaitGroups);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task RunAndResumeAsync_WhenAllPassJoinsAfterEveryBranchAndProjectsSeparately()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-file-all-pass-{Guid.NewGuid():N}.json");
        var first = CreateFanoutTransition("transition.first", "result.first");
        var second = CreateFanoutTransition("transition.second", "result.second");
        var workflow = CreateFanoutWorkflow("file-all-pass", new TransitionGroup
        {
            Id = "group.all-pass",
            Strategy = ConcurrencyStrategy.WhenAllPass,
            JoinTargetStateId = "state.join",
            TransitionIds = [first.Id, second.Id],
        }, "state.join", first, second);

        try
        {
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);
            var service = new WorkflowFileExecutionService();
            await service.RunAsync(workflowFile);
            var firstResult = await ResumeWithActiveWaitIdAsync(service, workflowFile, first.Id, null,
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "one" });
            var afterFirst = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            Assert.Equal(WorkflowStatus.WaitingExternal, firstResult.Status.Status);
            Assert.Equal(second.Id, firstResult.PendingTransitionId);
            Assert.Null(PathValueAccessor.GetValue(afterFirst.Context, "result.first"));

            var joined = await ResumeWithActiveWaitIdAsync(service, workflowFile, second.Id, null,
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "two" });
            var completed = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            Assert.Equal(WorkflowStatus.Succeeded, joined.Status.Status);
            Assert.Equal("one", PathValueAccessor.GetValue(completed.Context, "result.first"));
            Assert.Equal("two", PathValueAccessor.GetValue(completed.Context, "result.second"));
            Assert.Empty(completed.ActiveWaitGroups);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task RunAndResumeAsync_WhenExpressionJoinsEarlyAndSignalsPendingBranch()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-file-when-expression-{Guid.NewGuid():N}.json");
        var first = CreateFanoutTransition("transition.first", "result.choice");
        var second = CreateFanoutTransition("transition.second", "result.other");
        var workflow = CreateFanoutWorkflow("file-when-expression", new TransitionGroup
        {
            Id = "group.when-expression",
            Strategy = ConcurrencyStrategy.WhenExpression,
            PassExpression = "context.Get<string>(\"result.choice\") == \"first\"",
            JoinTargetStateId = "state.join",
            TransitionIds = [first.Id, second.Id],
        }, "state.join", first, second);

        try
        {
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);
            var service = new WorkflowFileExecutionService();
            await service.RunAsync(workflowFile);
            var joined = await ResumeWithActiveWaitIdAsync(service, workflowFile, first.Id, null,
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "first" });
            var completed = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var signal = Assert.Single(completed.FanoutCancellationSignals);

            Assert.Equal(WorkflowStatus.Succeeded, joined.Status.Status);
            Assert.Equal("first", PathValueAccessor.GetValue(completed.Context, "result.choice"));
            Assert.Null(PathValueAccessor.GetValue(completed.Context, "result.other"));
            Assert.Equal(second.Id, signal.TransitionId);
            Assert.Equal("fanout_completed", signal.Reason);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task RunAndResumeAsync_WhenFanoutTimesOutRoutesAndSignalsPendingBranches()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-file-fanout-timeout-{Guid.NewGuid():N}.json");
        var first = CreateFanoutTransition("transition.first", "result.first");
        var second = CreateFanoutTransition("transition.second", "result.second");
        var group = new TransitionGroup
        {
            Id = "group.timeout",
            Strategy = ConcurrencyStrategy.WhenAnyResponse,
            GroupTimeout = TimeSpan.FromMinutes(1),
            TimeoutTargetStateId = "state.timeout",
            TransitionIds = [first.Id, second.Id],
        };
        var workflow = CreateFanoutWorkflow("file-fanout-timeout", group, "state.timeout", first, second);
        var clock = new TestClock(DateTimeOffset.UtcNow);

        try
        {
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);
            var service = new WorkflowFileExecutionService(new WorkflowExecutionCore(clock: clock));
            await service.RunAsync(workflowFile);
            clock.Advance(TimeSpan.FromMinutes(1));

            var timeout = await ResumeWithActiveWaitIdAsync(service, workflowFile, first.Id, null,
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "late" });
            var timedOut = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            Assert.Equal(WorkflowStatus.Succeeded, timeout.Status.Status);
            Assert.Equal("state.timeout", timedOut.CurrentNodeId);
            Assert.Equal(2, timedOut.FanoutCancellationSignals.Count);
            Assert.All(timedOut.FanoutCancellationSignals, signal => Assert.Equal("fanout_timeout", signal.Reason));
            Assert.Null(PathValueAccessor.GetValue(timedOut.Context, "result.first"));
            Assert.Empty(timedOut.ActiveWaitGroups);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task RunAsync_WhenFanoutTimeoutExpiresWithoutResumeRoutesTimeoutTarget()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-file-fanout-no-resume-timeout-{Guid.NewGuid():N}.json");
        var first = CreateFanoutTransition("transition.no-resume-first", "result.first");
        var second = CreateFanoutTransition("transition.no-resume-second", "result.second");
        var timeout = new StateNode { Id = "state.timeout", Name = "Timed out", Groups = [] };
        var group = new TransitionGroup
        {
            Id = "group.no-resume-timeout",
            Strategy = ConcurrencyStrategy.WhenAllPass,
            GroupTimeout = TimeSpan.FromMinutes(1),
            TimeoutTargetStateId = timeout.Id,
            JoinTargetStateId = "state.join",
            TransitionIds = [first.Id, second.Id],
        };
        var workflow = CreateFanoutWorkflow("file-no-resume-timeout", group, timeout.Id, first, second);
        workflow.Nodes[timeout.Id] = timeout;
        var clock = new TestClock(DateTimeOffset.UtcNow);

        try
        {
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);
            var service = new WorkflowFileExecutionService(new WorkflowExecutionCore(clock: clock));
            var waiting = await service.RunAsync(workflowFile);
            var waitingInstance = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var pendingWaitIds = waitingInstance.ActiveWaitGroups
                .SelectMany(static waitGroup => waitGroup.Entries)
                .Where(static entry => !entry.Completed)
                .Select(static entry => entry.WaitId)
                .ToArray();

            Assert.Equal(WorkflowStatus.WaitingExternal, waiting.Status.Status);
            Assert.Equal(2, pendingWaitIds.Length);

            clock.Advance(TimeSpan.FromMinutes(1));
            var timedOut = await service.RunAsync(workflowFile);
            var finalInstance = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            Assert.Equal(WorkflowStatus.Succeeded, timedOut.Status.Status);
            Assert.Equal(timeout.Id, finalInstance.CurrentNodeId);
            Assert.Empty(finalInstance.ActiveWaitGroups);
            Assert.Equal(pendingWaitIds.OrderBy(static waitId => waitId, StringComparer.Ordinal), finalInstance.FanoutCancellationSignals.Select(static signal => signal.WaitId).OrderBy(static waitId => waitId, StringComparer.Ordinal));
            Assert.All(finalInstance.FanoutCancellationSignals, static signal =>
            {
                Assert.Equal("fanout_timeout", signal.Reason);
                Assert.True(signal.LateResultsIgnored);
            });
            Assert.False(finalInstance.Context.ContainsKey("result.first"));
            Assert.False(finalInstance.Context.ContainsKey("result.second"));
            Assert.Single(finalInstance.History, entry => entry.Message == "WaitTimeoutTransitionApplied");
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task RunAndResumeAsync_WhenFanoutOutputsOverlapFailsBeforeExternalWaits()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-file-fanout-overlap-{Guid.NewGuid():N}.json");
        var first = CreateFanoutTransition("transition.first", "result.shared");
        var second = CreateFanoutTransition("transition.second", "result.shared");
        var group = new TransitionGroup
        {
            Id = "group.overlap",
            Strategy = ConcurrencyStrategy.WhenAllPass,
            JoinTargetStateId = "state.join",
            ErrorHandlingTargetStateId = "state.error",
            TransitionIds = [first.Id, second.Id],
        };
        var workflow = CreateFanoutWorkflow("file-fanout-overlap", group, "state.error", first, second);

        try
        {
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);
            var service = new WorkflowFileExecutionService();
            var failed = await service.RunAsync(workflowFile);
            var persisted = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            Assert.Equal(WorkflowStatus.Failed, failed.Status.Status);
            Assert.Equal("state.start", persisted.CurrentNodeId);
            Assert.Empty(persisted.ActiveWaitGroups);
            Assert.Null(PathValueAccessor.GetValue(persisted.Context, "result.shared"));
            Assert.Contains(persisted.History, entry => entry.Message?.Contains("overlapping output projections", StringComparison.Ordinal) == true);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task ResumeAsync_FailedExternalResultClearsWaitGroup()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-file-plan-failed-{Guid.NewGuid():N}.json");
        try
        {
            var planTransition = new CommandTransition
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
                },
                GuardExpression = "true",
                SucceedExpression = "context.Get<string?>(\"plan.answer\") == \"approved\"",
                Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
            };
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, CreateWorkflow("file-core-plan-failed", planTransition));
            var service = new WorkflowFileExecutionService();
            await service.RunAsync(workflowFile);

            var failed = await service.ResumeAsync(
                workflowFile,
                planTransition.Id,
                null,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["plan"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "rejected" },
                },
                resultId: "plan-result-failed");
            var persisted = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            Assert.Equal(WorkflowStatus.Failed, failed.Status.Status);
            Assert.Equal(WorkflowStatus.Failed, persisted.Status);
            Assert.Empty(persisted.ActiveWaitGroups);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task RunAndResumeAsync_UseOnlyTheCanonicalFileAcrossServiceInstances()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-file-plan-{Guid.NewGuid():N}.json");
        try
        {
            var planTransition = new CommandTransition
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
                SucceedExpression = "context.Get<string?>(\"plan.answer\") == \"approved\"",
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.Tool,
                    Name = "noop",
                },
            };
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, CreateWorkflow("file-core-plan", planTransition));

            var blocked = await new WorkflowFileExecutionService().RunAsync(workflowFile, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["objective"] = "choose a route",
            });
            var waitingFile = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            var resumed = await new WorkflowFileExecutionService().ResumeAsync(
                workflowFile,
                planTransition.Id,
                correlationKey: null,
                payload: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["plan"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "approved" },
                },
                resultId: "plan-result-approved",
                operationId: "resume-op-1");
            var terminalFile = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            Assert.Equal(WorkflowStatus.WaitingExternal, blocked.Status.Status);
            Assert.Equal(WorkflowStatus.WaitingExternal, waitingFile.Status);
            Assert.Equal(planTransition.Id, blocked.PendingTransitionId);
            Assert.Equal(WorkflowStepKind.Plan, blocked.PendingStepKind);
            Assert.Equal("plan.result.json", blocked.ResultFile);
            Assert.Equal(WorkflowStatus.Succeeded, resumed.Status.Status);
            Assert.Equal(WorkflowStatus.Succeeded, terminalFile.Status);
            Assert.Equal("approved", PathValueAccessor.GetValue(terminalFile.Context, "plan.answer"));
            var versionBeforeDuplicate = terminalFile.Version;
            var historyCountBeforeDuplicate = terminalFile.History.Count;
            var replay = await new WorkflowFileExecutionService().ResumeAsync(
                workflowFile,
                planTransition.Id,
                correlationKey: null,
                payload: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["plan"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "approved" },
                },
                resultId: "plan-result-approved",
                operationId: "resume-op-1");
            var afterReplay = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(resumed.Status.Status, replay.Status.Status);
            Assert.Equal(terminalFile.Version, afterReplay.Version);
            Assert.Equal(terminalFile.History.Count, afterReplay.History.Count);
            Assert.Contains("\"operation_id\":\"resume-op-1\"", await File.ReadAllTextAsync(resumed.EventLogFile), StringComparison.Ordinal);
            var duplicate = await new WorkflowFileExecutionService().ResumeAsync(
                workflowFile,
                planTransition.Id,
                correlationKey: null,
                payload: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["plan"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "approved" },
                },
                resultId: "plan-result-approved");
            var afterDuplicate = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.Succeeded, duplicate.Status.Status);
            Assert.Equal(versionBeforeDuplicate, afterDuplicate.Version);
            Assert.Equal(historyCountBeforeDuplicate, afterDuplicate.History.Count);
            Assert.Empty(afterDuplicate.ActiveWaitGroups);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task RunAsync_WithSameOperationIdReusesPersistedResult()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-operation-replay-{Guid.NewGuid():N}.json");
        try
        {
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, CreateWorkflow("operation-replay", new CommandTransition
            {
                Id = "transition.echo",
                Name = "Echo",
                TargetNodeId = "state.done",
                StepKind = WorkflowStepKind.ToolCall,
                OutputPath = "result.message",
                GuardExpression = "true",
                SucceedExpression = "context.Get<string?>(\"result.message\") == \"hello\"",
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.Tool,
                    Name = "echo",
                    Parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { ["message"] = "hello" },
                },
            }));

            var first = await new WorkflowFileExecutionService().RunAsync(workflowFile, operationId: "run-op-1");
            var afterFirst = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var transition = Assert.IsType<CommandTransition>(afterFirst.Nodes["transition.echo"]);
            afterFirst.Nodes[transition.Id] = transition with
            {
                StepKind = WorkflowStepKind.AskUser,
                UserInput = new UserInputContract { Version = 2 },
            };
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, afterFirst);
            var beforeReplay = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var second = await new WorkflowFileExecutionService().RunAsync(workflowFile, operationId: "run-op-1");
            var afterSecond = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var ledger = await File.ReadAllLinesAsync(WorkflowOperationLedger.GetPath(workflowFile));

            Assert.Equal(first.Status with { FanoutCancellationSignals = second.Status.FanoutCancellationSignals }, second.Status);
            Assert.Equal(first.Status.FanoutCancellationSignals?.ToArray(), second.Status.FanoutCancellationSignals?.ToArray());
            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(beforeReplay.Version, afterSecond.Version);
            Assert.Equal(2, Assert.IsType<CommandTransition>(afterSecond.Nodes["transition.echo"]).UserInput!.Version);
            Assert.Equal(afterFirst.History.Count, afterSecond.History.Count);
            Assert.Equal(2, ledger.Length);
            Assert.Contains(ledger, line => line.Contains("\"status\":\"started\"", StringComparison.Ordinal));
            Assert.Contains(ledger, line => line.Contains("\"status\":\"completed\"", StringComparison.Ordinal));
            Assert.Contains("\"operation_id\":\"run-op-1\"", await File.ReadAllTextAsync(first.EventLogFile), StringComparison.Ordinal);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task RunAsync_WithSameOperationIdAndDifferentRequestFailsClosed()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-operation-request-{Guid.NewGuid():N}.json");
        try
        {
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, CreateWorkflow("operation-request", new CommandTransition
            {
                Id = "transition.echo", Name = "Echo", TargetNodeId = "state.done", StepKind = WorkflowStepKind.ToolCall,
                OutputPath = "result.message", GuardExpression = "true", SucceedExpression = "true",
                Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
            }));
            var firstContext = new Dictionary<string, object?>(StringComparer.Ordinal) { ["request_kind"] = "single" };
            var secondContext = new Dictionary<string, object?>(StringComparer.Ordinal) { ["request_kind"] = "batch" };
            await new WorkflowFileExecutionService().RunAsync(workflowFile, firstContext, operationId: "run-op-2");
            var before = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new WorkflowFileExecutionService().RunAsync(workflowFile, secondContext, operationId: "run-op-2"));
            var after = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);

            Assert.Contains("different request", exception.Message, StringComparison.Ordinal);
            Assert.Equal(before.Version, after.Version);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task RunAsync_WithStartedOperationRefusesReplay()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-operation-indoubt-{Guid.NewGuid():N}.json");
        try
        {
            await CanonicalWorkflowFileStore.SaveAsync(workflowFile, CreateWorkflow("operation-indoubt", new CommandTransition
            {
                Id = "transition.echo", Name = "Echo", TargetNodeId = "state.done", StepKind = WorkflowStepKind.ToolCall,
                OutputPath = "result.message", GuardExpression = "true", SucceedExpression = "true",
                Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
            }));
            var requestHash = WorkflowOperationLedger.ComputeRequestHash(new { context = (Dictionary<string, object?>?)null });
            await WorkflowOperationLedger.BeginAsync(workflowFile, "run-op-3", "run", requestHash);

            await Assert.ThrowsAsync<WorkflowOperationInDoubtException>(() =>
                new WorkflowFileExecutionService().RunAsync(workflowFile, operationId: "run-op-3"));
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task LedgerCompletion_RequiresMatchingStartedRecordAndRejectsDuplicateCompletion()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ledger-integrity-{Guid.NewGuid():N}.json");
        var ledgerPath = WorkflowOperationLedger.GetPath(workflowFile);
        var requestHash = WorkflowOperationLedger.ComputeRequestHash(new { value = "one" });
        try
        {
            var missingStarted = await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowOperationLedger.CompleteRawAsync(
                workflowFile,
                "ledger-op-1",
                "run",
                requestHash,
                "{}"));
            Assert.Contains("no started record", missingStarted.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(ledgerPath));

            Assert.Null(await WorkflowOperationLedger.BeginRawAsync(workflowFile, "ledger-op-1", "run", requestHash));

            var wrongHash = await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowOperationLedger.CompleteRawAsync(
                workflowFile,
                "ledger-op-1",
                "run",
                WorkflowOperationLedger.ComputeRequestHash(new { value = "two" }),
                "{}"));
            Assert.Contains("does not match", wrongHash.Message, StringComparison.Ordinal);
            Assert.Single(await File.ReadAllLinesAsync(ledgerPath));

            await WorkflowOperationLedger.CompleteRawAsync(workflowFile, "ledger-op-1", "run", requestHash, "{}");
            var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowOperationLedger.CompleteRawAsync(
                workflowFile,
                "ledger-op-1",
                "run",
                requestHash,
                "{}"));
            Assert.Contains("already completed", duplicate.Message, StringComparison.Ordinal);
            Assert.Equal(2, (await File.ReadAllLinesAsync(ledgerPath)).Length);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task CompleteRawAsync_RejectsInvalidJsonBeforeAppendingCompletion()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ledger-json-{Guid.NewGuid():N}.json");
        var ledgerPath = WorkflowOperationLedger.GetPath(workflowFile);
        var requestHash = WorkflowOperationLedger.ComputeRequestHash(new { value = "json" });
        try
        {
            await WorkflowOperationLedger.BeginRawAsync(workflowFile, "ledger-op-json", "resume", requestHash);
            var error = await Assert.ThrowsAsync<ArgumentException>(() => WorkflowOperationLedger.CompleteRawAsync(
                workflowFile,
                "ledger-op-json",
                "resume",
                requestHash,
                "{invalid"));
            Assert.Contains("valid JSON", error.Message, StringComparison.Ordinal);
            Assert.Single(await File.ReadAllLinesAsync(ledgerPath));
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task LedgerCompletion_SerializesConcurrentCompletions()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ledger-concurrent-{Guid.NewGuid():N}.json");
        var ledgerPath = WorkflowOperationLedger.GetPath(workflowFile);
        var requestHash = WorkflowOperationLedger.ComputeRequestHash(new { value = "concurrent" });
        try
        {
            await WorkflowOperationLedger.BeginRawAsync(workflowFile, "ledger-op-concurrent", "run", requestHash);
            var first = WorkflowOperationLedger.CompleteRawAsync(workflowFile, "ledger-op-concurrent", "run", requestHash, "{}");
            var second = WorkflowOperationLedger.CompleteRawAsync(workflowFile, "ledger-op-concurrent", "run", requestHash, "{}");
            var failures = new System.Collections.Generic.List<Exception>();
            foreach (var completion in new[] { first, second })
            {
                try
                {
                    await completion;
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            var failure = Assert.Single(failures);
            Assert.Contains("already completed", failure.Message, StringComparison.Ordinal);
            Assert.Equal(2, (await File.ReadAllLinesAsync(ledgerPath)).Length);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task CompleteRawAsync_RejectsNonObjectJsonBeforeAppendingCompletion()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ledger-json-shape-{Guid.NewGuid():N}.json");
        var ledgerPath = WorkflowOperationLedger.GetPath(workflowFile);
        var requestHash = WorkflowOperationLedger.ComputeRequestHash(new { value = "shape" });
        try
        {
            await WorkflowOperationLedger.BeginRawAsync(workflowFile, "ledger-op-shape", "resume", requestHash);
            var error = await Assert.ThrowsAsync<ArgumentException>(() => WorkflowOperationLedger.CompleteRawAsync(
                workflowFile,
                "ledger-op-shape",
                "resume",
                requestHash,
                "[]"));
            Assert.Contains("JSON object", error.Message, StringComparison.Ordinal);
            Assert.Single(await File.ReadAllLinesAsync(ledgerPath));
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("C:/outside")]
    [InlineData("operation/id")]
    [InlineData("operation\nid")]
    public void ValidateOperationId_RejectsPathAndControlCharacters(string operationId)
    {
        Assert.Throws<ArgumentException>(() => WorkflowOperationLedger.ValidateOperationId(operationId));
    }

    [Fact]
    public void ValidateOperationId_RejectsValuesLongerThan128Characters()
    {
        Assert.Throws<ArgumentException>(() => WorkflowOperationLedger.ValidateOperationId(new string('x', 129)));
    }
    [Fact]
    public void ComputeRequestHash_CanonicalizesObjectsButPreservesArrayOrder()
    {
        var first = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["z"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["b"] = 2,
                ["a"] = 1,
            },
            ["items"] = new object?[] { "first", 2 },
        };
        var sameValuesInDifferentObjectOrder = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["items"] = new object?[] { "first", 2 },
            ["z"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["a"] = 1,
                ["b"] = 2,
            },
        };
        var differentArrayOrder = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["z"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["a"] = 1,
                ["b"] = 2,
            },
            ["items"] = new object?[] { 2, "first" },
        };

        Assert.Equal(
            WorkflowOperationLedger.ComputeRequestHash(first),
            WorkflowOperationLedger.ComputeRequestHash(sameValuesInDifferentObjectOrder));
        Assert.NotEqual(
            WorkflowOperationLedger.ComputeRequestHash(first),
            WorkflowOperationLedger.ComputeRequestHash(differentArrayOrder));
    }

    [Fact]
    public void ComputeRequestHash_OmitsOptionalNullMembersButRetainsEmptyContainers()
    {
        var missingOptionalMember = new { transitionId = "transition.plan" };
        var explicitNullOptionalMember = new { transitionId = "transition.plan", correlationKey = (string?)null };
        var emptyObjectMember = new { transitionId = "transition.plan", payload = new Dictionary<string, object?>() };
        var emptyArrayMember = new { transitionId = "transition.plan", payload = Array.Empty<object?>() };

        Assert.Equal(
            WorkflowOperationLedger.ComputeRequestHash(missingOptionalMember),
            WorkflowOperationLedger.ComputeRequestHash(explicitNullOptionalMember));
        Assert.NotEqual(
            WorkflowOperationLedger.ComputeRequestHash(explicitNullOptionalMember),
            WorkflowOperationLedger.ComputeRequestHash(emptyObjectMember));
        Assert.NotEqual(
            WorkflowOperationLedger.ComputeRequestHash(emptyObjectMember),
            WorkflowOperationLedger.ComputeRequestHash(emptyArrayMember));
    }
    private static async Task<WorkflowFileExecutionResult> ResumeWithActiveWaitIdAsync(
        WorkflowFileExecutionService service,
        string workflowFile,
        string transitionId,
        string? correlationKey = null,
        Dictionary<string, object?>? payload = null,
        string? resultId = null,
        string? operationId = null)
    {
        var instance = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
        var waitGroup = instance.ActiveWaitGroups.Single(group =>
            string.Equals(group.TransitionId, transitionId, StringComparison.Ordinal)
            && (correlationKey is null || string.Equals(group.CorrelationKey, correlationKey, StringComparison.Ordinal)));
        var entry = waitGroup.GetNextPendingEntry()
            ?? throw new InvalidOperationException($"Wait group '{transitionId}' has no pending entry.");
        return await service.ResumeAsync(
            workflowFile,
            transitionId,
            waitGroup.CorrelationKey,
            payload,
            resultId,
            operationId: operationId,
            waitId: entry.WaitId);
    }

    private static WorkflowInstance CreateFanoutWorkflow(
        string instanceId,
        TransitionGroup group,
        string endNodeId,
        params CommandTransition[] transitions)
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups = [group],
        };
        var stateIds = transitions
            .Select(static transition => transition.TargetNodeId)
            .Concat(new string?[] { group.JoinTargetStateId, group.NoPassTargetStateId, group.ErrorHandlingTargetStateId, group.TimeoutTargetStateId, endNodeId })
            .OfType<string>()
            .Append(endNodeId)
            .Distinct(StringComparer.Ordinal);
        var nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
        {
            [start.Id] = start,
        };
        foreach (var stateId in stateIds)
        {
            nodes[stateId] = new StateNode { Id = stateId, Name = stateId, Groups = [] };
        }
        foreach (var transition in transitions)
        {
            nodes[transition.Id] = transition;
        }

        return new WorkflowInstance
        {
            InstanceId = instanceId,
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = endNodeId,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = nodes,
        };
    }

    private static CommandTransition CreateFanoutTransition(string transitionId, string outputPath, string passExpression = "true")
    {
        return new CommandTransition
        {
            Id = transitionId,
            Name = transitionId,
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ModelThink,
            OutputPath = outputPath,
            PassExpression = passExpression,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "external",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { ["resumeOutputKey"] = "answer" },
            },
        };
    }

    private static WorkflowInstance CreateWorkflow(string instanceId, CommandTransition transition)
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "01 Start",
            Groups = [new TransitionGroup { Id = "group.start", Strategy = ConcurrencyStrategy.FirstSuccess, TransitionIds = [transition.Id] }],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "02 Done",
            Groups = [],
        };
        return new WorkflowInstance
        {
            InstanceId = instanceId,
            ContractBinding = SkillOrchestratorBehaviorTestBase.CreateTestContextContractBinding(),
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

    private sealed class TestClock(DateTimeOffset initialTime) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = initialTime;

        public void Advance(TimeSpan duration)
        {
            UtcNow = UtcNow.Add(duration);
        }
    }

    private static void DeleteWorkflowFiles(string workflowFile)
    {
        if (File.Exists(workflowFile))
        {
            File.Delete(workflowFile);
        }

        var operationsFile = WorkflowOperationLedger.GetPath(workflowFile);
        if (File.Exists(operationsFile))
        {
            File.Delete(operationsFile);
        }
        var lockFile = workflowFile + ".lock";
        if (File.Exists(lockFile))
        {
            File.Delete(lockFile);
        }

        var ledgerLockFile = operationsFile + ".lock";
        if (File.Exists(ledgerLockFile))
        {
            File.Delete(ledgerLockFile);
        }
    }
}