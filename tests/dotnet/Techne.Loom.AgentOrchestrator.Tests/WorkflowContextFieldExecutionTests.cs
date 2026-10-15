using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class WorkflowContextFieldExecutionTests
{
    [Fact]
    public async Task RunAsync_ContextDeltaTypeMismatch_FailsBeforeWorkflowFileChanges()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-context-run-{Guid.NewGuid():N}.json");
        var instance = CreateWorkflow();
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(instance));
        var before = await File.ReadAllBytesAsync(workflowFile);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WorkflowFileExecutionService().RunAsync(
                workflowFile,
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["topic"] = 42 }));

        Assert.Contains("Field 'topic' context value", exception.Message, StringComparison.Ordinal);
        Assert.Contains("does not match JSON type 'string'", exception.Message, StringComparison.Ordinal);
        Assert.Equal(before, await File.ReadAllBytesAsync(workflowFile));
        File.Delete(workflowFile);
    }

    [Fact]
    public async Task RunAsync_ExpressionReadTypeMismatch_FailsBeforeWorkflowFileChanges()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-expression-type-{Guid.NewGuid():N}.json");
        var instance = CreateWorkflow();
        var start = new StateNode
        {
            Id = "state.start",
            Groups = [new TransitionGroup { Id = "group.run", TransitionIds = ["transition.read"] }],
        };
        var end = new StateNode { Id = "state.end", Groups = [] };
        var transition = new ExpressionTransition
        {
            Id = "transition.read",
            TargetNodeId = end.Id,
            GuardExpression = new ExpressionDefinition { Kind = "predicate", Source = "true", ResultType = "bool" },
            SucceedExpression = new ExpressionDefinition { Kind = "predicate", Source = "context.Get<bool>(\"topic\")", ResultType = "bool" },
        };
        instance.StartNodeId = start.Id;
        instance.CurrentNodeId = start.Id;
        instance.EndNodeId = end.Id;
        instance.Status = WorkflowStatus.ReadyToStart;
        instance.Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
        {
            [start.Id] = start,
            [end.Id] = end,
            [transition.Id] = transition,
        };
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(instance));
        var before = await File.ReadAllBytesAsync(workflowFile);
        var reloaded = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile));
        var catalog = (await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(reloaded)).Catalog;
        var preflightException = Assert.Throws<InvalidOperationException>(() =>
            ContextFieldRuntimeValidator.ValidateWorkflowDefinition(reloaded, catalog, workflowFile));
        Assert.Contains("Context path 'topic' is declared as JSON 'string'", preflightException.Message, StringComparison.Ordinal);

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new WorkflowFileExecutionService().RunAsync(workflowFile));

            Assert.Contains("Context path 'topic' is declared as JSON 'string'", exception.Message, StringComparison.Ordinal);
            Assert.Equal(before, await File.ReadAllBytesAsync(workflowFile));
            Assert.False(File.Exists(workflowFile + ".events.jsonl"));
        }
        finally
        {
            File.Delete(workflowFile);
            File.Delete(workflowFile + ".events.jsonl");
            File.Delete(workflowFile + ".lock");
        }
    }

    [Fact]
    public async Task ResumeAsync_ExpressionReadTypeMismatch_FailsBeforeWorkflowFileChanges()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-resume-expression-type-{Guid.NewGuid():N}.json");
        var instance = CreateWorkflow();
        var start = new StateNode { Id = "state.start" };
        var end = new StateNode { Id = "state.end", Groups = [] };
        var transition = new CommandTransition
        {
            Id = "transition.wait",
            OutputPath = "topic",
            StepKind = WorkflowStepKind.WaitResume,
            GuardExpression = new ExpressionDefinition { Kind = "predicate", Source = "context.Get<bool>(\"topic\") == true", ResultType = "bool" },
            SucceedExpression = new ExpressionDefinition { Kind = "predicate", Source = "true", ResultType = "bool" },
            Command = new CommandInvocation
            {
                Name = "review.wait",
                Kind = CommandInvocationKind.Tool,
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { ["resumeOutputKey"] = "result" },
            },
        };
        instance.StartNodeId = start.Id;
        instance.CurrentNodeId = start.Id;
        instance.EndNodeId = end.Id;
        instance.Status = WorkflowStatus.WaitingExternal;
        instance.Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
        {
            [start.Id] = start,
            [end.Id] = end,
            [transition.Id] = transition,
        };
        instance.ActiveWaitGroups.Add(new PendingWaitGroup
        {
            InstanceId = instance.InstanceId,
            TransitionId = transition.Id,
            TargetStateId = end.Id,
            Entries = [new PendingWaitEntry { WaitId = "pending-resume" }],
        });
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(instance));
        var before = await File.ReadAllBytesAsync(workflowFile);

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new WorkflowFileExecutionService().ResumeAsync(
                    workflowFile,
                    transition.Id,
                    correlationKey: null,
                    payload: new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = "approved" }));

            Assert.Contains("Context path 'topic' is declared as JSON 'string'", exception.Message, StringComparison.Ordinal);
            Assert.Equal(before, await File.ReadAllBytesAsync(workflowFile));
            Assert.False(File.Exists(workflowFile + ".events.jsonl"));
        }
        finally
        {
            File.Delete(workflowFile);
            File.Delete(workflowFile + ".events.jsonl");
            File.Delete(workflowFile + ".lock");
        }
    }

    [Fact]
    public async Task ResumeAsync_OutputTypeMismatch_FailsBeforeWorkflowFileChanges()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-context-resume-{Guid.NewGuid():N}.json");
        var instance = CreateWorkflow();
        instance.Nodes["transition.external"] = new CommandTransition
        {
            Id = "transition.external",
            TargetNodeId = "state.end",
            OutputPath = "topic",
            StepKind = WorkflowStepKind.WaitResume,
            SucceedExpression = new ExpressionDefinition { Kind = "predicate", Source = "true", ResultType = "bool" },
            Command = new CommandInvocation
            {
                Name = "review.wait",
                Kind = CommandInvocationKind.NativeCode,
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { ["resumeOutputKey"] = "result" },
            },
        };
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(instance));
        var before = await File.ReadAllBytesAsync(workflowFile);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WorkflowFileExecutionService().ResumeAsync(
                workflowFile,
                "transition.external",
                correlationKey: null,
                payload: new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = 42 }));

        Assert.Contains("Field 'topic' context value", exception.Message, StringComparison.Ordinal);
        Assert.Contains("does not match JSON type 'string'", exception.Message, StringComparison.Ordinal);
        Assert.Equal(before, await File.ReadAllBytesAsync(workflowFile));
        File.Delete(workflowFile);
    }

    [Fact]
    public async Task RunAsync_BlockedResultIncludesContextFieldMetadata()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-context-weaveout-{Guid.NewGuid():N}.json");
        var instance = CreateWorkflow();
        var start = new StateNode
        {
            Id = "state.start",
            Groups = [new TransitionGroup { Id = "group.wait", TransitionIds = ["transition.wait"] }],
        };
        var next = new StateNode
        {
            Id = "state.next",
            Groups = [new TransitionGroup { Id = "group.consume", TransitionIds = ["transition.consume"] }],
        };
        var consume = new ExpressionTransition
        {
            Id = "transition.consume",
            GuardExpression = "true",
            SucceedExpression = "context.Get<string>(\"topic\") != null",
        };
        var wait = new CommandTransition
        {
            Id = "transition.wait",
            TargetNodeId = next.Id,
            OutputPath = "topic",
            StepKind = WorkflowStepKind.WaitResume,
            GuardExpression = new ExpressionDefinition { Kind = "predicate", Source = "true", ResultType = "bool" },
            SucceedExpression = new ExpressionDefinition { Kind = "predicate", Source = "true", ResultType = "bool" },
            Command = new CommandInvocation
            {
                Name = "review.wait",
                Kind = CommandInvocationKind.NativeCode,
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new object?[] { "topic" },
                    ["resumeOutputKey"] = "result",
                    ["projectionMode"] = "canonical",
                },
            },
        };
        instance.StartNodeId = start.Id;
        instance.CurrentNodeId = start.Id;
        instance.EndNodeId = next.Id;
        instance.Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
        {
            [start.Id] = start,
            [next.Id] = next,
            [wait.Id] = wait,
            [consume.Id] = consume,
        };
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(instance));

        try
        {
            var result = await new WorkflowFileExecutionService().RunAsync(workflowFile);

            Assert.Equal(WorkflowStatus.WaitingExternal, result.Status.Status);
            var field = Assert.Single(result.ContextFields!);
            Assert.Equal("topic", field.Path);
            Assert.Equal("string", field.Type);
            Assert.Contains(field.ConsumerExpressions, expression => expression.Location == "transition:transition.consume/succeedExpression" && expression.ReadAs == "string");
        }
        finally
        {
            File.Delete(workflowFile);
            File.Delete(workflowFile + ".events.jsonl");
        }
    }

    [Fact]
    public async Task ResumeAsync_UndeclaredPayloadFieldsAreFilteredAndReported()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-context-filter-{Guid.NewGuid():N}.json");
        var instance = CreateWorkflow();
        var start = new StateNode
        {
            Id = "state.start",
            Groups = [new TransitionGroup { Id = "group.wait", TransitionIds = ["transition.wait"] }],
        };
        var done = new StateNode { Id = "state.done", Groups = [] };
        var wait = new CommandTransition
        {
            Id = "transition.wait",
            TargetNodeId = done.Id,
            OutputPath = "topic",
            StepKind = WorkflowStepKind.WaitResume,
            GuardExpression = new ExpressionDefinition { Kind = "predicate", Source = "true", ResultType = "bool" },
            SucceedExpression = new ExpressionDefinition { Kind = "predicate", Source = "true", ResultType = "bool" },
            Command = new CommandInvocation
            {
                Name = "review.wait",
                Kind = CommandInvocationKind.NativeCode,
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new object?[] { "topic" },
                    ["resumeOutputKey"] = "result",
                    ["projectionMode"] = "canonical",
                },
            },
        };
        instance.StartNodeId = start.Id;
        instance.CurrentNodeId = start.Id;
        instance.EndNodeId = done.Id;
        instance.Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
        {
            [start.Id] = start,
            [done.Id] = done,
            [wait.Id] = wait,
        };

        try
        {
            await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(instance));
            var service = new WorkflowFileExecutionService();
            var boundary = await service.RunAsync(workflowFile);
            Assert.Equal(WorkflowStatus.WaitingExternal, boundary.Status.Status);

            var result = await service.ResumeAsync(
                workflowFile,
                wait.Id,
                correlationKey: null,
                payload: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["result"] = "approved",
                    ["unexpected"] = "must be filtered",
                });

            Assert.Equal(WorkflowStatus.Succeeded, result.Status.Status);
            Assert.Contains("Filtered undeclared resume payload field 'unexpected'.", result.ContextFieldWarnings!);
            var persisted = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile));
            Assert.Equal("approved", PathValueAccessor.GetValue(persisted.Context, "topic"));
            Assert.False(persisted.Context.ContainsKey("unexpected"));
        }
        finally
        {
            File.Delete(workflowFile);
            File.Delete(workflowFile + ".events.jsonl");
        }
    }

    private static WorkflowInstance CreateWorkflow()
    {
        var repositoryRoot = FindRepositoryRoot();
        return new WorkflowInstance
        {
            InstanceId = "context-field-execution-test",
            ContractBinding = new ContractBinding
            {
                Path = "assets/so-workflow/contract.json",
                Format = "json",
                AssetRootPath = Path.Combine(
                    repositoryRoot,
                    "tests",
                    "dotnet",
                    "Techne.Loom.AgentOrchestrator.Tests",
                    "Fixtures",
                    "PromptReplanSkill"),
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal) { ["topic"] = "valid" },
        };
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Techne.Loom.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
