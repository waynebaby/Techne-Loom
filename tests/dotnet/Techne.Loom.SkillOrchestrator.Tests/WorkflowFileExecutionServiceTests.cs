using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowFileExecutionServiceTests
{
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
                    SucceedExpression = "context.Get<string>(\"result.message\") == \"hello\"",
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
                SucceedExpression = "context.Get<string>(\"plan.answer\") == \"approved\"",
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
                SucceedExpression = "context.Get<string>(\"plan.answer\") == \"approved\"",
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
                SucceedExpression = "context.Get<string>(\"result.message\") == \"hello\"",
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.Tool,
                    Name = "echo",
                    Parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { ["message"] = "hello" },
                },
            }));

            var first = await new WorkflowFileExecutionService().RunAsync(workflowFile, operationId: "run-op-1");
            var afterFirst = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var second = await new WorkflowFileExecutionService().RunAsync(workflowFile, operationId: "run-op-1");
            var afterSecond = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var ledger = await File.ReadAllLinesAsync(WorkflowOperationLedger.GetPath(workflowFile));

            Assert.Equal(first.Status, second.Status);
            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(afterFirst.Version, afterSecond.Version);
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
            var firstContext = new Dictionary<string, object?>(StringComparer.Ordinal) { ["request"] = "one" };
            var secondContext = new Dictionary<string, object?>(StringComparer.Ordinal) { ["request"] = "two" };
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