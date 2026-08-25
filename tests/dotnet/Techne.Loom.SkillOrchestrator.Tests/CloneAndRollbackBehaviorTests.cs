using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class CloneAndRollbackBehaviorTests
{
    [Fact]
    public void WorkflowInstanceCloner_DeepClonesTypedMultiDimensionalArrays()
    {
        var nested = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["state"] = "original",
        };
        var source = new WorkflowInstance
        {
            InstanceId = "matrix-clone",
            Context = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["matrix"] = new object[,]
                {
                    { nested, "value" },
                },
            },
        };

        var clone = WorkflowInstanceCloner.Clone(source);
        var sourceMatrix = Assert.IsType<object[,]>(source.Context["matrix"]);
        var clonedMatrix = Assert.IsType<object[,]>(clone.Context["matrix"]);
        Assert.NotSame(sourceMatrix, clonedMatrix);
        Assert.NotSame(sourceMatrix[0, 0], clonedMatrix[0, 0]);

        var clonedNested = Assert.IsAssignableFrom<IDictionary<string, object?>>(clonedMatrix[0, 0]);
        clonedNested["state"] = "changed";
        Assert.Equal("original", Assert.IsAssignableFrom<IDictionary<string, object?>>(sourceMatrix[0, 0])["state"]);
    }

    [Fact]
    public void CommandInvocationClone_DeepClonesArrayParameters()
    {
        var source = new CommandInvocation
        {
            Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["matrix"] = new object[,]
                {
                    { new Dictionary<string, object?>(StringComparer.Ordinal) { ["state"] = "original" } },
                },
            },
        };

        var clone = Assert.IsType<CommandInvocation>(source.Clone());
        var sourceMatrix = Assert.IsType<object[,]>(source.Parameters!["matrix"]);
        var clonedMatrix = Assert.IsType<object[,]>(clone.Parameters!["matrix"]);
        Assert.NotSame(sourceMatrix, clonedMatrix);
        var clonedNested = Assert.IsAssignableFrom<IDictionary<string, object?>>(clonedMatrix[0, 0]);
        clonedNested["state"] = "changed";
        Assert.Equal("original", Assert.IsAssignableFrom<IDictionary<string, object?>>(sourceMatrix[0, 0])["state"]);
    }

    [Fact]
    public async Task ResumeAsync_GateFailureRestoresAllWaitGroupAggregateState()
    {
        var transition = new CommandTransition
        {
            Id = "transition.review",
            Name = "Review result",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.WaitResume,
            OutputPath = "review_round",
            SucceedExpression = "true",
            SatisfiesGateIds = ["gate.review"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["resumeOutputKey"] = "result",
                    ["requiredInputs"] = new List<object?> { "result" },
                },
            },
        };
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Test",
            Groups = [],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "Done",
            Groups = [],
        };
        var waitGroup = new PendingWaitGroup
        {
            InstanceId = "rollback-all",
            TransitionId = transition.Id,
            TargetStateId = done.Id,
            OriginStrategy = ConcurrencyStrategy.All,
        };
        var completedEntry = waitGroup.AddEntry(null);
        var pendingEntry = waitGroup.AddEntry(null);
        Assert.True(waitGroup.TryCompleteEntry(
            completedEntry.WaitId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["prior_result"] = "keep-me",
            }));

        var instance = new WorkflowInstance
        {
            InstanceId = "rollback-all",
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
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
            ActiveWaitGroups = [waitGroup],
            Validation = new WorkflowValidationContract
            {
                Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
                {
                    ["gate.review"] = new WorkflowValidationGate
                    {
                        RequiredOutputFamilies = ["missing_family"],
                        ValueSemantics = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["missing_family"] = "nonEmptyObject",
                        },
                    },
                },
            },
        };
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        await service.ResumeAsync(
            instance.InstanceId,
            transition.Id,
            payload: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["secret"] = "top-secret",
                },
            });

        var saved = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(saved);
        Assert.Equal(WorkflowStatus.WaitingExternal, saved!.Status);
        var restoredGroup = Assert.Single(saved.ActiveWaitGroups);
        Assert.True(restoredGroup.Entries[0].Completed);
        Assert.False(restoredGroup.Entries[1].Completed);
        Assert.Equal("keep-me", restoredGroup.AggregatedContext["prior_result"]);
        Assert.False(restoredGroup.AggregatedContext.ContainsKey("result"));
        Assert.Null(PathValueAccessor.GetValue(saved.Context, "review_round"));
        Assert.NotNull(saved.LastGateEvaluation);
        Assert.Equal("missing_output_family", saved.LastGateEvaluation!.FailedCheck);
        Assert.DoesNotContain("top-secret", saved.History[^1].ContextChanges?.Values.Select(Convert.ToString) ?? []);
        Assert.Contains("received_payload_top_level_keys", saved.History[^1].ContextChanges!.Keys);
    }
}
