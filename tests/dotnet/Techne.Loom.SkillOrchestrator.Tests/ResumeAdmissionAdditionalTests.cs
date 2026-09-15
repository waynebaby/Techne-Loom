using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class ResumeAdmissionAdditionalTests
{
    [Fact]
    public async Task ResumeAsync_RejectsAmbiguousDuplicateWaitGroupsWithNullCorrelation()
    {
        var instance = new WorkflowInstance
        {
            InstanceId = "resume-ambiguous-null-correlation",
            Status = WorkflowStatus.WaitingExternal,
            ActiveWaitGroups =
            [
                CreateWaitGroup("resume-ambiguous-null-correlation"),
                CreateWaitGroup("resume-ambiguous-null-correlation"),
            ],
        };
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(
            instance.InstanceId,
            "transition.external"));

        Assert.Contains("Multiple active wait groups", error.Message, StringComparison.Ordinal);
        Assert.Contains("<null>", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResumeAsync_McpOperationIdMustExistInPayloadAndMatchNestedEvidence()
    {
        var missingRoot = await CreateMcpWaitAsync();
        var missingRootError = await Assert.ThrowsAsync<InvalidOperationException>(() => missingRoot.Service.ResumeAsync(
            missingRoot.Instance.InstanceId,
            "transition.mcp",
            payload: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mcp_startup_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["operation_id"] = "operation-1",
                },
            }));
        Assert.Contains("operation_id", missingRootError.Message, StringComparison.Ordinal);

        var missingNested = await CreateMcpWaitAsync();
        var missingNestedError = await Assert.ThrowsAsync<InvalidOperationException>(() => missingNested.Service.ResumeAsync(
            missingNested.Instance.InstanceId,
            "transition.mcp",
            payload: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["operation_id"] = "operation-1",
                ["mcp_startup_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            }));
        Assert.Contains("mcp_startup_evidence.operation_id", missingNestedError.Message, StringComparison.Ordinal);

        var mismatched = await CreateMcpWaitAsync();
        var mismatchError = await Assert.ThrowsAsync<InvalidOperationException>(() => mismatched.Service.ResumeAsync(
            mismatched.Instance.InstanceId,
            "transition.mcp",
            payload: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["operation_id"] = "operation-1",
                ["mcp_startup_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["operation_id"] = "operation-2",
                },
            }));
        Assert.Contains("aligned", mismatchError.Message, StringComparison.Ordinal);

        var empty = await CreateMcpWaitAsync();
        var emptyError = await Assert.ThrowsAsync<InvalidOperationException>(() => empty.Service.ResumeAsync(
            empty.Instance.InstanceId,
            "transition.mcp",
            payload: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["operation_id"] = "",
                ["mcp_startup_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["operation_id"] = "",
                },
            }));
        Assert.Contains("valid operation IDs", emptyError.Message, StringComparison.Ordinal);

        var matching = await CreateMcpWaitAsync();
        var resumed = await matching.Service.ResumeAsync(
            matching.Instance.InstanceId,
            "transition.mcp",
            payload: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["operation_id"] = "operation-1",
                ["mcp_startup_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["operation_id"] = "operation-1",
                },
            });

        Assert.Equal(WorkflowStatus.Running, resumed.Status);
        var terminal = await matching.Service.StartOrAdvanceAsync(matching.Instance.InstanceId);
        Assert.Equal(WorkflowStatus.Succeeded, terminal.StatusProjection.Status);
    }

    [Fact]
    public async Task ResumeAsync_RejectsInvalidOperationIds()
    {
        foreach (var invalidOperationId in new[] { " ", "operation/id", new string('x', 129) })
        {
            var invalid = await CreateMcpWaitAsync();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => invalid.Service.ResumeAsync(
                invalid.Instance.InstanceId,
                "transition.mcp",
                payload: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["operation_id"] = invalidOperationId,
                    ["mcp_startup_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["operation_id"] = invalidOperationId,
                    },
                }));
            Assert.Contains("valid operation IDs", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ResumeAsync_CaptureGuideAcceptsNewValidOperationIdWithoutHistoricalMcpMatch()
    {
        var guide = await CreateGuideWaitAsync();
        var resumed = await guide.Service.ResumeAsync(
            guide.Instance.InstanceId,
            "transition.capture_guide",
            payload: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["operation_id"] = "guide-operation-2",
                ["resolved_guide_surface_ref"] = "guide://so/en/latest",
                ["resolved_guide_surface"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["version"] = "0.3.283-beta",
                },
            });

        Assert.Equal(WorkflowStatus.Running, resumed.Status);
        var terminal = await guide.Service.StartOrAdvanceAsync(guide.Instance.InstanceId);
        Assert.Equal(WorkflowStatus.Succeeded, terminal.StatusProjection.Status);
    }

    private static async Task<(WorkflowInstance Instance, DefaultWorkflowTaskTrackingService Service)> CreateGuideWaitAsync()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups = [new TransitionGroup { Id = "group.capture", TransitionIds = ["transition.capture_guide"] }],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
        };
        var capture = new CommandTransition
        {
            Id = "transition.capture_guide",
            Name = "Capture guide",
            TargetNodeId = done.Id,
            OutputPath = "resolved_guide_surface",
            StepKind = WorkflowStepKind.WaitResume,
            GuardExpression = "true",
            SucceedExpression = "context.Has(\"resolved_guide_surface\")",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "workflow.captureGuideSurface",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["resumeOutputKey"] = "resolved_guide_surface",
                    ["requiredInputs"] = new object?[] { "operation_id", "resolved_guide_surface_ref", "resolved_guide_surface" },
                    ["mustMatchPayloadInputs"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["operation_id"] = "operation_id",
                    },
                },
            },
        };
        var instance = new WorkflowInstance
        {
            InstanceId = $"resume-guide-operation-id-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [capture.Id] = capture,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mcp_startup_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["operation_id"] = "startup-operation-1",
                },
            },
        };
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));
        var first = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.True(first.Suspended);
        return (instance, service);
    }

    private static async Task<(WorkflowInstance Instance, DefaultWorkflowTaskTrackingService Service)> CreateMcpWaitAsync()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups = [new TransitionGroup { Id = "group.mcp", TransitionIds = ["transition.mcp"] }],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
        };
        var mcp = new CommandTransition
        {
            Id = "transition.mcp",
            Name = "MCP startup",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.McpCall,
            OutputPath = "mcp_startup_evidence",
            GuardExpression = "true",
            SucceedExpression = "context.Has(\"mcp_startup_evidence\")",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "so_inspect_workflow_fragment",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["resumeOutputKey"] = "mcp_startup_evidence",
                    ["requiredInputs"] = new object?[] { "operation_id", "mcp_startup_evidence", "mcp_startup_evidence.operation_id" },
                    ["mustMatchPayloadInputs"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["operation_id"] = "mcp_startup_evidence.operation_id",
                    },
                },
            },
        };
        var instance = new WorkflowInstance
        {
            InstanceId = $"resume-mcp-operation-id-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [mcp.Id] = mcp,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));
        var first = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.True(first.Suspended);
        return (instance, service);
    }

    private static PendingWaitGroup CreateWaitGroup(string instanceId)
    {
        var group = new PendingWaitGroup
        {
            InstanceId = instanceId,
            TransitionId = "transition.external",
        };
        group.AddEntry(null);
        return group;
    }
}
