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
                resultId: "plan-result-approved");
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

        var lockFile = workflowFile + ".lock";
        if (File.Exists(lockFile))
        {
            File.Delete(lockFile);
        }
    }
}