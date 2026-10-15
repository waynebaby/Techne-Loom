using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class ContextFieldWeaveOutMetadataTests
{
    [Fact]
    public void Build_IncludesTypedSuggestionsConsumersAndUserDecisionGuidance()
    {
        using var contractDocument = JsonDocument.Parse(
            """
            {
              "name": "weave-out-test",
              "inputs": {},
              "outputs": {},
              "default_assumptions": {},
              "context_fields": {
                "schemaVersion": "loom.context-fields.v1",
                "fields": [
                  {
                    "path": "review.approved",
                    "type": "boolean",
                    "nullable": false,
                    "owner": "user",
                    "description": "Whether the review is approved.",
                    "suggestion_reason": "Only the requester knows the approval decision.",
                    "allowed_values": [true, false],
                    "suggested_values": [
                      {"value": true, "label": "Approve", "source": "Explicit user choice", "kind": "choice"},
                      {"value": false, "label": "Reject", "source": "Explicit user choice", "kind": "choice"}
                    ]
                  }
                ]
              }
            }
            """);
        var catalog = ContextFieldCatalog.Parse(contractDocument.RootElement, "weave-out-test/contract.json");
        var consumer = new ExpressionTransition
        {
            Id = "transition.consume",
            GuardExpression = "context.Get<bool>(\"review.approved\")",
            SucceedExpression = "true",
        };
        var target = new StateNode
        {
            Id = "state.next",
            Groups = [new TransitionGroup { Id = "group.next", TransitionIds = [consumer.Id] }],
        };
        var pending = new CommandTransition
        {
            Id = "transition.wait",
            TargetNodeId = target.Id,
            OutputPath = "review.approved",
            StepKind = WorkflowStepKind.AskUser,
            Command = new CommandInvocation
            {
                Name = "ask_user",
                Kind = CommandInvocationKind.NativeCode,
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new object?[] { "review.approved" },
                },
            },
        };
        var parallelPending = new CommandTransition
        {
            Id = "transition.parallel_wait",
            TargetNodeId = target.Id,
            StepKind = WorkflowStepKind.WaitResume,
            SucceedExpression = new ExpressionDefinition
            {
                Kind = "predicate",
                Source = "context.Get<bool>(\"review.approved\")",
                ResultType = "bool",
            },
            Command = new CommandInvocation
            {
                Name = "parallel.review",
                Kind = CommandInvocationKind.NativeCode,
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new object?[] { "review.approved" },
                },
            },
        };
        var instance = new WorkflowInstance
        {
            ActiveWaitGroups =
            [
                new PendingWaitGroup
                {
                    InstanceId = "weave-out-test",
                    TransitionId = pending.Id,
                    ExpectedTransitionIds = [pending.Id, parallelPending.Id],
                },
            ],
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [target.Id] = target,
                [pending.Id] = pending,
                [parallelPending.Id] = parallelPending,
                [consumer.Id] = consumer,
            },
        };

        var fields = ContextFieldWeaveOutMetadataBuilder.Build(instance, catalog, pending);

        var field = Assert.Single(fields);
        Assert.Equal("review.approved", field.Path);
        Assert.Equal("boolean", field.Type);
        Assert.Equal("Only the requester knows the approval decision.", field.SuggestionReason);
        Assert.Equal(2, field.AllowedValues.Count);
        Assert.Equal(2, field.SuggestedValues.Count);
        Assert.Equal(2, field.ConsumerExpressions.Count);
        var expression = Assert.Single(field.ConsumerExpressions, item => item.Location == "transition:transition.consume/guardExpression");
        Assert.Equal("transition:transition.consume/guardExpression", expression.Location);
        Assert.Equal("bool", expression.ReadAs);
        Assert.Contains(field.ConsumerExpressions, item => item.Location == "transition:transition.parallel_wait/succeedExpression" && item.ReadAs == "bool");
        Assert.Contains("user", field.HostDecisionGuidance, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void Build_IncludesOutputBindingAndGateExpressionConsumers()
    {
        using var contractDocument = JsonDocument.Parse(
            """
            {
              "name": "weave-out-consumers-test",
              "inputs": {},
              "outputs": {},
              "default_assumptions": {},
              "context_fields": {
                "schemaVersion": "loom.context-fields.v1",
                "fields": [
                  {"path":"request.value","type":"string","nullable":true,"owner":"agent","description":"Source value."},
                  {"path":"request.copy","type":"string","nullable":true,"owner":"agent","description":"Projected value."},
                  {"path":"request.gate","type":"string","nullable":true,"owner":"agent","description":"Gate input."}
                ]
              }
            }
            """);
        var catalog = ContextFieldCatalog.Parse(contractDocument.RootElement, "weave-out-consumers/contract.json");
        var consumer = new ExpressionTransition
        {
            Id = "transition.consume",
            GuardExpression = "true",
            SucceedExpression = "context.Get<string>(\"request.value\") != null",
        };
        var projection = new CommandTransition
        {
            Id = "transition.project",
            StepKind = WorkflowStepKind.ToolCall,
            SatisfiesGateIds = ["gate.review"],
            GuardExpression = new ExpressionDefinition { Kind = "predicate", Source = "true", ResultType = "bool" },
            SucceedExpression = new ExpressionDefinition { Kind = "predicate", Source = "true", ResultType = "bool" },
            Command = new CommandInvocation
            {
                Name = "project.review",
                Kind = CommandInvocationKind.NativeCode,
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["request.copy"] = "$context:request.value",
                    },
                },
            },
        };
        var next = new StateNode
        {
            Id = "state.next",
            Groups = [new TransitionGroup { Id = "group.next", TransitionIds = [consumer.Id, projection.Id] }],
        };
        var pending = new CommandTransition
        {
            Id = "transition.wait",
            TargetNodeId = next.Id,
            OutputPath = "request.value",
            StepKind = WorkflowStepKind.WaitResume,
            Command = new CommandInvocation
            {
                Name = "wait.review",
                Kind = CommandInvocationKind.NativeCode,
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new object?[] { "request.value" },
                },
            },
        };
        var instance = new WorkflowInstance
        {
            Validation = new WorkflowValidationContract
            {
                Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
                {
                    ["gate.review"] = new()
                    {
                        PassExpression = new ExpressionDefinition
                        {
                            Kind = "predicate",
                            Source = "context.Get<string>(\"request.gate\") != null",
                            ResultType = "bool",
                        },
                    },
                },
            },
            ActiveWaitGroups =
            [
                new PendingWaitGroup
                {
                    TransitionId = pending.Id,
                    ExpectedTransitionIds = [pending.Id, projection.Id],
                },
            ],
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [next.Id] = next,
                [consumer.Id] = consumer,
                [projection.Id] = projection,
                [pending.Id] = pending,
            },
        };

        var fields = ContextFieldWeaveOutMetadataBuilder.Build(instance, catalog, pending);
        var sourceField = Assert.Single(fields, field => field.Path == "request.value");
        Assert.Contains(sourceField.ConsumerExpressions, expression =>
            expression.Location == "transition:transition.project/outputBindings/request.copy"
            && expression.Source == "$context:request.value"
            && expression.ReadAs == "projection");
        var gateField = Assert.Single(fields, field => field.Path == "request.gate");
        Assert.Contains(gateField.ConsumerExpressions, expression =>
            expression.Location == "validation.gates.gate.review/passExpression"
            && expression.ReadAs == "string");
    }
    [Fact]
    public void Build_MapsDynamicKeyConsumersToTheirDeclaredMapField()
    {
        using var contractDocument = JsonDocument.Parse("""
            {"name":"weave-out-dynamic-map-test","inputs":{},"outputs":{},"default_assumptions":{},"context_fields":{"schemaVersion":"loom.context-fields.v1","fields":[{"path":"request.key","type":"string","nullable":false,"owner":"caller","description":"Map key."},{"path":"request.flags","type":"object","valueType":"boolean","nullable":true,"owner":"caller","description":"Boolean-valued map."}]}}
            """);
        var catalog = ContextFieldCatalog.Parse(contractDocument.RootElement, "weave-out-dynamic-map/contract.json");
        var consumer = new ExpressionTransition
        {
            Id = "transition.consume_map_value",
            GuardExpression = "context.Get<bool>(\"request.flags.\" + context.Get<string>(\"request.key\"))",
            SucceedExpression = "true",
        };
        var target = new StateNode
        {
            Id = "state.next",
            Groups = [new TransitionGroup { Id = "group.next", TransitionIds = [consumer.Id] }],
        };
        var pending = new CommandTransition
        {
            Id = "transition.wait",
            TargetNodeId = target.Id,
            OutputPath = "request.key",
            StepKind = WorkflowStepKind.WaitResume,
            Command = new CommandInvocation
            {
                Name = "wait.map-key",
                Kind = CommandInvocationKind.NativeCode,
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new object?[] { "request.key", "request.flags" },
                },
            },
        };
        var instance = new WorkflowInstance
        {
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [target.Id] = target,
                [consumer.Id] = consumer,
                [pending.Id] = pending,
            },
        };

        var fields = ContextFieldWeaveOutMetadataBuilder.Build(instance, catalog, pending);
        var map = Assert.Single(fields, field => field.Path == "request.flags");

        Assert.Equal("object", map.Type);
        Assert.Equal("boolean", map.ValueType);
        Assert.Contains(map.ConsumerExpressions, expression =>
            expression.Location == "transition:transition.consume_map_value/guardExpression"
            && expression.ReadAs == "bool");
    }
    [Fact]
    public void Build_IncludesNumericBoundsForRequiredFields()
    {
        using var contractDocument = JsonDocument.Parse("""
            {"name":"weave-out-numeric-bounds-test","inputs":{},"outputs":{},"default_assumptions":{},"context_fields":{"schemaVersion":"loom.context-fields.v1","fields":[{"path":"metrics.findings_count","type":"integer","nullable":false,"owner":"agent","description":"Finding count.","minimum":0,"maximum":12}]}}
            """);
        var catalog = ContextFieldCatalog.Parse(contractDocument.RootElement, "weave-out-numeric-bounds/contract.json");
        var next = new StateNode { Id = "state.next", Groups = [] };
        var pending = new CommandTransition
        {
            Id = "transition.wait",
            TargetNodeId = next.Id,
            OutputPath = "metrics.findings_count",
            StepKind = WorkflowStepKind.WaitResume,
            Command = new CommandInvocation
            {
                Name = "wait.metrics",
                Kind = CommandInvocationKind.NativeCode,
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new object?[] { "metrics.findings_count" },
                },
            },
        };
        var instance = new WorkflowInstance
        {
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [next.Id] = next,
                [pending.Id] = pending,
            },
        };

        var field = Assert.Single(ContextFieldWeaveOutMetadataBuilder.Build(instance, catalog, pending));

        Assert.Equal(0m, field.Minimum);
        Assert.Equal(12m, field.Maximum);
    }
}
