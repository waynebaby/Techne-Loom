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
                    Strategy = ConcurrencyStrategy.All,
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
