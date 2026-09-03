using System.Diagnostics;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Analysis;
using Techne.Loom.SkillOrchestrator.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

/// <summary>
/// Regression coverage for the 0.3.282 emitter-aware fail-closed dataflow semantic matrix (D1-D5).
/// The governed analyzer must reject known-null and update-less emitters as producers while still
/// accepting real tool results, declared literal writes, and external resume projections.
/// </summary>
public sealed class RuntimeSemantic03282Tests
{
    private const string GovernedTemplateKind = "so-governed-target-skill";

    [Fact]
    public void D1a_Governed_NoopToolOutputPath_IsNeverAProducer()
    {
        // D1: the built-in 'noop' tool returns null; writing null to outputPath is not a producer.
        var transition = new CommandTransition
        {
            Id = "transition.noop",
            Name = "No-op with output path",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            OutputPath = "audit_record",
            PublishesOutputFamilies = ["audit_record"],
            Command = Tool("noop"),
        };

        var report = Analyze(GovernedInstance("d1a-noop-output-path", transition));

        var issue = Assert.Single(report.Issues);
        Assert.Equal(transition.Id, issue.TransitionId);
        Assert.Equal("audit_record", issue.OutputFamily);
        Assert.Equal(WorkflowEmitterKind.KnownNull, issue.EmitterKind);
        Assert.Contains("known-null tool ('noop')", issue.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void D1b_Governed_NoopToolDollarResult_IsNeverAProducer()
    {
        // D1: a $result binding on a known-null emitter resolves to null and cannot publish.
        var transition = new CommandTransition
        {
            Id = "transition.noop",
            Name = "No-op with dollar result",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            PublishesOutputFamilies = ["audit_record"],
            Command = Tool("noop", parameters: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["audit_record"] = "$result",
                },
            }),
        };

        var report = Analyze(GovernedInstance("d1b-noop-dollar-result", transition));

        var issue = Assert.Single(report.Issues);
        Assert.Equal(transition.Id, issue.TransitionId);
        Assert.Equal("audit_record", issue.OutputFamily);
        Assert.Equal(WorkflowEmitterKind.KnownNull, issue.EmitterKind);
        Assert.Contains("known-null tool ('noop')", issue.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void D2_Governed_StateUpdateWithoutCoveringUpdates_IsNotAProducer()
    {
        // D2: state.update writes only its declared updates map; a published family outside that
        // map (even via $context on its own outputPath) has no concrete producer.
        var transition = new CommandTransition
        {
            Id = "transition.state-update",
            Name = "State update missing the family",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.StateUpdate,
            OutputPath = "audit_record",
            PublishesOutputFamilies = ["audit_record"],
            Command = Tool("workflow.updateState", parameters: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "in_progress",
                },
                ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["audit_record"] = "$context:audit_record",
                },
            }),
        };

        var report = Analyze(GovernedInstance("d2-state-update-missing-family", transition));

        var issue = Assert.Single(report.Issues);
        Assert.Equal(transition.Id, issue.TransitionId);
        Assert.Equal("audit_record", issue.OutputFamily);
        Assert.Equal(WorkflowEmitterKind.LiteralWriter, issue.EmitterKind);
        Assert.Contains("writes only its declared updates map", issue.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void D3a_Governed_StateUpdateWithCoveringUpdates_IsAProducer()
    {
        // D3a: when a declared update key covers the outputPath, the family is concretely written.
        var transition = new CommandTransition
        {
            Id = "transition.state-update",
            Name = "State update covering the family",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.StateUpdate,
            OutputPath = "audit_record",
            PublishesOutputFamilies = ["audit_record"],
            Command = Tool("workflow.updateState", parameters: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["audit_record"] = "recorded",
                },
            }),
        };

        var report = Analyze(GovernedInstance("d3a-state-update-covering-family", transition));

        Assert.True(report.IsResolved, string.Join("; ", report.Issues.Select(issue => $"{issue.TransitionId}: {issue.Reason}")));
    }

    [Fact]
    public void D3b_Governed_StateUpdateDollarResultOnCoveredOutputPath_IsAProducer()
    {
        // D3b: $result on a state.update is the value at its own outputPath after updates are applied.
        var transition = new CommandTransition
        {
            Id = "transition.state-update",
            Name = "State update dollar result",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.StateUpdate,
            OutputPath = "audit_record",
            PublishesOutputFamilies = ["audit_record"],
            Command = Tool("workflow.updateState", parameters: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["audit_record"] = "recorded",
                },
                ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["audit_record"] = "$result",
                },
            }),
        };

        var report = Analyze(GovernedInstance("d3b-state-update-dollar-result", transition));

        Assert.True(report.IsResolved, string.Join("; ", report.Issues.Select(issue => $"{issue.TransitionId}: {issue.Reason}")));
    }

    [Fact]
    public void D4_Governed_ExternalResumeSubpathProjection_IsAProducer()
    {
        // D4: an external step's resume payload is available at the transition; a $context binding to
        // a subpath of its own outputPath (the resumed value) is a legitimate projection.
        var transition = new CommandTransition
        {
            Id = "transition.review",
            Name = "External review resume",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.WaitResume,
            OutputPath = "review_payload",
            PublishesOutputFamilies = ["review_findings"],
            Command = Tool("workflow.requestReview", parameters: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["resumeOutputKey"] = "review_payload",
                ["requiredInputs"] = new List<object?> { "review_payload" },
                ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["review_findings"] = "$context:review_payload.findings",
                },
            }),
        };

        var report = Analyze(GovernedInstance("d4-external-resume-subpath", transition));

        Assert.True(report.IsResolved, string.Join("; ", report.Issues.Select(issue => $"{issue.TransitionId}: {issue.Reason}")));
    }

    [Fact]
    public void D5_Governed_RealToolDollarResult_IsAProducer()
    {
        // D5: real built-in tools (echo, write-file, ...) produce non-null results; $result is usable.
        var transition = new CommandTransition
        {
            Id = "transition.echo",
            Name = "Echo result",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            OutputPath = "echo_result",
            PublishesOutputFamilies = ["echo_result"],
            Command = Tool("echo", parameters: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["message"] = "hello-0.3.282",
                ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["echo_result"] = "$result",
                },
            }),
        };

        var report = Analyze(GovernedInstance("d5-real-tool-dollar-result", transition));

        Assert.True(report.IsResolved, string.Join("; ", report.Issues.Select(issue => $"{issue.TransitionId}: {issue.Reason}")));
    }

    [Fact]
    public void Legacy_Ungoverned_NoopDollarResult_RemainsAccepted()
    {
        // The governed fail-closed policy must not leak into ungoverned workflows: the legacy rule set
        // still treats $result on any emitter as a concrete producer.
        var transition = new CommandTransition
        {
            Id = "transition.noop",
            Name = "Legacy no-op dollar result",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            PublishesOutputFamilies = ["audit_record"],
            Command = Tool("noop", parameters: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["audit_record"] = "$result",
                },
            }),
        };

        var report = Analyze(UngovernedInstance("legacy-noop-dollar-result", transition));

        Assert.True(report.IsResolved, string.Join("; ", report.Issues.Select(issue => $"{issue.TransitionId}: {issue.Reason}")));
    }

    [Fact]
    public void Governed_GateRequiredFamilyOnKnownNullPublisher_FailsAtGateLevel()
    {
        // A gate whose only satisfying publisher is a known-null emitter has no reachable concrete producer.
        var transition = new CommandTransition
        {
            Id = "transition.noop",
            Name = "No-op gate publisher",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            OutputPath = "audit_record",
            SatisfiesGateIds = ["gate.audit"],
            PublishesOutputFamilies = ["audit_record"],
            Command = Tool("noop"),
        };

        var instance = GovernedInstance("gate-known-null-publisher", transition);
        instance.Validation = new WorkflowValidationContract
        {
            Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
            {
                ["gate.audit"] = new WorkflowValidationGate
                {
                    RequiredOutputFamilies = ["audit_record"],
                },
            },
        };

        var report = Analyze(instance);

        // The known-null publisher is flagged both at the transition level and at the gate level.
        Assert.Equal(2, report.Issues.Count);
        var gateIssue = Assert.Single(report.Issues, issue => issue.GateId == "gate.audit");
        Assert.Null(gateIssue.TransitionId);
        Assert.Equal("audit_record", gateIssue.OutputFamily);
        Assert.Equal(WorkflowEmitterKind.KnownNull, gateIssue.EmitterKind);
        Assert.Contains("no reachable concrete producer on a gate-satisfying transition", gateIssue.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compile_Governed_NoopProducer_FailsWithKnownNullDataflowDiagnostic()
    {
        // End-to-end: the governed compile pipeline surfaces the emitter-aware dataflow diagnostic with
        // the [emitter:KnownNull] location note under rule SO3000. (Note: C# string interpolation escapes
        // single quotes as \u0027, so assertions use quote-free substrings.)
        var transition = new CommandTransition
        {
            Id = "transition.noop",
            Name = "No-op producer",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            OutputPath = "audit_record",
            PublishesOutputFamilies = ["audit_record"],
            Command = Tool("noop"),
        };

        var instance = GovernedInstance("compile-known-null-producer", transition);
        var workflowFile = WriteTemporaryWorkflow(instance, "semantic-03282-known-null");
        try
        {
            var (exitCode, output) = await RunCompileAsync(workflowFile);

            Assert.NotEqual(0, exitCode);
            Assert.Contains("SO3000", output, StringComparison.Ordinal);
            Assert.Contains("Dataflow validation failed for output family", output, StringComparison.Ordinal);
            Assert.Contains("[emitter:KnownNull]", output, StringComparison.Ordinal);
            Assert.Contains("known-null tool (noop)", output.Replace("\\u0027", string.Empty), StringComparison.Ordinal);
        }
        finally
        {
            DeleteIfExists(workflowFile);
        }
    }

    [Fact]
    public void Governed_NoopOutputPath_DoesNotBecomePriorProducer()
    {
        // A known-null output must not enter the guaranteed context set for later transitions.
        var noop = new CommandTransition
        {
            Id = "transition.noop",
            Name = "No-op stale output",
            TargetNodeId = "state.middle",
            StepKind = WorkflowStepKind.ToolCall,
            OutputPath = "stale_record",
            Command = Tool("noop"),
        };
        var consumer = new CommandTransition
        {
            Id = "transition.consumer",
            Name = "Consumer of stale output",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            PublishesOutputFamilies = ["final_record"],
            Command = Tool("echo", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["message"] = "consumer",
                ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["final_record"] = "$context:stale_record",
                },
            }),
        };
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Test",
            Groups = [new TransitionGroup { Id = "group.start", TransitionIds = [noop.Id] }],
        };
        var middle = new StateNode
        {
            Id = "state.middle",
            Name = "Middle",
            WorkflowPhase = "Test",
            Groups = [new TransitionGroup { Id = "group.middle", TransitionIds = [consumer.Id] }],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "Done",
            Groups = [],
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "known-null-prior-producer",
            TemplateKind = GovernedTemplateKind,
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [middle.Id] = middle,
                [done.Id] = done,
                [noop.Id] = noop,
                [consumer.Id] = consumer,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };

        var report = Analyze(instance);

        var issue = Assert.Single(report.Issues);
        Assert.Equal(consumer.Id, issue.TransitionId);
        Assert.Equal("final_record", issue.OutputFamily);
        Assert.Contains("no reachable concrete producer", issue.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runtime_03282_ToolCallNoopUpdatesRemainAbsentFromContext()
    {
        var transition = new CommandTransition
        {
            Id = "transition.noop",
            Name = "No-op updates",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            GuardExpression = "true",
            SucceedExpression = "true",
            Command = Tool("noop", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ignored.output"] = "must-not-be-written",
                },
            }),
        };
        var instance = CreateRuntimeWorkflow("runtime-noop-updates", transition);
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var result = await service.StartOrAdvanceAsync(instance.InstanceId);
        var saved = await service.GetInstanceAsync(instance.InstanceId);

        Assert.Equal(WorkflowStatus.Succeeded, result.StatusProjection.Status);
        Assert.NotNull(saved);
        Assert.False(saved!.Context.ContainsKey("ignored.output"));
        Assert.DoesNotContain(saved.History, entry => entry.ContextChanges?.ContainsKey("ignored.output") == true);
    }

    [Theory]
    [InlineData(WorkflowStepKind.StateUpdate, "workflow.updateState")]
    [InlineData(WorkflowStepKind.MemoryWrite, "memory.write")]
    public async Task Runtime_03282_LiteralWritersApplyNestedUpdates(WorkflowStepKind stepKind, string commandName)
    {
        var transition = new CommandTransition
        {
            Id = "transition.literal-write",
            Name = "Literal context write",
            TargetNodeId = "state.done",
            StepKind = stepKind,
            GuardExpression = "true",
            SucceedExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.NativeCode,
                Name = commandName,
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["review.round"] = 2,
                        ["review.accepted"] = true,
                    },
                },
            },
        };
        var instance = CreateRuntimeWorkflow($"runtime-{stepKind}", transition);
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var result = await service.StartOrAdvanceAsync(instance.InstanceId);
        var saved = await service.GetInstanceAsync(instance.InstanceId);

        Assert.Equal(WorkflowStatus.Succeeded, result.StatusProjection.Status);
        Assert.NotNull(saved);
        Assert.Equal(2, Convert.ToInt32(PathValueAccessor.GetValue(saved!.Context, "review.round")));
        Assert.Equal(true, PathValueAccessor.GetValue(saved.Context, "review.accepted"));
    }

    [Fact]
    public async Task Runtime_03282_EchoDollarResultIsProjected()
    {
        var transition = new CommandTransition
        {
            Id = "transition.echo",
            Name = "Echo result",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            OutputPath = "echo.result",
            GuardExpression = "true",
            SucceedExpression = "context.Get<string>(\"echo.result\") == \"hello-0.3.282\" && context.Get<string>(\"echo.copy\") == \"hello-0.3.282\"",
            Command = Tool("echo", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["message"] = "hello-0.3.282",
                ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["echo.copy"] = "$result",
                },
            }),
        };
        var instance = CreateRuntimeWorkflow("runtime-echo-result", transition);
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var result = await service.StartOrAdvanceAsync(instance.InstanceId);
        var saved = await service.GetInstanceAsync(instance.InstanceId);

        Assert.Equal(WorkflowStatus.Succeeded, result.StatusProjection.Status);
        Assert.NotNull(saved);
        Assert.Equal("hello-0.3.282", PathValueAccessor.GetValue(saved!.Context, "echo.result"));
        Assert.Equal("hello-0.3.282", PathValueAccessor.GetValue(saved.Context, "echo.copy"));
    }

    [Fact]
    public async Task Runtime_03282_WriteFileDollarResultIsProjectedAsPath()
    {
        var outputFile = Path.Combine(Path.GetTempPath(), $"loom-semantic-03282-{Guid.NewGuid():N}.txt");
        try
        {
            var transition = new CommandTransition
            {
                Id = "transition.write-file",
                Name = "Write evidence file",
                TargetNodeId = "state.done",
                StepKind = WorkflowStepKind.ToolCall,
                OutputPath = "evidence.path",
                GuardExpression = "true",
                SucceedExpression = "context.Get<string>(\"evidence.path\") != null && context.Get<string>(\"evidence.copy\") != null",
                Command = Tool("write-file", new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = outputFile,
                    ["content"] = "semantic evidence",
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["evidence.copy"] = "$result",
                    },
                }),
            };
            var instance = CreateRuntimeWorkflow("runtime-write-file-result", transition);
            var store = new InMemoryInstanceStore();
            await store.SaveNewAsync(instance);
            var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

            var result = await service.StartOrAdvanceAsync(instance.InstanceId);
            var saved = await service.GetInstanceAsync(instance.InstanceId);

            Assert.Equal(WorkflowStatus.Succeeded, result.StatusProjection.Status);
            Assert.NotNull(saved);
            var evidencePath = Assert.IsType<string>(PathValueAccessor.GetValue(saved!.Context, "evidence.path"));
            var copiedPath = Assert.IsType<string>(PathValueAccessor.GetValue(saved.Context, "evidence.copy"));
            Assert.Equal(outputFile, evidencePath);
            Assert.Equal(outputFile, copiedPath);
            Assert.Equal("semantic evidence", await File.ReadAllTextAsync(outputFile));
        }
        finally
        {
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }
        }
    }

    [Fact]
    public void GovernedDataflow_AcceptsProducerBeforeBranchBackEdge()
    {
        var producer = new CommandTransition
        {
            Id = "transition.producer",
            Name = "Produce before branch",
            TargetNodeId = "state.branch",
            StepKind = WorkflowStepKind.StateUpdate,
            OutputPath = "probe.value",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.NativeCode,
                Name = "state.update",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["probe.value"] = "produced",
                        ["probe.round"] = 0,
                    },
                },
            },
        };
        var takeA = new CommandTransition
        {
            Id = "transition.take-a",
            Name = "Take first branch",
            TargetNodeId = "state.join",
            StepKind = WorkflowStepKind.MemoryWrite,
            OutputPath = "probe.round",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.NativeCode,
                Name = "memory.write",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["probe.round"] = 1,
                    },
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["probe.first_echo"] = "$context:probe.value",
                    },
                },
            },
        };
        var takeB = new CommandTransition
        {
            Id = "transition.take-b",
            Name = "Take final branch",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.MemoryWrite,
            OutputPath = "probe.final",
            PublishesOutputFamilies = ["final_record"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.NativeCode,
                Name = "memory.write",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["probe.final"] = "done",
                    },
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["final_record"] = "$context:probe.value",
                    },
                },
            },
        };
        var joinBack = new CommandTransition
        {
            Id = "transition.join-back",
            Name = "Join back",
            TargetNodeId = "state.branch",
            StepKind = WorkflowStepKind.ConditionBranch,
            GuardExpression = "true",
            SucceedExpression = "true",
            Command = Tool("noop"),
        };
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Start",
            Groups = [new TransitionGroup { Id = "group.start", TransitionIds = [producer.Id] }],
        };
        var branch = new StateNode
        {
            Id = "state.branch",
            Name = "Branch",
            WorkflowPhase = "Branch",
            Groups = [new TransitionGroup { Id = "group.branch", TransitionIds = [takeA.Id, takeB.Id] }],
        };
        var join = new StateNode
        {
            Id = "state.join",
            Name = "Join",
            WorkflowPhase = "Join",
            Groups = [new TransitionGroup { Id = "group.join", TransitionIds = [joinBack.Id] }],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "Done",
            Groups = [],
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "governed-cycle-ordered-producer",
            TemplateKind = GovernedTemplateKind,
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [branch.Id] = branch,
                [join.Id] = join,
                [done.Id] = done,
                [producer.Id] = producer,
                [takeA.Id] = takeA,
                [takeB.Id] = takeB,
                [joinBack.Id] = joinBack,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };

        var report = Analyze(instance);

        Assert.True(report.IsResolved, string.Join("; ", report.Issues.Select(issue => $"{issue.TransitionId}: {issue.Reason}")));
    }

    [Fact]
    public void Governed_UnknownTool_IsNotAProducer()
    {
        var transition = new CommandTransition
        {
            Id = "transition.unknown-tool",
            Name = "Unknown producer",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            OutputPath = "unknown_record",
            PublishesOutputFamilies = ["unknown_record"],
            Command = Tool("future-tool"),
        };

        var report = Analyze(GovernedInstance("unknown-tool-producer", transition));

        var issue = Assert.Single(report.Issues);
        Assert.Equal(transition.Id, issue.TransitionId);
        Assert.Equal("unknown_record", issue.OutputFamily);
        Assert.Equal(WorkflowEmitterKind.Unknown, issue.EmitterKind);
    }

    [Fact]
    public void Governed_EchoWithoutMessage_IsNotAProducer()
    {
        var transition = new CommandTransition
        {
            Id = "transition.echo",
            Name = "Empty echo producer",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            OutputPath = "echo_record",
            PublishesOutputFamilies = ["echo_record"],
            Command = Tool("echo"),
        };

        var report = Analyze(GovernedInstance("empty-echo-producer", transition));

        var issue = Assert.Single(report.Issues);
        Assert.Equal(transition.Id, issue.TransitionId);
        Assert.Equal("echo_record", issue.OutputFamily);
        Assert.Equal(WorkflowEmitterKind.KnownNull, issue.EmitterKind);
    }

    private static WorkflowInstance CreateRuntimeWorkflow(string instanceId, CommandTransition transition)
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

    private static SkillWorkflowDataflowReport Analyze(WorkflowInstance instance)
        => new SkillWorkflowDataflowAnalyzer().Analyze(instance);

    private static WorkflowInstance GovernedInstance(string instanceId, CommandTransition transition)
        => BuildInstance(instanceId, transition, templateKind: GovernedTemplateKind);

    private static WorkflowInstance UngovernedInstance(string instanceId, CommandTransition transition)
        => BuildInstance(instanceId, transition, templateKind: null);

    private static WorkflowInstance BuildInstance(string instanceId, CommandTransition transition, string? templateKind)
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
            TemplateKind = templateKind,
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

    private static CommandInvocation Tool(string name, Dictionary<string, object?>? parameters = null)
        => new()
        {
            Kind = CommandInvocationKind.Tool,
            Name = name,
            Parameters = parameters ?? new Dictionary<string, object?>(StringComparer.Ordinal),
        };

    private static string WriteTemporaryWorkflow(WorkflowInstance workflow, string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"techne-loom-{name}-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, WorkflowJsonSerializer.Serialize(workflow));
        return path;
    }

    private static async Task<(int ExitCode, string Output)> RunCompileAsync(string workflowFile)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = FindRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(typeof(DefaultWorkflowTaskTrackingService).Assembly.Location);
        startInfo.ArgumentList.Add("compile");
        startInfo.ArgumentList.Add("--workflow-file");
        startInfo.ArgumentList.Add(workflowFile);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start SO compile process.");
        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, standardOutput + Environment.NewLine + standardError);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Techne.Loom.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
