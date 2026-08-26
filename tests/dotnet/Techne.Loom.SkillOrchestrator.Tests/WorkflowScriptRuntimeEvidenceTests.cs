using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Abstractions.TaskTracking;
using Techne.Loom.Abstractions.TaskTracking.Runtime;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowScriptRuntimeEvidenceTests
{
    [Fact]
    public async Task StateUpdateHistoryContainsAppliedContextChanges()
    {
        var instance = CreateStateUpdateWorkflow();
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var result = await service.StartOrAdvanceAsync(instance.InstanceId);
        var saved = await service.GetInstanceAsync(instance.InstanceId);

        Assert.Equal(WorkflowStatus.Succeeded, result.StatusProjection.Status);
        var entry = Assert.Single(saved!.History, item => item.NodeId == "transition.update" && item.Status == ExecutionStatus.Succeeded);
        Assert.NotNull(entry.ContextChanges);
        Assert.Equal("updated", entry.ContextChanges!["result"]);
    }

    [Fact]
    public async Task ArtifactEmitHistoryContainsPathAndContentEvidence()
    {
        var artifactPath = Path.Combine(Path.GetTempPath(), $"loom-runtime-artifact-{Guid.NewGuid():N}.txt");
        var instance = CreateArtifactWorkflow(artifactPath);
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        try
        {
            var result = await service.StartOrAdvanceAsync(instance.InstanceId);
            var saved = await service.GetInstanceAsync(instance.InstanceId);

            Assert.Equal(WorkflowStatus.Succeeded, result.StatusProjection.Status);
            Assert.Equal("artifact content", await File.ReadAllTextAsync(artifactPath));
            var entry = Assert.Single(saved!.History, item => item.NodeId == "transition.artifact" && item.Status == ExecutionStatus.Succeeded);
            Assert.Equal(artifactPath, entry.ContextChanges!["artifact"]);
            Assert.Equal("artifact content", entry.ContextChanges["artifact.content"]);
        }
        finally
        {
            if (File.Exists(artifactPath))
            {
                File.Delete(artifactPath);
            }
        }
    }

    [Fact]
    public void StatusAloneDoesNotCountAsRuntimeEvidence()
    {
        var workflow = WorkflowSchemaDemoExporter.CreateDemoWorkflow("dotnet-so");
        workflow.Status = WorkflowStatus.Succeeded;
        workflow.CurrentNodeId = workflow.EndNodeId!;
        var model = CreateModelReference("dotnet-so");

        var result = WorkflowScriptModelVerifier.Verify(workflow, workflow, model);

        Assert.False(result.RuntimeEvidenceObserved);
        Assert.Contains(result.TestCases, item => item.Id == "runtime.terminal_route" && item.Skipped);
        Assert.Contains(result.TestCases, item => item.Id == "runtime.artifact.transition.emit_artifact" && item.Skipped);
    }

    [Fact]
    public void PersistedMissingAndEmptyGateEvidenceFailsBlockedVerification()
    {
        var workflow = WorkflowSchemaDemoExporter.CreateDemoWorkflow("dotnet-so");
        workflow.Status = WorkflowStatus.WaitingExternal;
        workflow.ActiveWaitGroups.Add(new PendingWaitGroup
        {
            InstanceId = workflow.InstanceId,
            TransitionId = "transition.model_think",
        });
        workflow.History.Add(new WorkflowHistoryEntry(
            DateTimeOffset.UtcNow,
            "transition.model_think",
            TaskNodeType.Transition,
            ExecutionStatus.Suspended));
        workflow.LastGateEvaluation = new GateEvaluationResult
        {
            InstanceId = workflow.InstanceId,
            TransitionId = "transition.model_think",
            Passed = false,
            MissingOutputFamilies = ["tool_summary"],
            EmptyOutputFamilies = ["model_summary"],
        };

        var result = WorkflowScriptModelVerifier.Verify(workflow, workflow, CreateModelReference("dotnet-so"));

        Assert.False(result.RuntimeEvidenceObserved);
        Assert.Contains(result.TestCases, item => item.Id == "runtime.gate_evidence" && !item.Passed);
        Assert.Contains(result.TestCases, item => item.Id == "runtime.provenance" && !item.Passed);
        Assert.Contains(result.TestCases, item => item.Id == "runtime.blocked_route" && item.Skipped);
    }

    [Fact]

    public void SyntheticArtifactHistoryDoesNotPassVerification()

    {

        var artifactPath = Path.Combine(Path.GetTempPath(), $"loom-synthetic-artifact-{Guid.NewGuid():N}.txt");

        var workflow = CreateArtifactWorkflow(artifactPath);

        workflow.Status = WorkflowStatus.Succeeded;

        workflow.CurrentNodeId = workflow.EndNodeId!;

        workflow.History.Add(new WorkflowHistoryEntry(

            DateTimeOffset.UtcNow,

            "transition.artifact",

            TaskNodeType.Transition,

            ExecutionStatus.Succeeded,

            new Dictionary<string, object?>(StringComparer.Ordinal)

            {

                ["artifact"] = artifactPath,

                ["artifact.content"] = "artifact content",

            }));



        try

        {

            File.WriteAllText(artifactPath, "artifact content");

            var result = WorkflowScriptModelVerifier.Verify(workflow, workflow, CreateModelReference("dotnet-so"));



            Assert.False(result.Passed);

            Assert.False(result.RuntimeEvidenceObserved);

            Assert.Contains(result.TestCases, item => item.Id == "runtime.provenance" && !item.Passed);

            Assert.Contains(result.TestCases, item => item.Id == "runtime.artifact.transition.artifact" && item.Skipped);

        }

        finally

        {

            if (File.Exists(artifactPath))

            {

                File.Delete(artifactPath);

            }

        }

    }



    [Fact]

    public async Task GateEvidenceRequiresPassedEvaluationAndSuccessfulTransitionHistory()

    {

        var workflow = CreateStateUpdateWorkflow();

        workflow.Validation = new WorkflowValidationContract

        {

            Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)

            {

                ["gate.final"] = new WorkflowValidationGate

                {

                    PassExpression = new ExpressionDefinition { Source = "true" },

                    RequiredOutputFamilies = ["result"],

                    ValueSemantics = new Dictionary<string, string>(StringComparer.Ordinal)

                    {

                        ["result"] = "nonEmptyString",

                    },

                },

            },

        };



        var store = new InMemoryInstanceStore();

        await store.SaveNewAsync(workflow);

        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        await service.StartOrAdvanceAsync(workflow.InstanceId);

        var observed = await service.GetInstanceAsync(workflow.InstanceId);

        Assert.NotNull(observed);

        Assert.NotNull(observed!.LastGateEvaluation);

        Assert.True(WorkflowRuntimeEvidenceRegistry.IsObserved(observed));



        observed.LastGateEvaluation = observed.LastGateEvaluation! with { Passed = false };

        var result = WorkflowScriptModelVerifier.Verify(observed, observed, CreateModelReference("dotnet-so"));



        Assert.Contains(result.TestCases, item => item.Id == "runtime.gate_evidence" && !item.Passed);

        observed.LastGateEvaluation = observed.LastGateEvaluation! with { Passed = true };

        var staleHistory = WorkflowInstanceCloner.Clone(observed);

        staleHistory.History.Add(new WorkflowHistoryEntry(

            DateTimeOffset.UtcNow,

            "transition.update",

            TaskNodeType.Transition,

            ExecutionStatus.Failed));

        var staleResult = WorkflowScriptModelVerifier.Verify(staleHistory, staleHistory, CreateModelReference("dotnet-so"));

        Assert.Contains(staleResult.TestCases, item => item.Id == "runtime.gate_evidence" && !item.Passed);



        var wrongGate = WorkflowInstanceCloner.Clone(observed);

        wrongGate.LastGateEvaluation = wrongGate.LastGateEvaluation! with { GateId = "gate.other" };

        var wrongGateResult = WorkflowScriptModelVerifier.Verify(wrongGate, wrongGate, CreateModelReference("dotnet-so"));

        Assert.Contains(wrongGateResult.TestCases, item => item.Id == "runtime.gate_evidence" && !item.Passed);

    }

    [Fact]

    public async Task DraftingNoProgressDoesNotCreateRuntimeProvenance()

    {

        var workflow = CreateArtifactWorkflow(Path.Combine(Path.GetTempPath(), $"loom-no-progress-{Guid.NewGuid():N}.txt"));

        workflow.Status = WorkflowStatus.Drafting;

        var store = new InMemoryInstanceStore();

        await store.SaveNewAsync(workflow);

        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));



        var result = await service.StartOrAdvanceAsync(workflow.InstanceId);

        var saved = await service.GetInstanceAsync(workflow.InstanceId);



        Assert.False(result.Progressed);

        Assert.NotNull(saved);

        Assert.False(WorkflowRuntimeEvidenceRegistry.IsObserved(saved!));

    }



    [Fact]

    public async Task FailedTickDoesNotCreateRuntimeProvenance()

    {

        var workflow = CreateFailedWorkflow();

        var store = new InMemoryInstanceStore();

        await store.SaveNewAsync(workflow);

        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));



        var result = await service.StartOrAdvanceAsync(workflow.InstanceId);

        var saved = await service.GetInstanceAsync(workflow.InstanceId);



        Assert.True(result.Failed);

        Assert.NotNull(saved);

        Assert.False(WorkflowRuntimeEvidenceRegistry.IsObserved(saved!));

    }

    [Fact]
    public async Task SuccessfulResumeCarriesRuntimeProvenanceThroughStoreClone()
    {
        var workflow = CreateWaitingWorkflow();
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(workflow);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var status = await service.ResumeAsync(workflow.InstanceId, "transition.wait", "resume-correlation", new Dictionary<string, object?>());
        var saved = await service.GetInstanceAsync(workflow.InstanceId);

        Assert.Equal(WorkflowStatus.Running, status.Status);
        Assert.NotNull(saved);
        Assert.True(WorkflowRuntimeEvidenceRegistry.IsObserved(saved!));
        Assert.Contains(saved.History, entry => entry.NodeId == "transition.wait" && entry.Status == ExecutionStatus.Succeeded);
    }

    [Fact]
    public async Task FailedResumePersistenceRollsBackNewRuntimeProvenance()
    {
        var workflow = CreateWaitingWorkflow();
        var store = new RejectingUpdateStore();
        await store.SaveNewAsync(workflow);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(workflow.InstanceId, "transition.wait", "resume-correlation", new Dictionary<string, object?>()));

        Assert.NotNull(store.LastUpdateAttempt);
        Assert.False(WorkflowRuntimeEvidenceRegistry.IsObserved(store.LastUpdateAttempt!));
    }
    private static WorkflowModelReference CreateModelReference(string runtimeBinding)
    {
        var schema = WorkflowSchemaDemoExporter.CreateSchemaContract();
        return new WorkflowModelReference
        {
            SchemaId = schema.SchemaId,
            SchemaVersion = schema.SchemaVersion,
            RuntimeBinding = runtimeBinding,
            RootFields = schema.RootFields,
            NodeFields = schema.NodeFields,
            RequiredRootFields = schema.RequiredRootFields,
            RequiredNodeFields = schema.RequiredNodeFields,
            AllowedValues = schema.AllowedValues,
            ExpressionDefinitionFields = schema.ExpressionDefinitionFields,
            CommandParameterContracts = schema.CommandParameterContracts,
        };
    }

    private static WorkflowInstance CreateStateUpdateWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Start",
            Groups = [new TransitionGroup { Id = "group.update", TransitionIds = ["transition.update"] }],
        };
        var done = new StateNode { Id = "state.done", Name = "Done", WorkflowPhase = "Done", Groups = [] };
        var transition = new CommandTransition
        {
            Id = "transition.update",
            Name = "Update",
            WorkflowPhase = "Start",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.StateUpdate,
            SatisfiesGateIds = ["gate.final"],
            GuardExpression = "true",
            SucceedExpression = "context.Get<string>(\"result\") != null",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.NativeCode,
                Name = "update",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = "updated" },
                },
            },
        };
        return new WorkflowInstance
        {
            InstanceId = "runtime-update-evidence",
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

    private static WorkflowInstance CreateFailedWorkflow()

    {

        var start = new StateNode

        {

            Id = "state.start",

            Name = "Start",

            WorkflowPhase = "Start",

            Groups = [new TransitionGroup { Id = "group.failure", TransitionIds = ["transition.failure"] }],

        };

        var done = new StateNode { Id = "state.done", Name = "Done", WorkflowPhase = "Done", Groups = [] };

        var transition = new CommandTransition

        {

            Id = "transition.failure",

            Name = "Failure",

            WorkflowPhase = "Start",

            TargetNodeId = done.Id,

            StepKind = WorkflowStepKind.ToolCall,

            GuardExpression = "true",

            SucceedExpression = "false",

            Command = new CommandInvocation

            {

                Kind = CommandInvocationKind.NativeCode,

                Name = "noop",

            },

        };

        return new WorkflowInstance

        {

            InstanceId = "runtime-failed-evidence",

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

    private static WorkflowInstance CreateWaitingWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Start",
            Groups = [new TransitionGroup { Id = "group.wait", TransitionIds = ["transition.wait"] }],
        };
        var done = new StateNode { Id = "state.done", Name = "Done", WorkflowPhase = "Done", Groups = [] };
        var transition = new CommandTransition
        {
            Id = "transition.wait",
            Name = "Wait",
            WorkflowPhase = "Start",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.ModelThink,
            GuardExpression = "true",
            SucceedExpression = "true",
        };
        var waitGroup = new PendingWaitGroup
        {
            InstanceId = "runtime-resume-evidence",
            TransitionId = transition.Id,
            CorrelationKey = "resume-correlation",
            TargetStateId = done.Id,
            OriginStrategy = ConcurrencyStrategy.FirstSuccess,
        };
        waitGroup.AddEntry(expireAt: null);
        return new WorkflowInstance
        {
            InstanceId = "runtime-resume-evidence",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.WaitingExternal,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [transition.Id] = transition,
            },
            ActiveWaitGroups = [waitGroup],
        };
    }
    private static WorkflowInstance CreateArtifactWorkflow(string artifactPath)
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Start",
            Groups = [new TransitionGroup { Id = "group.artifact", TransitionIds = ["transition.artifact"] }],
        };
        var done = new StateNode { Id = "state.done", Name = "Done", WorkflowPhase = "Done", Groups = [] };
        var transition = new CommandTransition
        {
            Id = "transition.artifact",
            Name = "Artifact",
            WorkflowPhase = "Start",
            TargetNodeId = done.Id,
            OutputPath = "artifact",
            StepKind = WorkflowStepKind.ArtifactEmit,
            GuardExpression = "true",
            SucceedExpression = "context.Get<string>(\"artifact\") != null",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.NativeCode,
                Name = "artifact",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = artifactPath,
                    ["content"] = "artifact content",
                },
            },
        };
        return new WorkflowInstance
        {
            InstanceId = "runtime-artifact-evidence",
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

    private sealed class RejectingUpdateStore : IInstanceStore
    {
        private readonly InMemoryInstanceStore inner = new();

        public WorkflowInstance? LastUpdateAttempt { get; private set; }

        public Task SaveNewAsync(WorkflowInstance instance, CancellationToken ct = default) => inner.SaveNewAsync(instance, ct);

        public Task<WorkflowInstance?> GetAsync(string instanceId, CancellationToken ct = default) => inner.GetAsync(instanceId, ct);

        public Task<bool> TryUpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default)
        {
            LastUpdateAttempt = instance;
            return Task.FromResult(false);
        }

        public Task<bool> TryAppendHistoryAsync(string instanceId, WorkflowHistoryEntry entry, int expectedVersion, CancellationToken ct = default) => inner.TryAppendHistoryAsync(instanceId, entry, expectedVersion, ct);

        public Task<bool> TryAcquireLeaseAsync(string instanceId, string ownerId, TimeSpan ttl, CancellationToken ct = default) => inner.TryAcquireLeaseAsync(instanceId, ownerId, ttl, ct);

        public Task<bool> TryRenewLeaseAsync(string instanceId, string ownerId, TimeSpan ttl, CancellationToken ct = default) => inner.TryRenewLeaseAsync(instanceId, ownerId, ttl, ct);

        public Task ReleaseLeaseAsync(string instanceId, string ownerId, CancellationToken ct = default) => inner.ReleaseLeaseAsync(instanceId, ownerId, ct);

        public Task<WorkflowInstanceStatus?> GetStatusAsync(string instanceId, CancellationToken ct = default) => inner.GetStatusAsync(instanceId, ct);

        public Task<IReadOnlyList<WorkflowInstanceStatus>> ListStatusAsync(int? top = null, CancellationToken ct = default) => inner.ListStatusAsync(top, ct);

        public Task<bool> TryCancelAsync(string instanceId, int expectedVersion, string? reason = null, CancellationToken ct = default) => inner.TryCancelAsync(instanceId, expectedVersion, reason, ct);

        public Task HeartbeatAsync(string instanceId, DateTimeOffset? now = null, CancellationToken ct = default) => inner.HeartbeatAsync(instanceId, now, ct);

        public Task TouchActivityAsync(string instanceId, DateTimeOffset? when = null, CancellationToken ct = default) => inner.TouchActivityAsync(instanceId, when, ct);
    }}
