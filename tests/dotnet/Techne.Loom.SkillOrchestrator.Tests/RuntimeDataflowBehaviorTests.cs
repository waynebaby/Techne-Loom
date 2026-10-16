using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Analysis;
using Techne.Loom.SkillOrchestrator.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class RuntimeDataflowBehaviorTests
{
    [Fact]
    public void WorkflowJsonSerializer_KeepsSemanticVersionStringsAsStrings()
    {
        var json = "{\"context\":{\"version\":\"1.2.3\",\"timestamp\":\"2026-08-24T05:34:05+00:00\"}}";
        var instance = System.Text.Json.JsonSerializer.Deserialize<WorkflowInstance>(json, WorkflowJsonSerializer.CreateDefaultOptions());

        Assert.NotNull(instance);
        Assert.IsType<string>(instance!.Context["version"]);
        Assert.IsType<DateTimeOffset>(instance.Context["timestamp"]);
    }
    [Fact]
    public async Task PlanStep_IsPersistedAsAnExternalBoundary()
    {
        var transition = new CommandTransition
        {
            Id = "transition.plan",
            Name = "Plan next route",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.Plan,
            Plan = new PlanStepContract
            {
                InputPaths = ["objective"],
                ResultFile = "plan.result.json",
                RequiredEvidence = ["plan.evidence"],
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
            Groups = [new TransitionGroup { Id = "group.start", TransitionIds = [transition.Id] }],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "plan-boundary",
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
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var result = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(result.Suspended);
        Assert.False(result.Failed);
        var persisted = await store.GetAsync(instance.InstanceId);
        Assert.NotNull(persisted);
        Assert.Equal(WorkflowStatus.WaitingExternal, persisted!.Status);
        var pending = Assert.Single(persisted.ActiveWaitGroups);
        Assert.Equal(transition.Id, pending.TransitionId);
        Assert.Equal(WorkflowStepKind.Plan, transition.StepKind);

        var resumed = await service.ResumeAsync(instance.InstanceId, transition.Id, resultId: "so-plan-result-1");
        Assert.Equal(WorkflowStatus.Running, resumed.Status);
        var terminal = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.Equal(WorkflowStatus.Succeeded, terminal.StatusProjection.Status);
        var versionBeforeDuplicate = (await store.GetAsync(instance.InstanceId))!.Version;
        var duplicate = await service.ResumeAsync(instance.InstanceId, transition.Id, resultId: "so-plan-result-1");
        Assert.Equal(WorkflowStatus.Succeeded, duplicate.Status);
        Assert.Equal(versionBeforeDuplicate, (await store.GetAsync(instance.InstanceId))!.Version);
    }

    [Fact]
    public async Task FirstPassExecutesReadyTransitionsInAscendingPriority()
    {
        var highPriority = new ExpressionTransition
        {
            Id = "transition.high-priority",
            Name = "High priority",
            TargetNodeId = "state.high-priority",
            Priority = 200,
            GuardExpression = "true",
            SucceedExpression = "true",
        };
        var lowPriority = new ExpressionTransition
        {
            Id = "transition.low-priority",
            Name = "Low priority",
            TargetNodeId = "state.low-priority",
            Priority = 100,
            GuardExpression = "true",
            SucceedExpression = "true",
        };
        var middlePriority = new ExpressionTransition
        {
            Id = "transition.middle-priority",
            Name = "Middle priority",
            TargetNodeId = "state.middle-priority",
            Priority = 150,
            GuardExpression = "true",
            SucceedExpression = "true",
        };
        var start = new StateNode
        {
            Id = "state.priority-start",
            Name = "Priority start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.priority-order",
                    TransitionIds = [highPriority.Id, lowPriority.Id, middlePriority.Id],
                },
            ],
        };
        var highState = new StateNode { Id = highPriority.TargetNodeId, Name = highPriority.Name, Groups = [] };
        var lowState = new StateNode { Id = lowPriority.TargetNodeId, Name = lowPriority.Name, Groups = [] };
        var middleState = new StateNode { Id = middlePriority.TargetNodeId, Name = middlePriority.Name, Groups = [] };
        var instance = new WorkflowInstance
        {
            InstanceId = "first-pass-priority-order",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = lowState.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [highState.Id] = highState,
                [lowState.Id] = lowState,
                [middleState.Id] = middleState,
                [highPriority.Id] = highPriority,
                [lowPriority.Id] = lowPriority,
                [middlePriority.Id] = middlePriority,
            },
        };
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        await service.StartOrAdvanceAsync(instance.InstanceId);

        var completed = await store.GetAsync(instance.InstanceId);
        Assert.NotNull(completed);
        Assert.Equal(lowState.Id, completed!.CurrentNodeId);
    }

    [Theory]
    [InlineData(true, "state.single-all-join")]
    [InlineData(false, "state.single-no-pass")]
    public async Task WhenAllPass_WithOneEligibleBranchUsesGroupOutcome(bool branchPasses, string expectedTarget)
    {
        var active = CreateSingleEligibleFanoutTransition("transition.single-all-active", "result.active", branchPasses ? "true" : "false");
        var excluded = CreateSingleEligibleFanoutTransition("transition.single-all-excluded", "result.excluded", "true", "false");
        var group = new TransitionGroup
        {
            Id = "group.single-all-pass",
            Strategy = ConcurrencyStrategy.WhenAllPass,
            JoinTargetStateId = "state.single-all-join",
            NoPassTargetStateId = "state.single-no-pass",
            TransitionIds = [active.Id, excluded.Id],
        };
        var instance = CreateSingleEligibleFanoutInstance("single-all-pass", group, expectedTarget, active, excluded);
        var service = await StartFanoutWorkflowAsync(instance);
        var waiting = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(waiting);
        var pending = Assert.Single(waiting!.ActiveWaitGroups);
        Assert.Equal(group.Id, pending.ConcurrencyGroupId);
        Assert.Equal(new[] { active.Id }, pending.ExpectedTransitionIds.ToArray());

        await ResumeWithActiveWaitIdAsync(service, instance.InstanceId, active.Id, payload: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["answer"] = "response",
        });
        var completed = await service.GetInstanceAsync(instance.InstanceId);

        Assert.NotNull(completed);
        Assert.Equal(WorkflowStatus.Running, completed!.Status);
        Assert.Equal(expectedTarget, completed.CurrentNodeId);
        Assert.Empty(completed.ActiveWaitGroups);
    }

    [Theory]
    [InlineData("accepted", "state.single-expression-join")]
    [InlineData("rejected", "state.single-expression-no-pass")]
    public async Task WhenExpression_WithOneEligibleBranchEvaluatesGroupPredicate(string answer, string expectedTarget)
    {
        var active = CreateSingleEligibleFanoutTransition("transition.single-expression-active", "result.active", "true");
        var excluded = CreateSingleEligibleFanoutTransition("transition.single-expression-excluded", "result.excluded", "true", "false");
        var group = new TransitionGroup
        {
            Id = "group.single-expression",
            Strategy = ConcurrencyStrategy.WhenExpression,
            PassExpression = "context.Get<string>(\"result.active\") == \"accepted\"",
            JoinTargetStateId = "state.single-expression-join",
            NoPassTargetStateId = "state.single-expression-no-pass",
            TransitionIds = [active.Id, excluded.Id],
        };
        var instance = CreateSingleEligibleFanoutInstance("single-expression", group, expectedTarget, active, excluded);
        var service = await StartFanoutWorkflowAsync(instance);
        var waiting = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(waiting);
        var pending = Assert.Single(waiting!.ActiveWaitGroups);
        Assert.Equal(group.Id, pending.ConcurrencyGroupId);
        Assert.Equal(new[] { active.Id }, pending.ExpectedTransitionIds.ToArray());

        await ResumeWithActiveWaitIdAsync(service, instance.InstanceId, active.Id, payload: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["answer"] = answer,
        });
        var completed = await service.GetInstanceAsync(instance.InstanceId);

        Assert.NotNull(completed);
        Assert.Equal(WorkflowStatus.Running, completed!.Status);
        Assert.Equal(expectedTarget, completed.CurrentNodeId);
        Assert.Empty(completed.ActiveWaitGroups);
    }

    [Theory]
    [InlineData(ConcurrencyStrategy.WhenAnyResponse)]
    [InlineData(ConcurrencyStrategy.WhenAnyPass)]
    [InlineData(ConcurrencyStrategy.WhenAllPass)]
    [InlineData(ConcurrencyStrategy.WhenExpression)]
    public async Task SynchronousFanoutBranchFailsClosedInBothExecutors(ConcurrencyStrategy strategy)
    {
        var sharedInstance = CreateSynchronousFanoutWorkflow($"shared-sync-fanout-{strategy}", strategy);
        var sharedOutcome = await new WorkflowExecutionCore().RunUntilBoundaryAsync(sharedInstance);

        Assert.True(sharedOutcome.Failed);
        Assert.Empty(sharedInstance.ActiveWaitGroups);
        Assert.Contains("requires external transitions only", sharedOutcome.ErrorMessage, StringComparison.Ordinal);

        var skillInstance = CreateSynchronousFanoutWorkflow($"so-sync-fanout-{strategy}", strategy);
        var skillOutcome = await new DefaultTaskTrackingEngine(new InMemoryInstanceStore()).TickAsync(skillInstance);

        Assert.True(skillOutcome.Failed);
        Assert.Empty(skillInstance.ActiveWaitGroups);
        Assert.Contains("requires external transitions only", skillOutcome.ErrorMessage, StringComparison.Ordinal);
    }

    private static WorkflowInstance CreateSynchronousFanoutWorkflow(string instanceId, ConcurrencyStrategy strategy)
    {
        var done = new StateNode { Id = "state.done", Name = "Done", Groups = [] };
        var transition = new CommandTransition
        {
            Id = "transition.sync",
            Name = "Synchronous transition",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.StateUpdate,
            GuardExpression = new ExpressionDefinition { Source = "true" },
            PassExpression = new ExpressionDefinition { Source = "true" },
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var group = new TransitionGroup
        {
            Id = "group.fanout-sync",
            Strategy = strategy,
            JoinTargetStateId = strategy is ConcurrencyStrategy.WhenAllPass or ConcurrencyStrategy.WhenExpression ? done.Id : null,
            PassExpression = strategy == ConcurrencyStrategy.WhenExpression ? new ExpressionDefinition { Source = "true" } : null,
            TransitionIds = [transition.Id],
        };
        var start = new StateNode { Id = "state.start", Name = "Start", Groups = [group] };
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

    private static CommandTransition CreateSingleEligibleFanoutTransition(
        string id,
        string outputPath,
        string passExpression,
        string guardExpression = "true") => new()
    {
        Id = id,
        Name = id,
        TargetNodeId = "state.branch-done",
        StepKind = WorkflowStepKind.SubagentCall,
        OutputPath = outputPath,
        GuardExpression = guardExpression,
        PassExpression = passExpression,
        Command = new CommandInvocation
        {
            Kind = CommandInvocationKind.Tool,
            Name = "noop",
            Parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { ["resumeOutputKey"] = "answer" },
        },
    };

    private static WorkflowInstance CreateSingleEligibleFanoutInstance(
        string instanceId,
        TransitionGroup group,
        string endNodeId,
        params CommandTransition[] transitions)
    {
        var start = new StateNode { Id = "state.start", Name = "Start", Groups = [group] };
        var nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal) { [start.Id] = start };
        foreach (var stateId in new[] { "state.branch-done", "state.single-all-join", "state.single-no-pass", "state.single-expression-join", "state.single-expression-no-pass", endNodeId }.Distinct(StringComparer.Ordinal))
        {
            nodes[stateId] = new StateNode { Id = stateId, Name = stateId, Groups = [] };
        }
        foreach (var transition in transitions) { nodes[transition.Id] = transition; }
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

    [Fact]
    public async Task AllExternalTransitions_RunAsOneParallelBatchAndJoinBeforeMoving()
    {
        var first = new CommandTransition
        {
            Id = "transition.parallel-first",
            Name = "First parallel review",
            TargetNodeId = "state.parallel-done",
            OutputPath = "first_result",
            StepKind = WorkflowStepKind.SubagentCall,
            SucceedExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
            },
        };
        var second = new CommandTransition
        {
            Id = "transition.parallel-second",
            Name = "Second parallel review",
            TargetNodeId = "state.parallel-done",
            OutputPath = "second_result",
            StepKind = WorkflowStepKind.SubagentCall,
            SucceedExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
            },
        };
        var start = new StateNode
        {
            Id = "state.parallel-start",
            Name = "Parallel start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.parallel-reviews",
                    Strategy = ConcurrencyStrategy.WhenAllPass,
                    TransitionIds = [first.Id, second.Id],
                },
            ],
        };
        var done = new StateNode
        {
            Id = "state.parallel-done",
            Name = "Parallel done",
            Groups = [],
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "parallel-review-batch",
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
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var initial = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(initial.Suspended);
        Assert.Equal(WorkflowStatus.WaitingExternal, initial.StatusProjection.Status);
        var waiting = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(waiting);
        Assert.Equal(2, waiting!.ActiveWaitGroups.Count);
        Assert.All(waiting.ActiveWaitGroups, group => Assert.Equal("group.parallel-reviews", group.ConcurrencyGroupId));
        var firstWaitId = waiting.ActiveWaitGroups.Single(group => group.TransitionId == first.Id).GetNextPendingEntry()!.WaitId;

        await ResumeWithActiveWaitIdAsync(service, instance.InstanceId, first.Id, payload: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["result"] = "first",
        });

        var duplicateResume = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(instance.InstanceId, first.Id, waitId: firstWaitId, payload: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["result"] = "duplicate",
        }));
        Assert.Contains("wait_id", duplicateResume.Message, StringComparison.OrdinalIgnoreCase);

        var afterFirst = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(afterFirst);
        Assert.Equal(WorkflowStatus.WaitingExternal, afterFirst!.Status);
        Assert.Equal(start.Id, afterFirst.CurrentNodeId);
        Assert.Equal(2, afterFirst.ActiveWaitGroups.Count);
        Assert.True(afterFirst.ActiveWaitGroups.Single(group => group.TransitionId == first.Id).Completed);
        Assert.False(afterFirst.ActiveWaitGroups.Single(group => group.TransitionId == second.Id).Completed);

        await ResumeWithActiveWaitIdAsync(service, instance.InstanceId, second.Id, payload: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["result"] = "second",
        });

        var joined = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(joined);
        Assert.Equal(WorkflowStatus.Running, joined!.Status);
        Assert.Equal(done.Id, joined.CurrentNodeId);
        Assert.Empty(joined.ActiveWaitGroups);
        Assert.False(joined.Context.ContainsKey("result"));
        var firstResult = Assert.IsAssignableFrom<IDictionary<string, object?>>(joined.Context["first_result"]);
        var secondResult = Assert.IsAssignableFrom<IDictionary<string, object?>>(joined.Context["second_result"]);
        Assert.Equal("first", firstResult["result"]);
        Assert.Equal("second", secondResult["result"]);

        var terminal = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.Equal(WorkflowStatus.Succeeded, terminal.StatusProjection.Status);
    }
    [Fact]
    public async Task WhenAnyResponse_CompletesWinnerAndRejectsLateLoserResult()
    {
        var first = new CommandTransition
        {
            Id = "transition.any-response-first",
            Name = "First response branch",
            TargetNodeId = "state.any-response-first-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "first_result",
            SucceedExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
            },
        };
        var second = new CommandTransition
        {
            Id = "transition.any-response-second",
            Name = "Second response branch",
            TargetNodeId = "state.any-response-second-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "second_result",
            SucceedExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
            },
        };
        var start = new StateNode
        {
            Id = "state.any-response-start",
            Name = "Any response start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.any-response",
                    Strategy = ConcurrencyStrategy.WhenAnyResponse,
                    TransitionIds = [first.Id, second.Id],
                },
            ],
        };
        var firstDone = new StateNode { Id = first.TargetNodeId, Name = "First response done", Groups = [] };
        var secondDone = new StateNode { Id = second.TargetNodeId, Name = "Second response done", Groups = [] };
        var instance = new WorkflowInstance
        {
            InstanceId = "when-any-response-runtime",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [firstDone.Id] = firstDone,
                [secondDone.Id] = secondDone,
                [first.Id] = first,
                [second.Id] = second,
            },
        };
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var initial = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(initial.Suspended);
        var waiting = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(waiting);
        Assert.Equal(2, waiting!.ActiveWaitGroups.Count);
        Assert.All(waiting.ActiveWaitGroups, waitGroup => Assert.Equal(ConcurrencyStrategy.WhenAnyResponse, waitGroup.OriginStrategy));
        var secondWaitId = Assert.Single(waiting.ActiveWaitGroups.Single(group => group.TransitionId == second.Id).Entries).WaitId;

        await ResumeWithActiveWaitIdAsync(service, instance.InstanceId, first.Id, payload: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["result"] = "first",
        });

        var winner = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(winner);
        Assert.Equal(WorkflowStatus.Running, winner!.Status);
        Assert.Equal(firstDone.Id, winner.CurrentNodeId);
        Assert.Empty(winner.ActiveWaitGroups);
        var cancellationSignal = Assert.Single(winner.FanoutCancellationSignals);
        Assert.Equal(second.Id, cancellationSignal.TransitionId);
        Assert.Equal(secondWaitId, cancellationSignal.WaitId);
        Assert.Equal($"{instance.InstanceId}:{secondWaitId}", cancellationSignal.SignalId);
        Assert.True(cancellationSignal.CancellationRequested);
        Assert.True(cancellationSignal.LateResultsIgnored);
        Assert.Equal("fanout_completed", cancellationSignal.Reason);
        Assert.Contains(winner.History, entry => entry.Message?.Contains(second.Id, StringComparison.Ordinal) == true
            && entry.Message.Contains("Cancellation", StringComparison.OrdinalIgnoreCase));
        var projectedStatus = await service.GetStatusAsync(instance.InstanceId);
        Assert.Equal(winner.FanoutCancellationSignals, projectedStatus.FanoutCancellationSignals);
        var firstOutput = Assert.IsAssignableFrom<IDictionary<string, object?>>(winner.Context["first_result"]);
        Assert.Equal("first", firstOutput["result"]);
        Assert.False(winner.Context.ContainsKey("second_result"));

        var versionBeforeLateResult = winner.Version;
        await service.ResumeAsync(
            instance.InstanceId,
            second.Id,
            payload: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = "late",
            },
            waitId: secondWaitId);

        var afterLateResult = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(afterLateResult);
        Assert.Equal(versionBeforeLateResult, afterLateResult!.Version);
        Assert.Equal(firstDone.Id, afterLateResult.CurrentNodeId);
        Assert.False(afterLateResult.Context.ContainsKey("second_result"));
    }

    [Fact]
    public async Task WhenAnyPass_WaitsForFirstPassingBranchAndKeepsPayloadsIsolated()
    {
        var first = new CommandTransition
        {
            Id = "transition.any-pass-first",
            Name = "First branch",
            TargetNodeId = "state.any-pass-first-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "first_branch_result",
            SucceedExpression = "context.Get<bool>(\"first_branch_result.branchPass\")",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
            },
        };
        var second = new CommandTransition
        {
            Id = "transition.any-pass-second",
            Name = "Second branch",
            TargetNodeId = "state.any-pass-second-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "second_branch_result",
            SucceedExpression = "context.Get<bool>(\"second_branch_result.branchPass\")",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
            },
        };
        var start = new StateNode
        {
            Id = "state.any-pass-start",
            Name = "Any pass start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.any-pass",
                    Strategy = ConcurrencyStrategy.WhenAnyPass,
                    NoPassTargetStateId = "state.any-pass-none",
                    TransitionIds = [first.Id, second.Id],
                },
            ],
        };
        var firstDone = new StateNode { Id = first.TargetNodeId, Name = "First branch done", Groups = [] };
        var secondDone = new StateNode { Id = second.TargetNodeId, Name = "Second branch done", Groups = [] };
        var noPass = new StateNode { Id = "state.any-pass-none", Name = "No pass", Groups = [] };
        var instance = new WorkflowInstance
        {
            InstanceId = "when-any-pass-runtime",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [firstDone.Id] = firstDone,
                [secondDone.Id] = secondDone,
                [noPass.Id] = noPass,
                [first.Id] = first,
                [second.Id] = second,
            },
        };
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var initial = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.True(initial.Suspended);

        var initialInstance = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(initialInstance);
        Assert.Equal(2, initialInstance!.ActiveWaitGroups.Count);
        Assert.All(initialInstance.ActiveWaitGroups, group => Assert.Equal(ConcurrencyStrategy.WhenAnyPass, group.OriginStrategy));
        Assert.Equal(2, initialInstance.ActiveWaitGroups[0].ExpectedTransitionIds.Count);

        await ResumeWithActiveWaitIdAsync(service, instance.InstanceId, first.Id, payload: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["branchPass"] = false,
            ["shared"] = "first",
        });

        var afterFailure = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(afterFailure);
        Assert.Equal(WorkflowStatus.WaitingExternal, afterFailure!.Status);
        Assert.Equal(start.Id, afterFailure.CurrentNodeId);
        Assert.False(afterFailure.ActiveWaitGroups.Single(group => group.TransitionId == first.Id).Passed);
        Assert.False(afterFailure.Context.ContainsKey("branchPass"));
        Assert.False(afterFailure.Context.ContainsKey("first_branch_result"));
        Assert.False(afterFailure.Context.ContainsKey("shared"));

        await ResumeWithActiveWaitIdAsync(service, instance.InstanceId, second.Id, payload: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["branchPass"] = true,
            ["shared"] = "second",
        });

        var winner = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(winner);
        Assert.Equal(WorkflowStatus.Running, winner!.Status);
        Assert.Equal(secondDone.Id, winner.CurrentNodeId);
        Assert.Empty(winner.ActiveWaitGroups);
        Assert.False(winner.Context.ContainsKey("first_branch_result"));
        Assert.False(winner.Context.ContainsKey("branchPass"));
        Assert.False(winner.Context.ContainsKey("shared"));
        var winningOutput = Assert.IsAssignableFrom<IDictionary<string, object?>>(winner.Context["second_branch_result"]);
        Assert.Equal(true, winningOutput["branchPass"]);
        Assert.Contains(winner.History, entry => entry.NodeId == first.Id && entry.Status == ExecutionStatus.Failed);
    }

    [Fact]
    public async Task WhenExpression_JoinsEarlyAndIsolatesNestedBranchOutputs()
    {
        var first = new CommandTransition
        {
            Id = "transition.when-expression-first",
            Name = "First fan-out branch",
            TargetNodeId = "state.when-expression-first-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "nested.first",
            SucceedExpression = "true",
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var second = new CommandTransition
        {
            Id = "transition.when-expression-second",
            Name = "Second fan-out branch",
            TargetNodeId = "state.when-expression-second-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "nested.second",
            SucceedExpression = "true",
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var third = new CommandTransition
        {
            Id = "transition.when-expression-third",
            Name = "Third fan-out branch",
            TargetNodeId = "state.when-expression-third-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "nested.third",
            SucceedExpression = "true",
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var start = new StateNode
        {
            Id = "state.when-expression-start",
            Name = "When expression start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.when-expression",
                    Strategy = ConcurrencyStrategy.WhenExpression,
                    PassExpression = new ExpressionDefinition
                    {
                        Source = "context.Has(\"nested.first\") && context.Has(\"nested.second\")",
                    },
                    JoinTargetStateId = "state.when-expression-joined",
                    ErrorHandlingTargetStateId = "state.when-expression-error",
                    TransitionIds = [first.Id, second.Id, third.Id],
                },
            ],
        };
        var firstDone = new StateNode { Id = first.TargetNodeId, Name = "First done", Groups = [] };
        var secondDone = new StateNode { Id = second.TargetNodeId, Name = "Second done", Groups = [] };
        var thirdDone = new StateNode { Id = third.TargetNodeId, Name = "Third done", Groups = [] };
        var joined = new StateNode { Id = "state.when-expression-joined", Name = "Joined", Groups = [] };
        var error = new StateNode { Id = "state.when-expression-error", Name = "Error", Groups = [] };
        var instance = new WorkflowInstance
        {
            InstanceId = "when-expression-nested-isolation",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = joined.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [firstDone.Id] = firstDone,
                [secondDone.Id] = secondDone,
                [thirdDone.Id] = thirdDone,
                [joined.Id] = joined,
                [error.Id] = error,
                [first.Id] = first,
                [second.Id] = second,
                [third.Id] = third,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["nested"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["baseline"] = "preserved",
                },
            },
        };
        var service = await StartFanoutWorkflowAsync(instance);

        await ResumeWithActiveWaitIdAsync(service, instance.InstanceId, first.Id, payload: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["result"] = "first",
        });

        var afterFirst = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(afterFirst);
        Assert.Equal(WorkflowStatus.WaitingExternal, afterFirst!.Status);
        var firstContext = Assert.IsAssignableFrom<IDictionary<string, object?>>(afterFirst.Context["nested"]);
        Assert.Equal("preserved", firstContext["baseline"]);
        Assert.False(firstContext.ContainsKey("first"));

        await ResumeWithActiveWaitIdAsync(service, instance.InstanceId, second.Id, payload: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["result"] = "second",
        });

        var joinedInstance = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(joinedInstance);
        Assert.Equal(WorkflowStatus.Running, joinedInstance!.Status);
        Assert.Equal(joined.Id, joinedInstance.CurrentNodeId);
        Assert.Empty(joinedInstance.ActiveWaitGroups);
        var joinedContext = Assert.IsAssignableFrom<IDictionary<string, object?>>(joinedInstance.Context["nested"]);
        Assert.Equal("preserved", joinedContext["baseline"]);
        Assert.Equal("first", Assert.IsAssignableFrom<IDictionary<string, object?>>(joinedContext["first"])["result"]);
        Assert.Equal("second", Assert.IsAssignableFrom<IDictionary<string, object?>>(joinedContext["second"])["result"]);
        Assert.False(joinedContext.ContainsKey("third"));
        Assert.Contains(joinedInstance.History, entry => entry.NodeId == third.Id && entry.Status == ExecutionStatus.Skipped);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(
            instance.InstanceId,
            third.Id,
            payload: new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = "late" }));
    }

    [Fact]
    public async Task WhenAllPass_OverlappingBranchOutputsFailBeforeExternalDispatch()
    {
        var first = new CommandTransition
        {
            Id = "transition.when-all-first",
            Name = "First branch",
            TargetNodeId = "state.when-all-first-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "shared.result",
            SucceedExpression = "true",
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var second = new CommandTransition
        {
            Id = "transition.when-all-second",
            Name = "Second branch",
            TargetNodeId = "state.when-all-second-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "shared.result",
            SucceedExpression = "true",
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var start = new StateNode
        {
            Id = "state.when-all-start",
            Name = "When all start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.when-all",
                    Strategy = ConcurrencyStrategy.WhenAllPass,
                    JoinTargetStateId = "state.when-all-joined",
                    ErrorHandlingTargetStateId = "state.when-all-error",
                    TransitionIds = [first.Id, second.Id],
                },
            ],
        };
        var firstDone = new StateNode { Id = first.TargetNodeId, Name = "First done", Groups = [] };
        var secondDone = new StateNode { Id = second.TargetNodeId, Name = "Second done", Groups = [] };
        var joined = new StateNode { Id = "state.when-all-joined", Name = "Joined", Groups = [] };
        var error = new StateNode { Id = "state.when-all-error", Name = "Error", Groups = [] };
        var instance = new WorkflowInstance
        {
            InstanceId = "when-all-output-conflict",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = joined.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [firstDone.Id] = firstDone,
                [secondDone.Id] = secondDone,
                [joined.Id] = joined,
                [error.Id] = error,
                [first.Id] = first,
                [second.Id] = second,
            },
        };
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var failedResult = await service.StartOrAdvanceAsync(instance.InstanceId);
        var failed = await service.GetInstanceAsync(instance.InstanceId);

        Assert.True(failedResult.Failed);
        Assert.NotNull(failed);
        Assert.Equal(WorkflowStatus.Failed, failed!.Status);
        Assert.Equal(start.Id, failed.CurrentNodeId);
        Assert.Empty(failed.ActiveWaitGroups);
        Assert.False(failed.Context.ContainsKey("shared"));
        Assert.Contains(failed.History, entry => entry.NodeId == start.Groups[0].Id
            && entry.Message?.Contains("overlapping output projections", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task WhenAllPass_PublishesOutputsOnlyAfterExplicitJoin()
    {
        var first = new CommandTransition
        {
            Id = "transition.explicit-all-first",
            Name = "First branch",
            TargetNodeId = "state.explicit-all-first-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "first_output",
            SucceedExpression = "true",
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var second = new CommandTransition
        {
            Id = "transition.explicit-all-second",
            Name = "Second branch",
            TargetNodeId = "state.explicit-all-second-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "second_output",
            SucceedExpression = "true",
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var start = new StateNode
        {
            Id = "state.explicit-all-start",
            Name = "Explicit all start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.explicit-all",
                    Strategy = ConcurrencyStrategy.WhenAllPass,
                    JoinTargetStateId = "state.explicit-all-joined",
                    ErrorHandlingTargetStateId = "state.explicit-all-error",
                    TransitionIds = [first.Id, second.Id],
                },
            ],
        };
        var firstDone = new StateNode { Id = first.TargetNodeId, Name = "First branch target", Groups = [] };
        var secondDone = new StateNode { Id = second.TargetNodeId, Name = "Second branch target", Groups = [] };
        var joined = new StateNode { Id = "state.explicit-all-joined", Name = "Explicit join", Groups = [] };
        var error = new StateNode { Id = "state.explicit-all-error", Name = "Error", Groups = [] };
        var instance = new WorkflowInstance
        {
            InstanceId = "when-all-explicit-join",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = joined.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [firstDone.Id] = firstDone,
                [secondDone.Id] = secondDone,
                [joined.Id] = joined,
                [error.Id] = error,
                [first.Id] = first,
                [second.Id] = second,
            },
        };
        var service = await StartFanoutWorkflowAsync(instance);

        await ResumeWithActiveWaitIdAsync(service, instance.InstanceId, first.Id, payload: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["result"] = "first",
        });
        var afterFirst = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(afterFirst);
        Assert.Equal(WorkflowStatus.WaitingExternal, afterFirst!.Status);
        Assert.False(afterFirst.Context.ContainsKey("first_output"));

        await ResumeWithActiveWaitIdAsync(service, instance.InstanceId, second.Id, payload: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["result"] = "second",
        });

        var afterJoin = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(afterJoin);
        Assert.Equal(WorkflowStatus.Running, afterJoin!.Status);
        Assert.Equal(joined.Id, afterJoin.CurrentNodeId);
        Assert.Empty(afterJoin.ActiveWaitGroups);
        var firstOutput = Assert.IsAssignableFrom<IDictionary<string, object?>>(afterJoin.Context["first_output"]);
        var secondOutput = Assert.IsAssignableFrom<IDictionary<string, object?>>(afterJoin.Context["second_output"]);
        Assert.Equal("first", firstOutput["result"]);
        Assert.Equal("second", secondOutput["result"]);
    }

    [Fact]
    public async Task SharedCoreFanoutAcrossSequentialGroupsPreservesSiblingOutputs()
    {
        var securityReview = CreateCoreFanoutTransition("transition.review-security", "state.security-done", "review.security");
        var performanceReview = CreateCoreFanoutTransition("transition.review-performance", "state.performance-done", "review.performance");
        var finalReview = CreateCoreFanoutTransition("transition.review-final", "state.done", "review.final");
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start review batch",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.initial-review",
                    Strategy = ConcurrencyStrategy.WhenAllPass,
                    JoinTargetStateId = "state.final-review",
                    TransitionIds = [securityReview.Id, performanceReview.Id],
                },
            ],
        };
        var final = new StateNode
        {
            Id = "state.final-review",
            Name = "Final review batch",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.final-review",
                    Strategy = ConcurrencyStrategy.WhenAllPass,
                    JoinTargetStateId = "state.done",
                    TransitionIds = [finalReview.Id],
                },
            ],
        };
        var done = new StateNode { Id = "state.done", Name = "Done", Groups = [] };
        var instance = new WorkflowInstance
        {
            InstanceId = "sequential-fanout-sibling-outputs",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [final.Id] = final,
                [done.Id] = done,
                ["state.security-done"] = new StateNode { Id = "state.security-done", Name = "Security branch", Groups = [] },
                ["state.performance-done"] = new StateNode { Id = "state.performance-done", Name = "Performance branch", Groups = [] },
                [securityReview.Id] = securityReview,
                [performanceReview.Id] = performanceReview,
                [finalReview.Id] = finalReview,
            },
        };
        var core = new WorkflowExecutionCore();

        var initial = await core.RunUntilBoundaryAsync(instance);
        Assert.True(initial.Suspended);
        var securityWait = Assert.Single(instance.ActiveWaitGroups, group => group.TransitionId == securityReview.Id);
        var performanceWait = Assert.Single(instance.ActiveWaitGroups, group => group.TransitionId == performanceReview.Id);
        await core.ResumeAsync(instance, securityReview.Id, securityWait.CorrelationKey, new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = "secure" }, waitId: securityWait.GetNextPendingEntry()!.WaitId);
        await core.ResumeAsync(instance, performanceReview.Id, performanceWait.CorrelationKey, new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = "fast" }, waitId: performanceWait.GetNextPendingEntry()!.WaitId);
        Assert.Equal(final.Id, instance.CurrentNodeId);

        var nextBatch = await core.RunUntilBoundaryAsync(instance);
        Assert.True(nextBatch.Suspended);
        var finalWait = Assert.Single(instance.ActiveWaitGroups);
        await core.ResumeAsync(instance, finalReview.Id, finalWait.CorrelationKey, new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = "approved" }, waitId: finalWait.GetNextPendingEntry()!.WaitId);

        Assert.Equal(done.Id, instance.CurrentNodeId);
        var review = Assert.IsAssignableFrom<IDictionary<string, object?>>(instance.Context["review"]);
        Assert.Equal("secure", review["security"]);
        Assert.Equal("fast", review["performance"]);
        Assert.Equal("approved", review["final"]);
    }

    [Fact]
    public async Task FanoutPlanResumeUsesWeaveBackTarget()
    {
        var plan = new CommandTransition
        {
            Id = "transition.plan-weaveback",
            Name = "Plan branch",
            TargetNodeId = "state.nominal-target",
            StepKind = WorkflowStepKind.Plan,
            Plan = new PlanStepContract
            {
                InputPaths = ["objective"],
                ResultFile = "plan.result.json",
                RequiredEvidence = ["plan.evidence"],
                WeaveBackTargetNodeId = "state.weaveback-target",
            },
            OutputPath = "plan_result",
            GuardExpression = "true",
            SucceedExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { ["resumeOutputKey"] = "result" },
            },
        };
        var other = CreateCoreFanoutTransition("transition.plan-other", "state.other-target", "other_result");
        var start = new StateNode
        {
            Id = "state.plan-start",
            Name = "Plan fanout start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.plan-race",
                    Strategy = ConcurrencyStrategy.WhenAnyResponse,
                    TransitionIds = [plan.Id, other.Id],
                },
            ],
        };
        var nominal = new StateNode { Id = "state.nominal-target", Name = "Nominal", Groups = [] };
        var weaveback = new StateNode { Id = "state.weaveback-target", Name = "Weave back", Groups = [] };
        var otherTarget = new StateNode { Id = "state.other-target", Name = "Other", Groups = [] };
        var instance = new WorkflowInstance
        {
            InstanceId = "plan-fanout-weaveback-target",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = weaveback.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [nominal.Id] = nominal,
                [weaveback.Id] = weaveback,
                [otherTarget.Id] = otherTarget,
                [plan.Id] = plan,
                [other.Id] = other,
            },
        };
        var service = await StartFanoutWorkflowAsync(instance);
        var waiting = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(waiting);
        var pending = Assert.Single(waiting!.ActiveWaitGroups, group => group.TransitionId == plan.Id);

        await ResumeWithActiveWaitIdAsync(service,
            instance.InstanceId,
            plan.Id,
            pending.CorrelationKey,
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = "planned" },
            resultId: "plan-fanout-result-1");

        var resumed = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(resumed);
        Assert.Equal(weaveback.Id, resumed!.CurrentNodeId);
        Assert.Empty(resumed.ActiveWaitGroups);
    }

    private static CommandTransition CreateCoreFanoutTransition(string id, string targetNodeId, string outputPath)
    {
        return new CommandTransition
        {
            Id = id,
            Name = id,
            TargetNodeId = targetNodeId,
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = outputPath,
            GuardExpression = "true",
            SucceedExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { ["resumeOutputKey"] = "result" },
            },
        };
    }

    [Fact]
    public async Task WhenAnyPass_AllFailedBranchesRouteToNoPassTarget()
    {
        var first = new CommandTransition
        {
            Id = "transition.no-pass-first",
            Name = "First branch",
            TargetNodeId = "state.no-pass-first-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "first_output",
            PassExpression = "context.Get<bool>(\"first_output.passed\")",
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var second = new CommandTransition
        {
            Id = "transition.no-pass-second",
            Name = "Second branch",
            TargetNodeId = "state.no-pass-second-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "second_output",
            PassExpression = "context.Get<bool>(\"second_output.passed\")",
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var start = new StateNode
        {
            Id = "state.no-pass-start",
            Name = "No-pass start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.no-pass",
                    Strategy = ConcurrencyStrategy.WhenAnyPass,
                    NoPassTargetStateId = "state.no-pass-result",
                    ErrorHandlingTargetStateId = "state.no-pass-error",
                    TransitionIds = [first.Id, second.Id],
                },
            ],
        };
        var firstDone = new StateNode { Id = first.TargetNodeId, Name = "First branch target", Groups = [] };
        var secondDone = new StateNode { Id = second.TargetNodeId, Name = "Second branch target", Groups = [] };
        var noPass = new StateNode { Id = "state.no-pass-result", Name = "No branch passed", Groups = [] };
        var error = new StateNode { Id = "state.no-pass-error", Name = "Error", Groups = [] };
        var instance = new WorkflowInstance
        {
            InstanceId = "when-any-pass-no-pass-route",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = noPass.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [firstDone.Id] = firstDone,
                [secondDone.Id] = secondDone,
                [noPass.Id] = noPass,
                [error.Id] = error,
                [first.Id] = first,
                [second.Id] = second,
            },
        };
        var service = await StartFanoutWorkflowAsync(instance);

        await ResumeWithActiveWaitIdAsync(service, instance.InstanceId, first.Id, payload: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["passed"] = false,
            ["result"] = "first",
        });
        var afterFirst = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(afterFirst);
        Assert.Equal(WorkflowStatus.WaitingExternal, afterFirst!.Status);
        Assert.False(afterFirst.Context.ContainsKey("first_output"));

        await ResumeWithActiveWaitIdAsync(service, instance.InstanceId, second.Id, payload: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["passed"] = false,
            ["result"] = "second",
        });

        var noPassResult = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(noPassResult);
        Assert.Equal(WorkflowStatus.Running, noPassResult!.Status);
        Assert.Equal(noPass.Id, noPassResult.CurrentNodeId);
        Assert.Empty(noPassResult.ActiveWaitGroups);
        Assert.Equal("first", Assert.IsAssignableFrom<IDictionary<string, object?>>(noPassResult.Context["first_output"])["result"]);
        Assert.Equal("second", Assert.IsAssignableFrom<IDictionary<string, object?>>(noPassResult.Context["second_output"])["result"]);
    }

    [Fact]
    public async Task ResumeAsync_ExpiredFanoutBranchBeforeTickUsesTimeoutFallback()
    {
        var first = CreateCoreFanoutTransition("transition.expired-first", "state.expired-first-done", "first_output");
        var second = CreateCoreFanoutTransition("transition.expired-second", "state.expired-second-done", "second_output");
        var timeout = new StateNode { Id = "state.expired-timeout", Name = "Timed out", Groups = [] };
        var start = new StateNode
        {
            Id = "state.expired-start",
            Name = "Expired start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.expired",
                    Strategy = ConcurrencyStrategy.WhenAllPass,
                    GroupTimeout = TimeSpan.Zero,
                    JoinTargetStateId = "state.expired-joined",
                    TimeoutTargetStateId = timeout.Id,
                    TransitionIds = [first.Id, second.Id],
                },
            ],
        };
        var joined = new StateNode { Id = "state.expired-joined", Name = "Joined", Groups = [] };
        var instance = new WorkflowInstance
        {
            InstanceId = "expired-fanout-resume-before-tick",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = timeout.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [joined.Id] = joined,
                [timeout.Id] = timeout,
                ["state.expired-first-done"] = new StateNode { Id = "state.expired-first-done", Name = "First branch", Groups = [] },
                ["state.expired-second-done"] = new StateNode { Id = "state.expired-second-done", Name = "Second branch", Groups = [] },
                [first.Id] = first,
                [second.Id] = second,
            },
        };
        var service = await StartFanoutWorkflowAsync(instance);
        var waiting = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(waiting);
        Assert.Equal(WorkflowStatus.WaitingExternal, waiting!.Status);

        await ResumeWithActiveWaitIdAsync(service, instance.InstanceId, first.Id, payload: new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = "late" });

        var timedOut = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(timedOut);
        Assert.Equal(timeout.Id, timedOut!.CurrentNodeId);
        Assert.Equal(WorkflowStatus.Running, timedOut.Status);
        Assert.Empty(timedOut.ActiveWaitGroups);
        Assert.False(timedOut.Context.ContainsKey("first_output"));
        Assert.All(timedOut.FanoutCancellationSignals, static signal => Assert.True(signal.LateResultsIgnored));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FanoutGroupTimeout_RoutesOnceWithoutPublishingExpiredPayloads(bool useTransitionFallback)
    {
        var first = new CommandTransition
        {
            Id = "transition.timeout-first",
            Name = "First branch",
            TargetNodeId = "state.timeout-first-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "first_output",
            SucceedExpression = "true",
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var second = new CommandTransition
        {
            Id = "transition.timeout-second",
            Name = "Second branch",
            TargetNodeId = "state.timeout-second-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "second_output",
            SucceedExpression = "true",
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var timeout = new StateNode { Id = "state.timeout", Name = "Timed out", Groups = [] };
        var start = new StateNode
        {
            Id = "state.timeout-start",
            Name = "Timeout start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.timeout",
                    Strategy = ConcurrencyStrategy.WhenAllPass,
                    GroupTimeout = TimeSpan.Zero,
                    TimeoutTargetStateId = useTransitionFallback ? null : timeout.Id,
                    TimeoutTransition = useTransitionFallback
                        ? new CommandTransition { Id = "transition.timeout-fallback", Name = "Timeout fallback", TargetNodeId = timeout.Id }
                        : null,
                    JoinTargetStateId = "state.timeout-joined",
                    TransitionIds = [first.Id, second.Id],
                },
            ],
        };
        var firstDone = new StateNode { Id = first.TargetNodeId, Name = "First branch target", Groups = [] };
        var secondDone = new StateNode { Id = second.TargetNodeId, Name = "Second branch target", Groups = [] };
        var joined = new StateNode { Id = "state.timeout-joined", Name = "Joined", Groups = [] };
        var instance = new WorkflowInstance
        {
            InstanceId = "fanout-group-timeout",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = timeout.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [timeout.Id] = timeout,
                [firstDone.Id] = firstDone,
                [secondDone.Id] = secondDone,
                [joined.Id] = joined,
                [first.Id] = first,
                [second.Id] = second,
            },
        };
        var service = await StartFanoutWorkflowAsync(instance);

        var waiting = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(waiting);
        var pendingWaitIds = waiting!.ActiveWaitGroups
            .SelectMany(static waitGroup => waitGroup.Entries)
            .Where(static entry => !entry.Completed)
            .Select(static entry => entry.WaitId)
            .ToArray();
        var deadlines = waiting.ActiveWaitGroups
            .SelectMany(static waitGroup => waitGroup.Entries)
            .Select(static entry => entry.ExpireAt)
            .Distinct()
            .ToArray();
        Assert.Single(deadlines);
        Assert.NotNull(deadlines[0]);

        var timedOut = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.Equal(WorkflowStatus.Succeeded, timedOut.StatusProjection.Status);
        var finalInstance = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(finalInstance);
        Assert.Equal(timeout.Id, finalInstance!.CurrentNodeId);
        Assert.Empty(finalInstance.ActiveWaitGroups);
        Assert.Equal(pendingWaitIds.OrderBy(static waitId => waitId, StringComparer.Ordinal), finalInstance.FanoutCancellationSignals.Select(static signal => signal.WaitId).OrderBy(static waitId => waitId, StringComparer.Ordinal));
        Assert.All(finalInstance.FanoutCancellationSignals, static signal => Assert.True(signal.LateResultsIgnored));
        Assert.False(finalInstance.Context.ContainsKey("waitExpired"));
        Assert.False(finalInstance.Context.ContainsKey("first_output"));
        Assert.False(finalInstance.Context.ContainsKey("second_output"));
        Assert.Single(finalInstance.History, entry => entry.Message == "WaitTimeoutTransitionApplied");
    }

    [Fact]
    public async Task WhenExpression_RuntimeExceptionRoutesToGroupErrorTarget()
    {
        var first = new CommandTransition
        {
            Id = "transition.when-expression-error-first",
            Name = "First branch",
            TargetNodeId = "state.when-expression-error-first-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "candidate.first",
            SucceedExpression = "true",
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var second = new CommandTransition
        {
            Id = "transition.when-expression-error-second",
            Name = "Second branch",
            TargetNodeId = "state.when-expression-error-second-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "candidate.second",
            SucceedExpression = "true",
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var start = new StateNode
        {
            Id = "state.when-expression-error-start",
            Name = "When expression error start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.when-expression-error",
                    Strategy = ConcurrencyStrategy.WhenExpression,
                    PassExpression = "context.Get<int>(\"zero\") / context.Get<int>(\"zero\") == 1",
                    JoinTargetStateId = "state.when-expression-error-joined",
                    ErrorHandlingTargetStateId = "state.when-expression-error-target",
                    TransitionIds = [first.Id, second.Id],
                },
            ],
        };
        var firstDone = new StateNode { Id = first.TargetNodeId, Name = "First branch target", Groups = [] };
        var secondDone = new StateNode { Id = second.TargetNodeId, Name = "Second branch target", Groups = [] };
        var joined = new StateNode { Id = "state.when-expression-error-joined", Name = "Joined", Groups = [] };
        var error = new StateNode { Id = "state.when-expression-error-target", Name = "Expression error", Groups = [] };
        var instance = new WorkflowInstance
        {
            InstanceId = "when-expression-runtime-error",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = joined.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [firstDone.Id] = firstDone,
                [secondDone.Id] = secondDone,
                [joined.Id] = joined,
                [error.Id] = error,
                [first.Id] = first,
                [second.Id] = second,
            },
        };
        var service = await StartFanoutWorkflowAsync(instance);

        await ResumeWithActiveWaitIdAsync(service,
            instance.InstanceId,
            first.Id,
            payload: new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = "first" });

        var afterFirst = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(afterFirst);
        Assert.Equal(WorkflowStatus.WaitingExternal, afterFirst!.Status);
        Assert.Equal(start.Id, afterFirst.CurrentNodeId);
        Assert.Empty(afterFirst.Context);
        Assert.Equal(2, afterFirst.ActiveWaitGroups.Count);

        await ResumeWithActiveWaitIdAsync(service,
            instance.InstanceId,
            second.Id,
            payload: new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = "second" });

        var routed = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(routed);
        Assert.Equal(WorkflowStatus.Running, routed!.Status);
        Assert.Equal(error.Id, routed.CurrentNodeId);
        Assert.Empty(routed.ActiveWaitGroups);
        Assert.False(routed.Context.ContainsKey("candidate.first"));
        Assert.False(routed.Context.ContainsKey("candidate.second"));
        Assert.Contains(routed.History, entry => entry.NodeId == start.Groups[0].Id
            && entry.ContextChanges?.ContainsKey("executionError") == true);
    }

    [Fact]
    public async Task WhenExpression_ExhaustedSiblingErrorWinsOverLaterPassingEvaluation()
    {
        var first = CreateCoreFanoutTransition(
            "transition.when-expression-exhausted-first",
            "state.when-expression-exhausted-first-done",
            "candidate.first") with { MaxRetries = 0 };
        var second = CreateCoreFanoutTransition(
            "transition.when-expression-exhausted-second",
            "state.when-expression-exhausted-second-done",
            "candidate.second") with { MaxRetries = 0 };
        var start = new StateNode
        {
            Id = "state.when-expression-exhausted-start",
            Name = "When expression exhausted start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.when-expression-exhausted",
                    Strategy = ConcurrencyStrategy.WhenExpression,
                    PassExpression = "context.Get<string>(\"candidate.second\") == \"second\"",
                    JoinTargetStateId = "state.when-expression-exhausted-joined",
                    ErrorHandlingTargetStateId = "state.when-expression-exhausted-error",
                    TransitionIds = [first.Id, second.Id],
                },
            ],
        };
        var firstDone = new StateNode { Id = first.TargetNodeId!, Name = "First done", Groups = [] };
        var secondDone = new StateNode { Id = second.TargetNodeId!, Name = "Second done", Groups = [] };
        var joined = new StateNode { Id = "state.when-expression-exhausted-joined", Name = "Joined", Groups = [] };
        var error = new StateNode { Id = "state.when-expression-exhausted-error", Name = "Error", Groups = [] };
        var instance = new WorkflowInstance
        {
            InstanceId = "when-expression-exhausted-sibling-error",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = joined.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [firstDone.Id] = firstDone,
                [secondDone.Id] = secondDone,
                [joined.Id] = joined,
                [error.Id] = error,
                [first.Id] = first,
                [second.Id] = second,
            },
        };
        var service = await StartFanoutWorkflowAsync(instance);
        var initial = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(initial);
        var firstWait = initial!.ActiveWaitGroups.Single(group => group.TransitionId == first.Id);
        var firstWaitId = firstWait.GetNextPendingEntry()!.WaitId;

        await service.ResumeAsync(
            instance.InstanceId,
            first.Id,
            executionError: "first branch exhausted",
            waitId: firstWaitId);
        var afterError = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(afterError);
        Assert.Equal(WorkflowStatus.WaitingExternal, afterError!.Status);
        Assert.Equal(start.Id, afterError.CurrentNodeId);

        await ResumeWithActiveWaitIdAsync(
            service,
            instance.InstanceId,
            second.Id,
            payload: new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = "second" });
        var resolved = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(resolved);
        Assert.Equal(WorkflowStatus.Running, resolved!.Status);
        Assert.Equal(error.Id, resolved.CurrentNodeId);
        Assert.Empty(resolved.ActiveWaitGroups);
        Assert.False(resolved.Context.ContainsKey("candidate.first"));
        Assert.False(resolved.Context.ContainsKey("candidate.second"));
        Assert.Contains(resolved.History, entry => entry.NodeId == start.Groups[0].Id
            && entry.ContextChanges?.ContainsKey("executionError") == true);
    }

    [Fact]
    public async Task FanoutExecutionError_RetriesWithFreshWaitIdThenRoutesWhenExhausted()
    {
        var first = new CommandTransition
        {
            Id = "transition.retry-first",
            Name = "Retrying branch",
            TargetNodeId = "state.retry-first-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "first_output",
            SucceedExpression = "true",
            MaxRetries = 1,
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var second = new CommandTransition
        {
            Id = "transition.retry-second",
            Name = "Other branch",
            TargetNodeId = "state.retry-second-done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "second_output",
            SucceedExpression = "true",
            MaxRetries = 0,
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var start = new StateNode
        {
            Id = "state.retry-start",
            Name = "Retry start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.retry",
                    Strategy = ConcurrencyStrategy.WhenAnyPass,
                    ErrorHandlingTargetStateId = "state.retry-error",
                    NoPassTargetStateId = "state.retry-no-pass",
                    TransitionIds = [first.Id, second.Id],
                },
            ],
        };
        var firstDone = new StateNode { Id = first.TargetNodeId, Name = "First done", Groups = [] };
        var secondDone = new StateNode { Id = second.TargetNodeId, Name = "Second done", Groups = [] };
        var error = new StateNode { Id = "state.retry-error", Name = "Retry exhausted", Groups = [] };
        var noPass = new StateNode { Id = "state.retry-no-pass", Name = "No pass", Groups = [] };
        var instance = new WorkflowInstance
        {
            InstanceId = "fanout-execution-error-retry",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = error.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [firstDone.Id] = firstDone,
                [secondDone.Id] = secondDone,
                [error.Id] = error,
                [noPass.Id] = noPass,
                [first.Id] = first,
                [second.Id] = second,
            },
        };
        var service = await StartFanoutWorkflowAsync(instance);
        var initial = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(initial);
        var firstWaitGroup = initial!.ActiveWaitGroups.Single(group => group.TransitionId == first.Id);
        var firstWaitId = Assert.Single(firstWaitGroup.Entries).WaitId;

        await service.ResumeAsync(
            instance.InstanceId,
            first.Id,
            executionError: "temporary remote failure",
            waitId: firstWaitId);

        var retried = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(retried);
        Assert.Equal(WorkflowStatus.WaitingExternal, retried!.Status);
        Assert.Equal(start.Id, retried.CurrentNodeId);
        var retryHistoryCount = retried.History.Count;
        var retryTick = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.True(retryTick.Suspended);
        var afterRetryTick = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(afterRetryTick);
        Assert.Equal(WorkflowStatus.WaitingExternal, afterRetryTick!.Status);
        Assert.Equal(retryHistoryCount, afterRetryTick.History.Count);
        var retriedTransition = Assert.IsType<CommandTransition>(afterRetryTick.Nodes[first.Id]);
        Assert.Equal(1, retriedTransition.CurrentRetryCount);
        var retryGroup = afterRetryTick.ActiveWaitGroups.Single(group => group.TransitionId == first.Id);
        Assert.Equal(2, retryGroup.Entries.Count);
        var retryWaitId = Assert.Single(retryGroup.Entries, static entry => !entry.Completed).WaitId;
        Assert.NotEqual(firstWaitId, retryWaitId);
        Assert.Contains(retried.History, entry => entry.NodeId == first.Id
            && entry.ContextChanges?.ContainsKey("executionError") == true);

        var staleWaitError = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(
            instance.InstanceId,
            first.Id,
            executionError: "stale result",
            waitId: firstWaitId));
        Assert.Contains("wait_id", staleWaitError.Message, StringComparison.OrdinalIgnoreCase);

        await service.ResumeAsync(
            instance.InstanceId,
            first.Id,
            executionError: "permanent remote failure",
            waitId: retryWaitId);

        var afterFirstExhaustion = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(afterFirstExhaustion);
        Assert.Equal(WorkflowStatus.WaitingExternal, afterFirstExhaustion!.Status);
        Assert.Equal(start.Id, afterFirstExhaustion.CurrentNodeId);
        var secondWaitGroup = Assert.Single(afterFirstExhaustion.ActiveWaitGroups, group => group.TransitionId == second.Id);
        var secondWaitId = Assert.Single(secondWaitGroup.Entries, static entry => !entry.Completed).WaitId;

        await service.ResumeAsync(
            instance.InstanceId,
            second.Id,
            executionError: "second branch failed",
            waitId: secondWaitId);

        var exhausted = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(exhausted);
        Assert.Equal(WorkflowStatus.Running, exhausted!.Status);
        Assert.Equal(error.Id, exhausted.CurrentNodeId);
        Assert.Empty(exhausted.ActiveWaitGroups);
        Assert.False(exhausted.Context.ContainsKey("first_output"));
        Assert.False(exhausted.Context.ContainsKey("second_output"));
        Assert.Equal(2, Assert.IsType<CommandTransition>(exhausted.Nodes[first.Id]).CurrentRetryCount);
        Assert.Equal(1, Assert.IsType<CommandTransition>(exhausted.Nodes[second.Id]).CurrentRetryCount);
        Assert.Contains(exhausted.History, entry => entry.NodeId == start.Groups[0].Id
            && entry.ContextChanges?.ContainsKey("executionError") == true);
    }

    [Fact]
    public async Task BoundedMemoryRead_BuildsOneHashableSharedReviewContext()
    {
        var assetRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-shared-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(assetRoot);
        File.WriteAllText(Path.Combine(assetRoot, "shared-context.md"), "0123456789");
        try
        {
            var transition = new CommandTransition
            {
                Id = "transition.build-shared-context",
                Name = "Build shared review context",
                TargetNodeId = "state.shared-context-done",
                StepKind = WorkflowStepKind.MemoryRead,
                OutputPath = "shared_review_context",
                SucceedExpression = "true",
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.NativeCode,
                    Name = "workflow.buildSharedReviewContext",
                    Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["assetRootInput"] = "target_skill_path",
                        ["checkedInAssets"] = new[] { "shared-context.md" },
                        ["boundedContext"] = true,
                        ["maxContentCharacters"] = 4,
                        ["contextKind"] = "shared_enhancement_review_context",
                    },
                },
            };
            var start = new StateNode
            {
                Id = "state.shared-context-start",
                Name = "Shared context start",
                WorkflowPhase = "Test",
                Groups = [new TransitionGroup { Id = "group.shared-context", TransitionIds = [transition.Id] }],
            };
            var done = new StateNode
            {
                Id = "state.shared-context-done",
                Name = "Shared context done",
                WorkflowPhase = "Test",
                Groups = [],
            };
            var instance = new WorkflowInstance
            {
                InstanceId = "shared-context-runtime",
                StartNodeId = start.Id,
                CurrentNodeId = start.Id,
                EndNodeId = done.Id,
                Status = WorkflowStatus.ReadyToStart,
                Context = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["target_skill_path"] = assetRoot,
                },
                Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
                {
                    [start.Id] = start,
                    [done.Id] = done,
                    [transition.Id] = transition,
                },
            };
            var store = new InMemoryInstanceStore();
            await store.SaveNewAsync(instance);
            var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

            var result = await service.StartOrAdvanceAsync(instance.InstanceId);

            Assert.True(result.Progressed);
            var saved = await service.GetInstanceAsync(instance.InstanceId);
            Assert.NotNull(saved);
            var context = Assert.IsAssignableFrom<IDictionary<string, object?>>(saved!.Context["shared_review_context"]);
            Assert.Equal("shared_enhancement_review_context", Convert.ToString(context["context_kind"]));
            Assert.Equal(true, context["bounded"]);
            Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(context["context_hash"])));
            Assert.NotNull(context["source_manifest"]);
            var snapshots = Assert.IsAssignableFrom<IEnumerable<object?>>(context["checkedInAssets"]);
            var snapshot = Assert.IsAssignableFrom<IDictionary<string, object?>>(snapshots.Single());
            Assert.Contains("...[bounded context truncated]", Convert.ToString(snapshot["content"]), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(assetRoot))
            {
                Directory.Delete(assetRoot, recursive: true);
            }
        }
    }
    [Fact]
    public async Task ResumeAsync_SucceededInstanceRequiresFreshRuntimeCopy()
    {
        var instance = new WorkflowInstance
        {
            InstanceId = "terminal-resume-succeeded",
            Status = WorkflowStatus.Succeeded,
        };
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(instance.InstanceId, "transition.any"));

        Assert.Contains("terminally Succeeded", error.Message, StringComparison.Ordinal);
        Assert.Contains("fresh runtime workflow copy", error.Message, StringComparison.Ordinal);
        Assert.Contains("do not resume this persisted state", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResumeAsync_FailedInstanceRestoresPreviousStateForRetry()
    {
        var transition = new CommandTransition
        {
            Id = "transition.retry-failed",
            Name = "Retry failed transition",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            GuardExpression = "true",
            SucceedExpression = "context.Get<bool>(\"allow_retry\")",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups = [new TransitionGroup { Id = "group.start", TransitionIds = [transition.Id] }],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "failed-resume-retry",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Context = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["allow_retry"] = false,
            },
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [transition.Id] = transition,
            },
        };
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var failed = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.Equal(WorkflowStatus.Failed, failed.StatusProjection.Status);

        var wrongTransitionError = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(instance.InstanceId, "transition.other"));
        Assert.Contains("most recent failed transition", wrongTransitionError.Message, StringComparison.Ordinal);

        var failedInstance = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(failedInstance);
        failedInstance!.Context["allow_retry"] = true;
        await service.SaveWorkflowAsync(failedInstance);

        var resumed = await service.ResumeAsync(instance.InstanceId, transition.Id);

        Assert.Equal(WorkflowStatus.Running, resumed.Status);
        Assert.Equal(start.Id, resumed.CurrentNodeId);
        var completed = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.Equal(WorkflowStatus.Succeeded, completed.StatusProjection.Status);
    }

    [Fact]
    public async Task ResumeAsync_FailedInstanceWithoutFailureHistoryIsRejected()
    {
        var instance = new WorkflowInstance
        {
            InstanceId = "failed-resume-no-history",
            CurrentNodeId = "state.start",
            Status = WorkflowStatus.Failed,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["state.start"] = new StateNode
                {
                    Id = "state.start",
                    Groups = [],
                },
            },
        };
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(instance.InstanceId, "transition.failed"));

        Assert.Contains("no failed transition", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResumeAsync_FailedInstanceWithMissingPreviousStateIsRejected()
    {
        var instance = new WorkflowInstance
        {
            InstanceId = "failed-resume-missing-state",
            CurrentNodeId = "state.missing",
            Status = WorkflowStatus.Failed,
            History =
            [
                new WorkflowHistoryEntry(DateTimeOffset.UtcNow, "transition.failed", TaskNodeType.Transition, ExecutionStatus.Failed),
            ],
        };
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(instance.InstanceId, "transition.failed"));

        Assert.Contains("previous state", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResumeAsync_FailedInstanceWithTransitionOutsidePreviousStateIsRejected()
    {
        var instance = new WorkflowInstance
        {
            InstanceId = "failed-resume-outside-state",
            CurrentNodeId = "state.start",
            Status = WorkflowStatus.Failed,
            History =
            [
                new WorkflowHistoryEntry(DateTimeOffset.UtcNow, "transition.failed", TaskNodeType.Transition, ExecutionStatus.Failed),
            ],
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["state.start"] = new StateNode
                {
                    Id = "state.start",
                    Groups = [new TransitionGroup { Id = "group.start", TransitionIds = ["transition.other"] }],
                },
            },
        };
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(instance.InstanceId, "transition.failed"));

        Assert.Contains("cannot recover transition", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResumeAsync_FailedGroupIdIsNotTreatedAsTransition()
    {
        var first = new CommandTransition
        {
            Id = "transition.first",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };
        var second = new CommandTransition
        {
            Id = "transition.second",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };
        var start = new StateNode
        {
            Id = "state.start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.all",
                    Strategy = ConcurrencyStrategy.WhenAllPass,
                    TransitionIds = [first.Id, second.Id],
                },
            ],
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "failed-resume-group-id",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = "state.done",
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [first.Id] = first,
                [second.Id] = second,
            },
        };
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var failed = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.Equal(WorkflowStatus.Failed, failed.StatusProjection.Status);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(instance.InstanceId, start.Groups[0].Id));

        Assert.Contains("cannot recover transition", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResumeAsync_RejectsNonWaitingStateAndMismatchedWaitGroupIdentity()
    {
        var transition = new CommandTransition
        {
            Id = "transition.external",
            Name = "External step",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.SubagentCall,
            SucceedExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        var runningInstance = CreateExternalWorkflow("resume-running", transition);
        runningInstance.Status = WorkflowStatus.Running;
        var runningWaitGroup = new PendingWaitGroup
        {
            InstanceId = runningInstance.InstanceId,
            TransitionId = transition.Id,
        };
        runningWaitGroup.AddEntry(null);
        runningInstance.ActiveWaitGroups = [runningWaitGroup];
        var runningStore = new InMemoryInstanceStore();
        await runningStore.SaveNewAsync(runningInstance);
        var runningService = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(runningStore));

        var runningError = await Assert.ThrowsAsync<InvalidOperationException>(() => runningService.ResumeAsync(
            runningInstance.InstanceId,
            transition.Id));
        Assert.Contains("WaitingExternal", runningError.Message, StringComparison.Ordinal);

        var waitingInstance = CreateExternalWorkflow("resume-mismatched-wait", transition);
        waitingInstance.Status = WorkflowStatus.WaitingExternal;
        waitingInstance.ActiveWaitGroups =
        [
            new PendingWaitGroup
            {
                InstanceId = "different-instance",
                TransitionId = transition.Id,
                CorrelationKey = "expected-correlation",
                Entries = [new PendingWaitEntry { WaitId = "wait-1" }],
            },
        ];
        var waitingStore = new InMemoryInstanceStore();
        await waitingStore.SaveNewAsync(waitingInstance);
        var waitingService = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(waitingStore));

        var identityError = await Assert.ThrowsAsync<InvalidOperationException>(() => waitingService.ResumeAsync(
            waitingInstance.InstanceId,
            transition.Id,
            correlationKey: "wrong-correlation"));
        Assert.Contains("Active wait group", identityError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResumeAsync_CanonicalResultProjectionDoesNotCreateDuplicateWrapperPath()
    {
        var transition = new CommandTransition
        {
            Id = "transition.review",
            Name = "Review result",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "review_round",
            SucceedExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["resumeOutputKey"] = "result",
                    ["requiredInputs"] = new List<object?> { "result" },
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["review_findings"] = "$context:review_round.findings",
                    },
                },
            },
        };
        var instance = CreateExternalWorkflow("canonical-projection", transition);
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var boundary = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.True(boundary.Suspended);

        await service.ResumeAsync(
            instance.InstanceId,
            transition.Id,
            payload: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["findings"] = new[] { "finding-1" },
                },
            });

        var completed = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.Equal(WorkflowStatus.Succeeded, completed.StatusProjection.Status);

        var saved = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(saved);
        Assert.NotNull(PathValueAccessor.GetValue(saved!.Context, "review_round"));
        var findings = Assert.IsAssignableFrom<System.Collections.IEnumerable>(PathValueAccessor.GetValue(saved.Context, "review_findings"));
        Assert.Equal("finding-1", Convert.ToString(findings.Cast<object?>().Single()));
        Assert.Null(PathValueAccessor.GetValue(saved.Context, "review_round.review_round"));
    }

    [Fact]
    public void DataflowAnalyzer_MapsRequiredFamilyToConcreteBinding()
    {
        var transition = new CommandTransition
        {
            Id = "transition.review",
            Name = "Review result",
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "review_round",
            SatisfiesGateIds = ["gate.review"],
            PublishesOutputFamilies = ["review_findings"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["resumeOutputKey"] = "result",
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["review_findings"] = "$context:review_round.findings",
                    },
                },
            },
        };
        var instance = CreateExternalWorkflow("dataflow-report", transition);
        instance.Validation = new WorkflowValidationContract
        {
            Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
            {
                ["gate.review"] = new WorkflowValidationGate
                {
                    RequiredOutputFamilies = ["review_findings"],
                },
            },
        };

        var report = new SkillWorkflowDataflowAnalyzer().Analyze(instance);
        var transitionReport = Assert.Single(report.Transitions);

        Assert.True(report.IsResolved);
        Assert.Equal("result", transitionReport.ResumeOutputKey);
        Assert.Equal("review_round", transitionReport.OutputPath);
        Assert.Equal("$context:review_round.findings", transitionReport.OutputBindings["review_findings"]);
        Assert.Contains("review_findings", report.GateRequiredOutputFamilies["gate.review"]);
    }

    [Fact]
    public void DataflowAnalyzer_RejectsFutureProducerForContextBackedOutputFamily()
    {
        var consumer = new CommandTransition
        {
            Id = "transition.consume",
            Name = "Consume future output",
            TargetNodeId = "state.produce",
            StepKind = WorkflowStepKind.ToolCall,
            PublishesOutputFamilies = ["review_findings"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["review_findings"] = "$context:future_output.findings",
                    },
                },
            },
        };
        var producer = new CommandTransition
        {
            Id = "transition.produce",
            Name = "Produce future output",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            OutputPath = "future_output",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "future-producer",
            StartNodeId = "state.start",
            CurrentNodeId = "state.start",
            EndNodeId = "state.done",
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["state.start"] = new StateNode
                {
                    Id = "state.start",
                    Name = "Start",
                    WorkflowPhase = "Test",
                    Groups = [new TransitionGroup { Id = "group.start", TransitionIds = [consumer.Id] }],
                },
                ["state.produce"] = new StateNode
                {
                    Id = "state.produce",
                    Name = "Produce",
                    WorkflowPhase = "Test",
                    Groups = [new TransitionGroup { Id = "group.produce", TransitionIds = [producer.Id] }],
                },
                ["state.done"] = new StateNode { Id = "state.done", Name = "Done", WorkflowPhase = "Done", Groups = [] },
                [consumer.Id] = consumer,
                [producer.Id] = producer,
            },
        };

        var report = new SkillWorkflowDataflowAnalyzer().Analyze(instance);

        var issue = Assert.Single(report.Issues);
        Assert.Equal(consumer.Id, issue.TransitionId);
        Assert.Equal("review_findings", issue.OutputFamily);
        Assert.Contains("before this transition", issue.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void DataflowAnalyzer_RejectsFirstPassFutureProducerInsideCycle()
    {
        // Cycle: state.start ->(consumer)-> state.produce ->(producer back edge)-> state.start.
        // On the first pass the consumer executes before the producer has ever run, so the
        // $context binding cannot be satisfied even though pure reachability says otherwise.
        var consumer = new CommandTransition
        {
            Id = "transition.consume",
            Name = "Consume on first loop pass",
            TargetNodeId = "state.produce",
            StepKind = WorkflowStepKind.ToolCall,
            PublishesOutputFamilies = ["review_findings"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["review_findings"] = "$context:future_output.findings",
                    },
                },
            },
        };
        var producer = new CommandTransition
        {
            Id = "transition.produce",
            Name = "Produce via back edge",
            TargetNodeId = "state.start",
            StepKind = WorkflowStepKind.ToolCall,
            OutputPath = "future_output",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "cycle-future-producer",
            StartNodeId = "state.start",
            CurrentNodeId = "state.start",
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["state.start"] = new StateNode
                {
                    Id = "state.start",
                    Name = "Start",
                    WorkflowPhase = "Test",
                    Groups = [new TransitionGroup { Id = "group.start", TransitionIds = [consumer.Id] }],
                },
                ["state.produce"] = new StateNode
                {
                    Id = "state.produce",
                    Name = "Produce",
                    WorkflowPhase = "Test",
                    Groups = [new TransitionGroup { Id = "group.produce", TransitionIds = [producer.Id] }],
                },
                [consumer.Id] = consumer,
                [producer.Id] = producer,
            },
        };

        var report = new SkillWorkflowDataflowAnalyzer().Analyze(instance);

        var issue = Assert.Single(report.Issues);
        Assert.Equal(consumer.Id, issue.TransitionId);
        Assert.Equal("review_findings", issue.OutputFamily);
        Assert.Contains("before this transition", issue.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void DataflowAnalyzer_AcceptsProducerBeforeConsumerInsideCycle()
    {
        // Cycle: state.start ->(producer)-> state.consume ->(consumer back edge)-> state.start.
        // The producer runs before the consumer on every pass, so earliest-arrival ordering accepts it.
        var producer = new CommandTransition
        {
            Id = "transition.produce",
            Name = "Produce before consumer",
            TargetNodeId = "state.consume",
            StepKind = WorkflowStepKind.ToolCall,
            OutputPath = "future_output",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };
        var consumer = new CommandTransition
        {
            Id = "transition.consume",
            Name = "Consume produced output",
            TargetNodeId = "state.start",
            StepKind = WorkflowStepKind.ToolCall,
            PublishesOutputFamilies = ["review_findings"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["review_findings"] = "$context:future_output.findings",
                    },
                },
            },
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "cycle-ordered-producer",
            StartNodeId = "state.start",
            CurrentNodeId = "state.start",
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["state.start"] = new StateNode
                {
                    Id = "state.start",
                    Name = "Start",
                    WorkflowPhase = "Test",
                    Groups = [new TransitionGroup { Id = "group.start", TransitionIds = [producer.Id] }],
                },
                ["state.consume"] = new StateNode
                {
                    Id = "state.consume",
                    Name = "Consume",
                    WorkflowPhase = "Test",
                    Groups = [new TransitionGroup { Id = "group.consume", TransitionIds = [consumer.Id] }],
                },
                [producer.Id] = producer,
                [consumer.Id] = consumer,
            },
        };

        var report = new SkillWorkflowDataflowAnalyzer().Analyze(instance);

        Assert.True(report.IsResolved, string.Join("; ", report.Issues.Select(issue => $"{issue.TransitionId}: {issue.Reason}")));
    }

        [Fact]
        public void DataflowAnalyzer_RejectsProducerOnOnlyOneBranchBeforeConsumer()
        {
            var producer = new CommandTransition
            {
                Id = "transition.produce",
                Name = "Produce on one branch",
                TargetNodeId = "state.join",
                StepKind = WorkflowStepKind.ToolCall,
                OutputPath = "future_output",
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.Tool,
                    Name = "noop",
                    Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
                },
            };
            var bypass = new CommandTransition
            {
                Id = "transition.bypass",
                Name = "Bypass producer",
                TargetNodeId = "state.join",
                StepKind = WorkflowStepKind.ToolCall,
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.Tool,
                    Name = "noop",
                    Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
                },
            };
            var consumer = new CommandTransition
            {
                Id = "transition.consume",
                Name = "Consume after branch join",
                TargetNodeId = "state.done",
                StepKind = WorkflowStepKind.ToolCall,
                PublishesOutputFamilies = ["review_findings"],
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.Tool,
                    Name = "noop",
                    Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["review_findings"] = "$context:future_output.findings",
                        },
                    },
                },
            };
            var instance = new WorkflowInstance
            {
                InstanceId = "branch-future-producer",
                StartNodeId = "state.start",
                CurrentNodeId = "state.start",
                EndNodeId = "state.done",
                Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
                {
                    ["state.start"] = new StateNode
                    {
                        Id = "state.start",
                        Name = "Start",
                        WorkflowPhase = "Test",
                        Groups = [new TransitionGroup { Id = "group.start", TransitionIds = [producer.Id, bypass.Id] }],
                    },
                    ["state.join"] = new StateNode
                    {
                        Id = "state.join",
                        Name = "Join",
                        WorkflowPhase = "Test",
                        Groups = [new TransitionGroup { Id = "group.join", TransitionIds = [consumer.Id] }],
                    },
                    ["state.done"] = new StateNode { Id = "state.done", Name = "Done", WorkflowPhase = "Done", Groups = [] },
                    [producer.Id] = producer,
                    [bypass.Id] = bypass,
                    [consumer.Id] = consumer,
                },
            };

            var report = new SkillWorkflowDataflowAnalyzer().Analyze(instance);

            var issue = Assert.Single(report.Issues);
            Assert.Equal(consumer.Id, issue.TransitionId);
            Assert.Equal("review_findings", issue.OutputFamily);
            Assert.Contains("before this transition", issue.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public async Task StartOrAdvanceAsync_GateEvaluationDistinguishesEmptyOutputFamily()
    {
        var transition = new CommandTransition
        {
            Id = "transition.emit",
            Name = "Emit empty evidence",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            OutputPath = "unresolved_findings",
            SucceedExpression = "true",
            SatisfiesGateIds = ["gate.review"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };
        var instance = CreateExternalWorkflow("empty-family", transition);
        instance.TemplateKind = "explicit-workflow-graph";
        instance.Validation = new WorkflowValidationContract
        {
            Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
            {
                ["gate.review"] = new WorkflowValidationGate
                {
                    PassExpression = "context.Get<bool>(\"gate_outputs_present\")",
                    RequiredOutputFamilies = ["unresolved_findings"],
                    ValueSemantics = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["unresolved_findings"] = "nonEmptyArray",
                    },
                    FailureGuidance = new WorkflowGateFailureGuidance
                    {
                        NextAction = "Publish a non-empty unresolved findings summary object.",
                    },
                },
            },
        };
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));
        var result = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.Equal(WorkflowStatus.Failed, result.StatusProjection.Status);
        var saved = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(saved?.LastGateEvaluation);
        Assert.False(saved!.LastGateEvaluation!.Passed);
        Assert.Equal("empty_output_family", saved.LastGateEvaluation.FailedCheck);
        Assert.Equal(["unresolved_findings"], saved.LastGateEvaluation.EmptyOutputFamilies);
        Assert.Empty(saved.LastGateEvaluation.MissingOutputFamilies);
        Assert.Contains("non-empty unresolved findings summary", result.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

        [Fact]
        public void DataflowAnalyzer_IgnoresUnreachableTransitionSourceState()
        {
            var producer = new CommandTransition
            {
                Id = "transition.produce",
                Name = "Produce output",
                TargetNodeId = "state.consume",
                StepKind = WorkflowStepKind.ToolCall,
                OutputPath = "future_output",
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.Tool,
                    Name = "noop",
                    Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
                },
            };
            var consumer = new CommandTransition
            {
                Id = "transition.consume",
                Name = "Consume output",
                TargetNodeId = "state.after-consume",
                StepKind = WorkflowStepKind.ToolCall,
                PublishesOutputFamilies = ["review_findings"],
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.Tool,
                    Name = "noop",
                    Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["review_findings"] = "$context:future_output.findings",
                        },
                    },
                },
            };
            var downstream = new CommandTransition
            {
                Id = "transition.downstream",
                Name = "Read output after consumer",
                TargetNodeId = "state.done",
                StepKind = WorkflowStepKind.ToolCall,
                PublishesOutputFamilies = ["downstream_findings"],
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.Tool,
                    Name = "noop",
                    Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["downstream_findings"] = "$context:future_output.findings",
                        },
                    },
                },
            };
            var instance = new WorkflowInstance
            {
                InstanceId = "unreachable-transition-source",
                StartNodeId = "state.start",
                CurrentNodeId = "state.start",
                EndNodeId = "state.done",
                Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
                {
                    ["state.start"] = new StateNode
                    {
                        Id = "state.start",
                        Name = "Start",
                        WorkflowPhase = "Test",
                        Groups = [new TransitionGroup { Id = "group.start", TransitionIds = [producer.Id] }],
                    },
                    ["state.unreachable"] = new StateNode
                    {
                        Id = "state.unreachable",
                        Name = "Unreachable",
                        WorkflowPhase = "Test",
                        Groups = [new TransitionGroup { Id = "group.unreachable", TransitionIds = [consumer.Id] }],
                    },
                    ["state.consume"] = new StateNode
                    {
                        Id = "state.consume",
                        Name = "Consume",
                        WorkflowPhase = "Test",
                        Groups = [new TransitionGroup { Id = "group.consume", TransitionIds = [consumer.Id] }],
                    },
                    ["state.after-consume"] = new StateNode
                    {
                        Id = "state.after-consume",
                        Name = "After consume",
                        WorkflowPhase = "Test",
                        Groups = [new TransitionGroup { Id = "group.after-consume", TransitionIds = [downstream.Id] }],
                    },
                    ["state.done"] = new StateNode { Id = "state.done", Name = "Done", WorkflowPhase = "Done", Groups = [] },
                    [producer.Id] = producer,
                    [consumer.Id] = consumer,
                    [downstream.Id] = downstream,
                    },
                };

            var report = new SkillWorkflowDataflowAnalyzer().Analyze(instance);

            Assert.True(report.IsResolved, string.Join("; ", report.Issues.Select(issue => $"{issue.TransitionId}: {issue.Reason}")));
        }

        private static async Task<Techne.Loom.Abstractions.TaskTracking.WorkflowInstanceStatus> ResumeWithActiveWaitIdAsync(
            DefaultWorkflowTaskTrackingService service,
            string instanceId,
            string transitionId,
            string? correlationKey = null,
            Dictionary<string, object?>? payload = null,
            string? resultId = null)
        {
            var instance = await service.GetInstanceAsync(instanceId)
                ?? throw new InvalidOperationException($"Workflow instance '{instanceId}' was not found.");
            var waitGroup = instance.ActiveWaitGroups.Single(group =>
                string.Equals(group.TransitionId, transitionId, StringComparison.Ordinal)
                && (correlationKey is null || string.Equals(group.CorrelationKey, correlationKey, StringComparison.Ordinal)));
            var entry = waitGroup.GetNextPendingEntry()
                ?? throw new InvalidOperationException($"Wait group '{transitionId}' has no pending entry.");
            return await service.ResumeAsync(instanceId, transitionId, waitGroup.CorrelationKey, payload, resultId, waitId: entry.WaitId);
        }

        private static async Task<DefaultWorkflowTaskTrackingService> StartFanoutWorkflowAsync(WorkflowInstance instance)
        {
            var store = new InMemoryInstanceStore();
            await store.SaveNewAsync(instance);
            var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));
            var initial = await service.StartOrAdvanceAsync(instance.InstanceId);
            Assert.True(initial.Suspended);
            return service;
        }

        private static WorkflowInstance CreateExternalWorkflow(string instanceId, CommandTransition transition)
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
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [transition.Id] = transition,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }
}
