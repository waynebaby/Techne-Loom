using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class ContextFieldRuntimeAdmissionTests : SkillOrchestratorBehaviorTestBase
{
    [Fact]
    public async Task CliRun_UndeclaredContextDeltaRoot_FailsBeforeWorkflowOrEventMutation()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-context-unknown-{Guid.NewGuid():N}.json");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-context-unknown-{Guid.NewGuid():N}.context.json");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateContextWorkflow()));
        await File.WriteAllTextAsync(contextFile, JsonSerializer.Serialize(new { unlisted = "must be rejected" }));
        var before = await File.ReadAllBytesAsync(workflowFile);

        try
        {
            var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowFile}\" --context-file \"{contextFile}\"");

            Assert.Equal(2, run.ExitCode);
            using var errorEnvelope = ReadFinalSoEnvelope(run.StdOut);
            var message = errorEnvelope.RootElement.GetProperty("payload").GetProperty("message").GetString() ?? string.Empty;
            Assert.Contains("unlisted", message, StringComparison.Ordinal);
            Assert.Contains("not declared", message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before, await File.ReadAllBytesAsync(workflowFile));
            Assert.False(File.Exists(workflowFile + ".events.jsonl"));
        }
        finally
        {
            File.Delete(workflowFile);
            File.Delete(contextFile);
            File.Delete(workflowFile + ".events.jsonl");
            File.Delete(workflowFile + ".lock");
        }
    }

    [Fact]
    public async Task CliRun_ContextDeltaTypeMismatch_FailsBeforeWorkflowOrEventMutation()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-context-run-{Guid.NewGuid():N}.json");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-context-run-{Guid.NewGuid():N}.context.json");
        var instance = CreateContextWorkflow();
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(instance));
        await File.WriteAllTextAsync(contextFile, JsonSerializer.Serialize(new { review = new { approved = "not-a-boolean" } }));
        var before = await File.ReadAllBytesAsync(workflowFile);

        try
        {
            var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowFile}\" --context-file \"{contextFile}\"");

            Assert.Equal(2, run.ExitCode);
            using var errorEnvelope = ReadFinalSoEnvelope(run.StdOut);
            var message = errorEnvelope.RootElement.GetProperty("payload").GetProperty("message").GetString() ?? string.Empty;
            Assert.Contains("review.approved", message, StringComparison.Ordinal);
            Assert.Contains("JSON type 'boolean'", message, StringComparison.Ordinal);
            Assert.Equal(before, await File.ReadAllBytesAsync(workflowFile));
            Assert.False(File.Exists(workflowFile + ".events.jsonl"));
        }
        finally
        {
            File.Delete(workflowFile);
            File.Delete(contextFile);
            File.Delete(workflowFile + ".events.jsonl");
        }
    }

    [Fact]
    public async Task CliRun_UndeclaredNestedContextMember_FailsBeforeWorkflowOrEventMutation()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-context-nested-{Guid.NewGuid():N}.json");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-context-nested-{Guid.NewGuid():N}.context.json");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateContextWorkflow()));
        await File.WriteAllTextAsync(contextFile, JsonSerializer.Serialize(new
        {
            review = new { approved = true, unexpected = "undeclared" },
        }));
        var before = await File.ReadAllBytesAsync(workflowFile);

        try
        {
            var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowFile}\" --context-file \"{contextFile}\"");

            Assert.Equal(2, run.ExitCode);
            using var errorEnvelope = ReadFinalSoEnvelope(run.StdOut);
            var message = errorEnvelope.RootElement.GetProperty("payload").GetProperty("message").GetString() ?? string.Empty;
            Assert.Contains("review.unexpected", message, StringComparison.Ordinal);
            Assert.Contains("not declared", message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before, await File.ReadAllBytesAsync(workflowFile));
            Assert.False(File.Exists(workflowFile + ".events.jsonl"));
        }
        finally
        {
            File.Delete(workflowFile);
            File.Delete(contextFile);
            File.Delete(workflowFile + ".events.jsonl");
        }
    }

    [Fact]
    public async Task CliResume_NestedPayloadTypeMismatch_FailsBeforeWorkflowMutation()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-context-resume-{Guid.NewGuid():N}.json");
        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-context-resume-{Guid.NewGuid():N}.result.json");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        try
        {
            var firstRun = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowFile}\"");
            Assert.Equal(3, firstRun.ExitCode);
            var beforeResume = await File.ReadAllBytesAsync(workflowFile);
            await File.WriteAllTextAsync(
                resultFile,
                JsonSerializer.Serialize(new
                {
                    transition_id = "transition.ask",
                    correlation_key = (string?)null,
                    payload = new { review = new { approved = "not-a-boolean" } },
                }));

            var resume = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --result-file \"{resultFile}\"");

            Assert.Equal(2, resume.ExitCode);
            using var errorEnvelope = ReadFinalSoEnvelope(resume.StdOut);
            var message = errorEnvelope.RootElement.GetProperty("payload").GetProperty("message").GetString() ?? string.Empty;
            Assert.Contains("review.approved", message, StringComparison.Ordinal);
            Assert.Contains("JSON type 'boolean'", message, StringComparison.Ordinal);
            Assert.Equal(beforeResume, await File.ReadAllBytesAsync(workflowFile));
        }
        finally
        {
            File.Delete(workflowFile);
            File.Delete(resultFile);
            File.Delete(workflowFile + ".events.jsonl");
        }
    }
    [Fact]
    public void ResumePayload_RuntimeOwnedOutputAllowedOnlyForWaitResume()
    {
        using var contract = JsonDocument.Parse("""
            {"context_fields":{"schemaVersion":"loom.context-fields.v1","fields":[{"path":"runtime_fact","type":"string","nullable":true,"owner":"runtime","description":"Runtime fact."}]}}
            """);
        var catalog = ContextFieldCatalog.Parse(contract.RootElement, "contract.json");
        var transitionId = "transition.external";
        var transition = new CommandTransition
        {
            Id = transitionId,
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "runtime_fact",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "test.external",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["resumeOutputKey"] = "result",
                },
            },
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "runtime-owner-resume-test",
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [transitionId] = transition,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["result"] = "external value",
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            ContextFieldRuntimeValidator.ValidateResumeProjection(instance, catalog, transitionId, payload, "workflow.json"));
        Assert.Contains("runtime-owned context field 'runtime_fact'", error.Message, StringComparison.Ordinal);

        var waitTransition = transition with { StepKind = WorkflowStepKind.WaitResume };
        instance.Nodes[transitionId] = waitTransition;
        var admission = ContextFieldRuntimeValidator.ValidateResumeProjection(instance, catalog, transitionId, payload, "workflow.json");
        Assert.Equal("external value", admission.Payload!["result"]);
    }
    [Fact]
    public void ResumePayload_AllowsTrustedContextPassThroughButRejectsExternalRuntimeWrites()
    {
        using var contract = JsonDocument.Parse("""
            {"context_fields":{"schemaVersion":"loom.context-fields.v1","fields":[{"path":"runtime_fact","type":"string","nullable":false,"owner":"runtime","description":"Runtime fact."},{"path":"agent_result","type":"string","nullable":false,"owner":"agent","description":"External result."},{"path":"runtime_container","type":"object","nullable":true,"owner":"runtime","description":"Runtime container."},{"path":"runtime_container.agent_value","type":"string","nullable":false,"owner":"agent","description":"Agent-owned nested result."}]}}
            """);
        var catalog = ContextFieldCatalog.Parse(contract.RootElement, "contract.json");
        var transitionId = "transition.external";
        var transition = new CommandTransition
        {
            Id = transitionId,
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "agent_result",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "test.external",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["resumeOutputKey"] = "result",
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["runtime_fact"] = "$context:runtime_fact",
                        ["runtime_container.agent_value"] = "$result",
                    },
                },
            },
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "runtime-context-pass-through-test",
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal) { [transitionId] = transition },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal) { ["runtime_fact"] = "trusted runtime value" },
        };
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = "external value" };

        var admission = ContextFieldRuntimeValidator.ValidateResumeProjection(instance, catalog, transitionId, payload, "workflow.json");
        Assert.Equal("external value", admission.Payload!["result"]);

        var bindings = (Dictionary<string, object?>)transition.Command.Parameters!["outputBindings"]!;
        bindings["runtime_fact"] = "$context:agent_result";
        var error = Assert.Throws<InvalidOperationException>(() =>
            ContextFieldRuntimeValidator.ValidateResumeProjection(instance, catalog, transitionId, payload, "workflow.json"));
        Assert.Contains("runtime-owned context field 'runtime_fact'", error.Message, StringComparison.Ordinal);
    }
    [Fact]
    public void ContextDelta_DottedRootKeyCannotBypassNestedFieldType()
    {
        using var contract = JsonDocument.Parse("""
            {"context_fields":{"schemaVersion":"loom.context-fields.v1","fields":[{"path":"review.approved","type":"boolean","nullable":false,"owner":"user","description":"Review decision."}]}}
            """);
        var catalog = ContextFieldCatalog.Parse(contract.RootElement, "contract.json");
        var emptyContext = new Dictionary<string, object?>(StringComparer.Ordinal);

        var invalidFlatDelta = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["review.approved"] = "true",
        };
        var error = Assert.Throws<InvalidOperationException>(() =>
            ContextFieldRuntimeValidator.ValidateContextDelta(catalog, emptyContext, invalidFlatDelta, "workflow.json"));
        Assert.Contains("does not match JSON type 'boolean'", error.Message, StringComparison.Ordinal);

        var validFlatDelta = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["review.approved"] = true,
        };
        ContextFieldRuntimeValidator.ValidateContextDelta(catalog, emptyContext, validFlatDelta, "workflow.json");

        var nestedDelta = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["review"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["approved"] = true },
        };
        ContextFieldRuntimeValidator.ValidateContextDelta(catalog, emptyContext, nestedDelta, "workflow.json");

        var duplicateRepresentation = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["review.approved"] = false,
            ["review"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["approved"] = true },
        };
        var duplicateError = Assert.Throws<InvalidOperationException>(() =>
            catalog.ValidateContextValues(duplicateRepresentation, "workflow.json"));
        Assert.Contains("both flat and nested form", duplicateError.Message, StringComparison.Ordinal);
    }
    [Fact]
    public void ResumeProjection_RequiresResultKeyButAcceptsExplicitNull()
    {
        using var contract = JsonDocument.Parse("""
            {"context_fields":{"schemaVersion":"loom.context-fields.v1","fields":[{"path":"result_value","type":"string","nullable":true,"owner":"agent","description":"External result value."}]}}
            """);
        var catalog = ContextFieldCatalog.Parse(contract.RootElement, "contract.json");
        var transitionId = "transition.external";
        var transition = new CommandTransition
        {
            Id = transitionId,
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "result_value",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "test.external",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { ["resumeOutputKey"] = "result" },
            },
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "missing-versus-null-test",
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal) { [transitionId] = transition },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal) { ["result_value"] = "preserve this value" },
        };

        var missingError = Assert.Throws<InvalidOperationException>(() =>
            ContextFieldRuntimeValidator.ValidateResumeProjection(
                instance,
                catalog,
                transitionId,
                new Dictionary<string, object?>(StringComparer.Ordinal),
                "workflow.json"));
        Assert.Contains("missing declared resumeOutputKey 'result'", missingError.Message, StringComparison.Ordinal);
        Assert.Equal("preserve this value", instance.Context["result_value"]);

        var explicitNull = ContextFieldRuntimeValidator.ValidateResumeProjection(
            instance,
            catalog,
            transitionId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = null },
            "workflow.json");
        Assert.Null(explicitNull.Payload!["result"]);
    }

    [Fact]
    public void ResumeProjection_MissingContextSourceIsNotTreatedAsExplicitNull()
    {
        using var contract = JsonDocument.Parse("""
            {"context_fields":{"schemaVersion":"loom.context-fields.v1","fields":[{"path":"result_value","type":"string","nullable":true,"owner":"agent","description":"External result."},{"path":"source_value","type":"string","nullable":true,"owner":"agent","description":"Existing source value."},{"path":"mirror_value","type":"string","nullable":true,"owner":"agent","description":"Projected value."}]}}
            """);
        var catalog = ContextFieldCatalog.Parse(contract.RootElement, "contract.json");
        var transitionId = "transition.external";
        var transition = new CommandTransition
        {
            Id = transitionId,
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "result_value",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "test.external",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["resumeOutputKey"] = "result",
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["mirror_value"] = "$context:source_value" },
                },
            },
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "missing-context-source-test",
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal) { [transitionId] = transition },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = "external value" };

        var missingError = Assert.Throws<InvalidOperationException>(() =>
            ContextFieldRuntimeValidator.ValidateResumeProjection(instance, catalog, transitionId, payload, "workflow.json"));
        Assert.Contains("source_value", missingError.Message, StringComparison.Ordinal);

        instance.Context["source_value"] = null;
        var admission = ContextFieldRuntimeValidator.ValidateResumeProjection(instance, catalog, transitionId, payload, "workflow.json");
        Assert.Equal("external value", admission.Payload!["result"]);
    }
    [Fact]
    public void ResumePayload_RejectsNestedObjectValuesUnderRuntimeOwnedMap()
    {
        using var contract = JsonDocument.Parse("""
            {"context_fields":{"schemaVersion":"loom.context-fields.v1","fields":[{"path":"agent_result","type":"string","nullable":false,"owner":"agent","description":"External result."},{"path":"runtime_map","type":"object","valueType":"object","nullable":false,"owner":"runtime","description":"Runtime-owned object map."}]}}
            """);
        var catalog = ContextFieldCatalog.Parse(contract.RootElement, "contract.json");
        const string transitionId = "transition.external";
        var transition = new CommandTransition
        {
            Id = transitionId,
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "agent_result",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "test.external",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { ["resumeOutputKey"] = "result" },
            },
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "runtime-map-owner-test",
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal) { [transitionId] = transition },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["runtime_map"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["entry-1"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["state"] = "forged" },
            },
            ["result"] = "external result",
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            ContextFieldRuntimeValidator.ValidateResumeProjection(instance, catalog, transitionId, payload, "workflow.json"));

        Assert.Contains("runtime-owned context field 'runtime_map'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResumePayload_AllowsAgentSiblingButRejectsRuntimeSiblingInParentObject()
    {
        using var contract = JsonDocument.Parse("""
            {"context_fields":{"schemaVersion":"loom.context-fields.v1","fields":[{"path":"plan","type":"object","nullable":true,"owner":"agent","description":"Plan result."},{"path":"plan.answer","type":"string","nullable":false,"owner":"agent","description":"Plan answer."},{"path":"plan.consumed_result_ids","type":"array","itemType":"string","nullable":false,"owner":"runtime","description":"Consumed plan result identifiers."}]}}
            """);
        var catalog = ContextFieldCatalog.Parse(contract.RootElement, "contract.json");
        const string transitionId = "transition.plan";
        var transition = new CommandTransition
        {
            Id = transitionId,
            StepKind = WorkflowStepKind.WaitResume,
            OutputPath = "plan.answer",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "test.plan",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { ["resumeOutputKey"] = "plan.answer" },
            },
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "runtime-sibling-plan-test",
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal) { [transitionId] = transition },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
        var agentOnlyPayload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["plan"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["answer"] = "approved" },
        };

        var admission = ContextFieldRuntimeValidator.ValidateResumeProjection(instance, catalog, transitionId, agentOnlyPayload, "workflow.json");
        Assert.Equal("approved", PathValueAccessor.GetValue(admission.Payload!, "plan.answer"));

        var runtimeWritePayload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["plan"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["answer"] = "approved",
                ["consumed_result_ids"] = new[] { "injected-id" },
            },
        };
        var error = Assert.Throws<InvalidOperationException>(() =>
            ContextFieldRuntimeValidator.ValidateResumeProjection(instance, catalog, transitionId, runtimeWritePayload, "workflow.json"));
        Assert.Contains("runtime-owned context field 'plan.consumed_result_ids'", error.Message, StringComparison.Ordinal);
    }
    [Fact]
    public async Task ResumeAsync_ProjectsDeclaredFlatDottedContextValue()
    {
        using var contract = JsonDocument.Parse("""
            {"context_fields":{"schemaVersion":"loom.context-fields.v1","fields":[{"path":"review.approved","type":"boolean","nullable":false,"owner":"user","description":"Review decision."},{"path":"review.copy","type":"boolean","nullable":false,"owner":"agent","description":"Copied review value."},{"path":"result","type":"string","nullable":true,"owner":"agent","description":"External result."}]}}
            """);
        var catalog = ContextFieldCatalog.Parse(contract.RootElement, "contract.json");
        var instance = new WorkflowInstance
        {
            InstanceId = "flat-dotted-projection-test",
            Status = WorkflowStatus.WaitingExternal,
            StartNodeId = "state.start",
            CurrentNodeId = "state.start",
            EndNodeId = "state.done",
            Context = new Dictionary<string, object?>(StringComparer.Ordinal) { ["review.approved"] = true },
        };
        var start = new StateNode { Id = "state.start", Groups = [] };
        var done = new StateNode { Id = "state.done", Groups = [] };
        var transition = new CommandTransition
        {
            Id = "transition.wait",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.WaitResume,
            OutputPath = "result",
            GuardExpression = new ExpressionDefinition { Kind = "predicate", Source = "true", ResultType = "bool" },
            SucceedExpression = new ExpressionDefinition { Kind = "predicate", Source = "true", ResultType = "bool" },
            Command = new CommandInvocation
            {
                Name = "wait.review",
                Kind = CommandInvocationKind.Tool,
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["resumeOutputKey"] = "result",
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["review.copy"] = "$context:review.approved",
                    },
                },
            },
        };
        instance.Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
        {
            [start.Id] = start,
            [done.Id] = done,
            [transition.Id] = transition,
        };
        var waitGroup = new PendingWaitGroup
        {
            InstanceId = instance.InstanceId,
            TransitionId = transition.Id,
            TargetStateId = done.Id,
            OriginStrategy = ConcurrencyStrategy.FirstSuccess,
        };
        waitGroup.AddEntry(null);
        instance.ActiveWaitGroups.Add(waitGroup);
        catalog.ValidateContextValues(instance.Context, "workflow.json");

        await new WorkflowExecutionCore().ResumeAsync(
            instance,
            transition.Id,
            correlationKey: null,
            payload: new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = "complete" });

        Assert.True(PathValueAccessor.GetValue(instance.Context, "review.approved") is true);
        Assert.True(PathValueAccessor.GetValue(instance.Context, "review.copy") is true);
        Assert.Equal("complete", instance.Context["result"]);
        catalog.ValidateContextValues(instance.Context, "workflow.json");
    }
}
