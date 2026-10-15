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
            SucceedExpression = "context.Get<string?>(\"result.message\") == \"hello\"",
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
    public async Task CliRunAndResume_WithWorkflowFileWaitsForAllFanoutErrorsBeforeRouting()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-fanout-errors-{Guid.NewGuid():N}.json");
        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-fanout-error-result-{Guid.NewGuid():N}.json");
        var first = CreateFanoutTransition("transition.error-first", "first_output") with { MaxRetries = 1 };
        var second = CreateFanoutTransition("transition.error-second", "second_output") with { MaxRetries = 0 };
        var workflow = CreateWorkflow("ao-fanout-error-route", first);
        var group = workflow.GetStateNodes()["state.start"].Groups[0];
        group.Strategy = ConcurrencyStrategy.WhenAllPass;
        group.JoinTargetStateId = "state.joined";
        group.NoPassTargetStateId = "state.no-pass";
        group.ErrorHandlingTargetStateId = "state.done";
        group.TransitionIds = [first.Id, second.Id];
        workflow.Nodes[second.Id] = second;
        workflow.Nodes["state.joined"] = new StateNode { Id = "state.joined", Name = "Joined", Groups = [] };
        workflow.Nodes["state.no-pass"] = new StateNode { Id = "state.no-pass", Name = "No pass", Groups = [] };
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);

        async Task ResumeExecutionError(string transitionId, PendingWaitGroup waitGroup, string error, int expectedExit)
        {
            var pendingEntry = Assert.Single(waitGroup.Entries, static entry => !entry.Completed);
            await File.WriteAllTextAsync(resultFile, JsonSerializer.Serialize(new
            {
                transition_id = transitionId,
                correlation_key = waitGroup.CorrelationKey,
                payload = (Dictionary<string, object?>?)null,
                execution_error = error,
                wait_id = pendingEntry.WaitId,
            }));
            var resumed = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --result-file \"{resultFile}\"");
            Assert.Equal(expectedExit, resumed.ExitCode);
        }

        try
        {
            var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowFile}\"");
            Assert.Equal(3, run.ExitCode);
            var initial = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.WaitingExternal, initial.Status);
            var firstWait = Assert.Single(initial.ActiveWaitGroups, waitGroup => waitGroup.TransitionId == first.Id);
            await ResumeExecutionError(first.Id, firstWait, "first attempt failed", 3);

            var afterFirst = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.WaitingExternal, afterFirst.Status);
            Assert.Equal("state.start", afterFirst.CurrentNodeId);
            Assert.Equal(1, Assert.IsType<CommandTransition>(afterFirst.Nodes[first.Id]).CurrentRetryCount);
            Assert.Single(afterFirst.ActiveWaitGroups, waitGroup => waitGroup.TransitionId == first.Id);
            Assert.Single(afterFirst.ActiveWaitGroups, waitGroup => waitGroup.TransitionId == second.Id);

            var secondWait = afterFirst.ActiveWaitGroups.Single(waitGroup => waitGroup.TransitionId == second.Id);
            await ResumeExecutionError(second.Id, secondWait, "second branch exhausted", 3);
            var afterSecond = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.WaitingExternal, afterSecond.Status);
            Assert.Equal("state.start", afterSecond.CurrentNodeId);
            Assert.Equal(1, Assert.IsType<CommandTransition>(afterSecond.Nodes[second.Id]).CurrentRetryCount);
            Assert.Equal(2, afterSecond.ActiveWaitGroups.Count);
            Assert.True(afterSecond.ActiveWaitGroups.Single(waitGroup => waitGroup.TransitionId == second.Id).Completed);
            Assert.Single(afterSecond.ActiveWaitGroups, waitGroup => waitGroup.TransitionId == first.Id && !waitGroup.Completed);

            var firstRetryWait = afterSecond.ActiveWaitGroups.Single(waitGroup => waitGroup.TransitionId == first.Id);
            await ResumeExecutionError(first.Id, firstRetryWait, "first retry exhausted", 0);
            var completed = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.Succeeded, completed.Status);
            Assert.Equal("state.done", completed.CurrentNodeId);
            Assert.Empty(completed.ActiveWaitGroups);
            Assert.Equal(2, Assert.IsType<CommandTransition>(completed.Nodes[first.Id]).CurrentRetryCount);
            Assert.Equal(1, Assert.IsType<CommandTransition>(completed.Nodes[second.Id]).CurrentRetryCount);
            Assert.False(completed.Context.ContainsKey("first_output"));
            Assert.False(completed.Context.ContainsKey("second_output"));
            Assert.Contains(completed.History, entry => entry.NodeId == group.Id
                && entry.ContextChanges?.ContainsKey("executionError") == true);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
            if (File.Exists(resultFile)) File.Delete(resultFile);
        }
    }

    [Fact]
    public async Task CliRunAndResume_WithWorkflowFileLetsPassingBranchWinAfterSiblingError()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-any-pass-error-{Guid.NewGuid():N}.json");
        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-any-pass-result-{Guid.NewGuid():N}.json");
        var first = CreateFanoutTransition("transition.error-first", "first_output") with { MaxRetries = 0 };
        var second = CreateFanoutTransition("transition.pass-second", "second_output") with { PassExpression = "context.Get<string>(\"second_output\") == \"ok\"" };
        var workflow = CreateWorkflow("ao-any-pass-error-order", first);
        var group = workflow.GetStateNodes()["state.start"].Groups[0];
        group.Strategy = ConcurrencyStrategy.WhenAnyPass;
        group.NoPassTargetStateId = "state.no-pass";
        group.ErrorHandlingTargetStateId = "state.error";
        group.TransitionIds = [first.Id, second.Id];
        workflow.Nodes[second.Id] = second;
        workflow.Nodes["state.no-pass"] = new StateNode { Id = "state.no-pass", Name = "No pass", Groups = [] };
        workflow.Nodes["state.error"] = new StateNode { Id = "state.error", Name = "Error", Groups = [] };
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);

        async Task<int> ResumeAsync(string transitionId, PendingWaitGroup waitGroup, Dictionary<string, object?>? payload, string? error)
        {
            var pendingEntry = Assert.Single(waitGroup.Entries, static entry => !entry.Completed);
            await File.WriteAllTextAsync(resultFile, JsonSerializer.Serialize(new
            {
                transition_id = transitionId,
                correlation_key = waitGroup.CorrelationKey,
                payload,
                execution_error = error,
                wait_id = pendingEntry.WaitId,
            }));
            return (await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --result-file \"{resultFile}\"")).ExitCode;
        }

        try
        {
            Assert.Equal(3, (await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowFile}\"")).ExitCode);
            var waiting = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var firstWait = waiting.ActiveWaitGroups.Single(waitGroup => waitGroup.TransitionId == first.Id);
            Assert.Equal(3, await ResumeAsync(first.Id, firstWait, null, "first branch failed"));

            var afterError = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.WaitingExternal, afterError.Status);
            var secondWait = afterError.ActiveWaitGroups.Single(waitGroup => waitGroup.TransitionId == second.Id);
            Assert.Equal(0, await ResumeAsync(second.Id, secondWait, new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "ok" }, null));

            var completed = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.Succeeded, completed.Status);
            Assert.Equal("state.done", completed.CurrentNodeId);
            Assert.Equal("ok", PathValueAccessor.GetValue(completed.Context, "second_output"));
            Assert.False(completed.Context.ContainsKey("first_output"));
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
            if (File.Exists(resultFile)) File.Delete(resultFile);
        }
    }

    [Fact]
    public async Task CliRunAndResume_WithWorkflowFileWaitsForAllWhenAllPassBranchesBeforeNoPass()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-all-pass-no-pass-{Guid.NewGuid():N}.json");
        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-all-pass-result-{Guid.NewGuid():N}.json");
        var first = CreateFanoutTransition("transition.fail-first", "first_output") with { PassExpression = "context.Get<string>(\"first_output\") == \"pass\"" };
        var second = CreateFanoutTransition("transition.pass-second", "second_output") with { PassExpression = "context.Get<string>(\"second_output\") == \"pass\"" };
        var workflow = CreateWorkflow("ao-all-pass-waits-before-no-pass", first);
        var group = workflow.GetStateNodes()["state.start"].Groups[0];
        group.Strategy = ConcurrencyStrategy.WhenAllPass;
        group.JoinTargetStateId = "state.joined";
        group.NoPassTargetStateId = "state.done";
        group.ErrorHandlingTargetStateId = "state.error";
        group.TransitionIds = [first.Id, second.Id];
        workflow.Nodes[second.Id] = second;
        workflow.Nodes["state.joined"] = new StateNode { Id = "state.joined", Name = "Joined", Groups = [] };
        workflow.Nodes["state.error"] = new StateNode { Id = "state.error", Name = "Error", Groups = [] };
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);

        async Task<int> ResumeAsync(string transitionId, PendingWaitGroup waitGroup, string answer)
        {
            var pendingEntry = Assert.Single(waitGroup.Entries, static entry => !entry.Completed);
            await File.WriteAllTextAsync(resultFile, JsonSerializer.Serialize(new
            {
                transition_id = transitionId,
                correlation_key = waitGroup.CorrelationKey,
                payload = new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = answer },
                wait_id = pendingEntry.WaitId,
            }));
            return (await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --result-file \"{resultFile}\"")).ExitCode;
        }

        try
        {
            Assert.Equal(3, (await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowFile}\"")).ExitCode);
            var waiting = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var firstWait = waiting.ActiveWaitGroups.Single(waitGroup => waitGroup.TransitionId == first.Id);
            Assert.Equal(3, await ResumeAsync(first.Id, firstWait, "fail"));

            var afterFirst = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.WaitingExternal, afterFirst.Status);
            Assert.Equal("state.start", afterFirst.CurrentNodeId);
            Assert.Equal(2, afterFirst.ActiveWaitGroups.Count);
            var secondWait = afterFirst.ActiveWaitGroups.Single(waitGroup => waitGroup.TransitionId == second.Id);
            Assert.Equal(0, await ResumeAsync(second.Id, secondWait, "pass"));

            var completed = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.Succeeded, completed.Status);
            Assert.Equal("state.done", completed.CurrentNodeId);
            Assert.Empty(completed.ActiveWaitGroups);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
            if (File.Exists(resultFile)) File.Delete(resultFile);
        }
    }

    [Fact]
    public async Task CliRunAndResume_WithWorkflowFileResetsFanoutRetryBudgetOnStateReentry()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-fanout-retry-cycle-{Guid.NewGuid():N}.json");
        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-fanout-retry-cycle-result-{Guid.NewGuid():N}.json");
        var transition = CreateFanoutTransition("transition.looping", "loop_output") with
        {
            MaxRetries = 1,
            TargetNodeId = "state.start",
        };
        var workflow = CreateWorkflow("ao-fanout-retry-cycle", transition);
        var group = workflow.GetStateNodes()["state.start"].Groups[0];
        group.Strategy = ConcurrencyStrategy.WhenAnyPass;
        group.NoPassTargetStateId = "state.done";
        group.ErrorHandlingTargetStateId = "state.error";
        workflow.Nodes["state.error"] = new StateNode { Id = "state.error", Name = "Error", Groups = [] };
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);

        async Task<int> ResumeAsync(PendingWaitGroup waitGroup, Dictionary<string, object?>? payload, string? error)
        {
            var pendingEntry = Assert.Single(waitGroup.Entries, static entry => !entry.Completed);
            await File.WriteAllTextAsync(resultFile, JsonSerializer.Serialize(new
            {
                transition_id = transition.Id,
                correlation_key = waitGroup.CorrelationKey,
                payload,
                execution_error = error,
                wait_id = pendingEntry.WaitId,
            }));
            return (await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --result-file \"{resultFile}\"")).ExitCode;
        }

        try
        {
            Assert.Equal(3, (await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowFile}\"")).ExitCode);
            var firstCycle = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var firstWait = Assert.Single(firstCycle.ActiveWaitGroups);
            Assert.Equal(3, await ResumeAsync(firstWait, null, "first cycle failed"));
            var firstRetry = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(1, Assert.IsType<CommandTransition>(firstRetry.Nodes[transition.Id]).CurrentRetryCount);
            var firstRetryWait = Assert.Single(firstRetry.ActiveWaitGroups);
            Assert.Equal(3, await ResumeAsync(firstRetryWait, new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "ok" }, null));

            var secondCycle = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.WaitingExternal, secondCycle.Status);
            Assert.Equal("state.start", secondCycle.CurrentNodeId);
            Assert.Equal(0, Assert.IsType<CommandTransition>(secondCycle.Nodes[transition.Id]).CurrentRetryCount);
            var secondWait = Assert.Single(secondCycle.ActiveWaitGroups);
            Assert.Equal(3, await ResumeAsync(secondWait, null, "second cycle failed"));

            var secondRetry = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.WaitingExternal, secondRetry.Status);
            Assert.Equal(1, Assert.IsType<CommandTransition>(secondRetry.Nodes[transition.Id]).CurrentRetryCount);
            Assert.Single(secondRetry.ActiveWaitGroups.Single().Entries, static entry => !entry.Completed);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
            if (File.Exists(resultFile)) File.Delete(resultFile);
        }
    }

    [Fact]
    public async Task CliRunAndResume_WithWorkflowFileWaitsForAllBranchesAfterWhenExpressionError()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-when-expression-error-{Guid.NewGuid():N}.json");
        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-when-expression-result-{Guid.NewGuid():N}.json");
        var first = CreateFanoutTransition("transition.when-expression-first", "candidate.first");
        var second = CreateFanoutTransition("transition.when-expression-second", "candidate.second");
        var workflow = CreateWorkflow("ao-when-expression-partial-error", first);
        workflow.Context["zero"] = 0;
        var group = workflow.GetStateNodes()["state.start"].Groups[0];
        group.Strategy = ConcurrencyStrategy.WhenExpression;
        group.PassExpression = "context.Get<int>(\"zero\") / context.Get<int>(\"zero\") == 1";
        group.JoinTargetStateId = "state.joined";
        group.NoPassTargetStateId = "state.no-pass";
        group.ErrorHandlingTargetStateId = "state.done";
        group.TransitionIds = [first.Id, second.Id];
        workflow.Nodes[second.Id] = second;
        workflow.Nodes["state.joined"] = new StateNode { Id = "state.joined", Name = "Joined", Groups = [] };
        workflow.Nodes["state.no-pass"] = new StateNode { Id = "state.no-pass", Name = "No pass", Groups = [] };
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);

        async Task<int> ResumeAsync(string transitionId, PendingWaitGroup waitGroup)
        {
            var pendingEntry = Assert.Single(waitGroup.Entries, static entry => !entry.Completed);
            await File.WriteAllTextAsync(resultFile, JsonSerializer.Serialize(new
            {
                transition_id = transitionId,
                correlation_key = waitGroup.CorrelationKey,
                payload = new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "candidate" },
                wait_id = pendingEntry.WaitId,
            }));
            return (await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --result-file \"{resultFile}\"")).ExitCode;
        }

        try
        {
            Assert.Equal(3, (await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowFile}\"")).ExitCode);
            var waiting = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var firstWait = waiting.ActiveWaitGroups.Single(waitGroup => waitGroup.TransitionId == first.Id);
            Assert.Equal(3, await ResumeAsync(first.Id, firstWait));

            var afterFirst = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.WaitingExternal, afterFirst.Status);
            Assert.Equal("state.start", afterFirst.CurrentNodeId);
            Assert.Equal(2, afterFirst.ActiveWaitGroups.Count);
            Assert.False(afterFirst.Context.ContainsKey("candidate"));

            var secondWait = afterFirst.ActiveWaitGroups.Single(waitGroup => waitGroup.TransitionId == second.Id);
            Assert.Equal(0, await ResumeAsync(second.Id, secondWait));
            var completed = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(WorkflowStatus.Succeeded, completed.Status);
            Assert.Equal("state.done", completed.CurrentNodeId);
            Assert.Empty(completed.ActiveWaitGroups);
            Assert.False(completed.Context.ContainsKey("candidate"));
            Assert.Contains(completed.History, entry => entry.NodeId == group.Id
                && entry.ContextChanges?.ContainsKey("executionError") == true);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
            if (File.Exists(resultFile)) File.Delete(resultFile);
        }
    }

    [Fact]
    public async Task CliRunAndResume_WithWorkflowFileProjectsFanoutCancellationSignals()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-fanout-{Guid.NewGuid():N}.json");
        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-fanout-result-{Guid.NewGuid():N}.json");
        var first = CreateFanoutTransition("transition.first", "result.answer");
        var second = CreateFanoutTransition("transition.second", "result.answer");
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "01 Start",
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
        var done = new StateNode { Id = "state.done", Name = "Done", WorkflowPhase = "02 Done", Groups = [] };
        var instance = new WorkflowInstance
        {
            InstanceId = "ao-cli-fanout",
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
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, instance);

        try
        {
            var blocked = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowFile}\"");
            Assert.Equal(3, blocked.ExitCode);
            var initial = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var secondWait = initial.ActiveWaitGroups.Single(waitGroup => waitGroup.TransitionId == second.Id);
            var firstWait = initial.ActiveWaitGroups.Single(waitGroup => waitGroup.TransitionId == first.Id);
            var firstPendingEntry = Assert.Single(firstWait.Entries, static entry => !entry.Completed);
            var pendingEntry = Assert.Single(secondWait.Entries, static entry => !entry.Completed);

            await File.WriteAllTextAsync(resultFile, JsonSerializer.Serialize(new
            {
                transition_id = second.Id,
                correlation_key = secondWait.CorrelationKey,
                payload = new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "second" },
            }));
            var missingWaitId = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --result-file \"{resultFile}\"");
            Assert.NotEqual(0, missingWaitId.ExitCode);
            Assert.Contains("wait_id", missingWaitId.StdOut + missingWaitId.StdErr, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(WorkflowStatus.WaitingExternal, (await CanonicalWorkflowFileStore.LoadAsync(workflowFile)).Status);

            await File.WriteAllTextAsync(resultFile, JsonSerializer.Serialize(new
            {
                transition_id = second.Id,
                correlation_key = secondWait.CorrelationKey,
                payload = new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "second" },
                wait_id = pendingEntry.WaitId,
            }));
            var resumed = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --result-file \"{resultFile}\"");
            Assert.Equal(0, resumed.ExitCode);
            const string startTag = "<ao_property>";
            const string endTag = "</ao_property>";
            var startIndex = resumed.StdOut.IndexOf(startTag, StringComparison.Ordinal);
            var endIndex = resumed.StdOut.IndexOf(endTag, startIndex + startTag.Length, StringComparison.Ordinal);
            using var envelope = JsonDocument.Parse(resumed.StdOut.Substring(startIndex + startTag.Length, endIndex - startIndex - startTag.Length).Trim());
            var signal = Assert.Single(envelope.RootElement.GetProperty("payload").GetProperty("fanout_cancellation_signals").EnumerateArray().ToArray());
            Assert.Equal(first.Id, signal.GetProperty("transitionId").GetString());
            Assert.Equal("fanout_completed", signal.GetProperty("reason").GetString());
            var completed = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            var versionAfterWinner = completed.Version;
            await File.WriteAllTextAsync(resultFile, JsonSerializer.Serialize(new
            {
                transition_id = first.Id,
                correlation_key = firstWait.CorrelationKey,
                payload = new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "late" },
                wait_id = firstPendingEntry.WaitId,
            }));
            var ignoredLateResult = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --result-file \"{resultFile}\"");
            Assert.Equal(0, ignoredLateResult.ExitCode);
            var afterLateResult = await CanonicalWorkflowFileStore.LoadAsync(workflowFile);
            Assert.Equal(versionAfterWinner, afterLateResult.Version);
            Assert.Equal("second", PathValueAccessor.GetValue(afterLateResult.Context, "result.answer"));
            Assert.Single(afterLateResult.FanoutCancellationSignals);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
            if (File.Exists(resultFile)) File.Delete(resultFile);
        }
    }

    [Fact]
    public async Task CliCompile_FanoutWithContractRefsIsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-fanout-contract-refs-{Guid.NewGuid():N}.json");
        var transition = CreateFanoutTransition("transition.contract-ref", "contract_output") with { ContractRefs = ["/rules"] };
        var workflow = CreateWorkflow("ao-fanout-contract-refs", transition);
        workflow.ContractBinding = new ContractBinding();
        workflow.GetStateNodes()["state.start"].Groups[0].Strategy = ConcurrencyStrategy.WhenAnyResponse;
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);

        try
        {
            var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

            Assert.Equal(2, run.ExitCode);
            Assert.Contains("contractRefs in fan-out mode", run.StdOut, StringComparison.Ordinal);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task CliCompile_WhenAllPassRejectsOverlappingBranchOutputPaths()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-overlapping-fanout-output-{Guid.NewGuid():N}.json");
        var first = CreateFanoutTransition("transition.overlap-first", "shared.result");
        var second = CreateFanoutTransition("transition.overlap-second", "shared.result.child");
        var workflow = CreateWorkflow("ao-overlapping-fanout-output", first);
        var group = workflow.GetStateNodes()["state.start"].Groups[0];
        group.Strategy = ConcurrencyStrategy.WhenAllPass;
        group.JoinTargetStateId = "state.done";
        group.TransitionIds = [first.Id, second.Id];
        workflow.Nodes[second.Id] = second;
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);

        try
        {
            var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

            Assert.Equal(2, run.ExitCode);
            Assert.Contains("overlapping output projections", run.StdOut, StringComparison.Ordinal);
            Assert.Contains(first.Id, run.StdOut, StringComparison.Ordinal);
            Assert.Contains(second.Id, run.StdOut, StringComparison.Ordinal);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Theory]
    [InlineData("noPassTargetStateId")]
    [InlineData("errorHandlingTargetStateId")]
    public async Task CliCompile_FirstPassRejectsFanoutOnlyRoutes(string routeName)
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-first-pass-route-{Guid.NewGuid():N}.json");
        var transition = CreateFanoutTransition("transition.first-pass-route", "route_output");
        var workflow = CreateWorkflow("ao-first-pass-route", transition);
        var group = workflow.GetStateNodes()["state.start"].Groups[0];
        if (routeName == "noPassTargetStateId")
        {
            group.NoPassTargetStateId = "state.done";
        }
        else
        {
            group.ErrorHandlingTargetStateId = "state.done";
        }
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);

        try
        {
            var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

            Assert.Equal(2, run.ExitCode);
        Assert.Contains(routeName, run.StdOut, StringComparison.Ordinal);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task CliCompile_SynchronousTransitionInFanoutGroupIsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-sync-fanout-{Guid.NewGuid():N}.json");
        var transition = CreateFanoutTransition("transition.sync", "sync_output") with { StepKind = WorkflowStepKind.StateUpdate };
        var workflow = CreateWorkflow("ao-sync-fanout", transition);
        workflow.GetStateNodes()["state.start"].Groups[0].Strategy = ConcurrencyStrategy.WhenAnyResponse;
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);

        try
        {
            var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

            Assert.Equal(2, run.ExitCode);
            Assert.Contains("fan-out group", run.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("requires external transitions only", run.StdOut, StringComparison.Ordinal);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    [Fact]
    public async Task CliCompile_TimeoutTransitionFallbackTargetIsValidated()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-timeout-fallback-{Guid.NewGuid():N}.json");
        var transition = CreateFanoutTransition("transition.timeout", "timeout_output");
        var workflow = CreateWorkflow("ao-timeout-fallback", transition);
        var group = workflow.GetStateNodes()["state.start"].Groups[0];
        group.Strategy = ConcurrencyStrategy.WhenAnyResponse;
        group.TimeoutTransition = new CommandTransition
        {
            Id = "transition.timeout-fallback",
            Name = "Invalid timeout fallback",
            TargetNodeId = "state.missing",
        };
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, workflow);

        try
        {
            var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

            Assert.Equal(2, run.ExitCode);
            Assert.Contains("timeoutTargetStateId", run.StdOut, StringComparison.Ordinal);
            Assert.Contains("state.missing", run.StdOut, StringComparison.Ordinal);
        }
        finally
        {
            DeleteWorkflowFiles(workflowFile);
        }
    }

    private static CommandTransition CreateFanoutTransition(string transitionId, string outputPath)
    {
        return new CommandTransition
        {
            Id = transitionId,
            Name = transitionId,
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ModelThink,
            OutputPath = outputPath,
            PassExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "external",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { ["resumeOutputKey"] = "answer" },
            },
        };
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
            SucceedExpression = "context.Get<string?>(\"plan.answer\") == \"approved\"",
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
            Assert.True(resume.ExitCode == 0, resume.StdOut + resume.StdErr);
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
            ContractBinding = new ContractBinding
            {
                Path = "assets/so-workflow/contract.json",
                Format = "json",
                AssetRootPath = Path.Combine(
                    FindRepositoryRoot(),
                    "tests",
                    "dotnet",
                    "Techne.Loom.AgentOrchestrator.Tests",
                    "Fixtures",
                    "PromptReplanSkill"),
            },
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