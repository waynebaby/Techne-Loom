using System.Text.Json;
using System.Text.Json.Nodes;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowScriptVerificationTests
{
    [Fact]
    public void Verify_ReportsUnitStyleChecksAndSkipsRuntimeOnlyChecksWithoutHistory()
    {
        var workflow = WorkflowSchemaDemoExporter.CreateDemoWorkflow("dotnet-so");
        var result = WorkflowScriptModelVerifier.Verify(workflow, workflow, CreateModelReference("dotnet-so"));

        Assert.True(result.Passed, string.Join(Environment.NewLine, result.Failures));
        Assert.True(result.TotalChecks >= 20);
        Assert.True(result.PassedChecks > 0);
        Assert.Equal(0, result.FailedChecks);
        Assert.True(result.SkippedChecks > 0);
        Assert.False(result.RuntimeEvidenceObserved);
        Assert.Contains(result.TestCases, item => item.Id == "serializer.round_trip" && item.Passed);
        Assert.Contains(result.TestCases, item => item.Id == "runtime.blocked_route" && item.Skipped);
    }

    [Fact]
    public void Verify_FailsWhenRootFieldOrNodeKindIsMissingFromSerializedCandidate()
    {
        var workflow = WorkflowSchemaDemoExporter.CreateDemoWorkflow("dotnet-so");
        var json = JsonNode.Parse(WorkflowJsonSerializer.Serialize(workflow, indented: false))!.AsObject();
        json.Remove("instanceId");
        json["nodes"]!["transition.tool_call"]!.AsObject().Remove("$kind");

        var result = WorkflowScriptModelVerifier.Verify(
            workflow,
            workflow,
            CreateModelReference("dotnet-so"),
            json.ToJsonString(),
            WorkflowJsonSerializer.Serialize(workflow, indented: false));

        Assert.False(result.Passed);
        Assert.Contains(result.TestCases, item => item.Id == "root.required.instanceId" && !item.Passed);
        Assert.Contains(result.TestCases, item => item.Id == "nodes.transition.tool_call.kind" && !item.Passed);
    }

    [Fact]
    public void Verify_FailsInvalidTargetExpressionProjectionAndProducerClaims()
    {
        var workflow = WorkflowSchemaDemoExporter.CreateDemoWorkflow("dotnet-so");
        var transition = Assert.IsType<CommandTransition>(workflow.Nodes["transition.tool_call"]);
        transition = transition with
        {
            TargetNodeId = "state.missing",
            SucceedExpression = new ExpressionDefinition { Kind = "predicate", Source = "not valid", ResultType = "bool" },
            PublishesOutputFamilies = ["unresolved-family"],
        };
        transition.Command.Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["message"] = "hello",
            ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tool.result.child"] = "$result",
            },
        };
        workflow.Nodes[transition.Id] = transition;

        var result = WorkflowScriptModelVerifier.Verify(workflow, workflow, CreateModelReference("dotnet-so"));

        Assert.False(result.Passed);
        Assert.Contains(result.TestCases, item => item.Id == "graph.target.transition.tool_call" && !item.Passed);
        Assert.Contains(result.TestCases, item => item.Id == "expressions.transition.tool_call.succeed" && !item.Passed);
        Assert.Contains(result.TestCases, item => item.Id == "projection.binding.transition.tool_call.tool.result.child" && !item.Passed);
        Assert.Contains(result.TestCases, item => item.Id == "dataflow.producer.transition.tool_call.unresolved-family" && !item.Passed);
    }

    [Fact]
    public void Verify_RequiresGateValueSemanticsAndRuntimeUpdateEvidence()
    {
        var workflow = CreateStateUpdateWorkflow();
        var resultWithoutHistory = WorkflowScriptModelVerifier.Verify(workflow, workflow, CreateModelReference("dotnet-so"));
        Assert.Contains(resultWithoutHistory.TestCases, item => item.Id == "runtime.updates.transition.update" && item.Skipped);

        workflow.History.Add(new WorkflowHistoryEntry(
            DateTimeOffset.UtcNow,
            "transition.update",
            TaskNodeType.Transition,
            ExecutionStatus.Succeeded));
        var resultWithHistory = WorkflowScriptModelVerifier.Verify(workflow, workflow, CreateModelReference("dotnet-so"));
        Assert.Contains(resultWithHistory.TestCases, item => item.Id == "runtime.updates.transition.update" && !item.Passed);

        workflow.TemplateKind = "so-governed-target-skill";
        workflow.Validation = new WorkflowValidationContract
        {
            Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
            {
                ["gate.final"] = new WorkflowValidationGate
                {
                    PassExpression = new ExpressionDefinition { Source = "true" },
                    RequiredOutputFamilies = ["final_output"],
                },
            },
        };
        var governedResult = WorkflowScriptModelVerifier.Verify(workflow, workflow, CreateModelReference("dotnet-so"));
        Assert.Contains(governedResult.TestCases, item => item.Id == "gates.gate.final.value_semantics.final_output" && !item.Passed);
    }

    [Fact]
    public void VerificationSuite_TracksPassedFailedAndSkippedCases()
    {
        var suite = new WorkflowScriptVerificationSuite();
        suite.Check("pass", true);
        suite.Check("fail", false, "expected failure");
        suite.Skip("skip", "runtime evidence was not supplied");

        var result = suite.Complete();

        Assert.False(result.Passed);
        Assert.Equal(3, result.TotalChecks);
        Assert.Equal(1, result.PassedChecks);
        Assert.Equal(1, result.FailedChecks);
        Assert.Equal(1, result.SkippedChecks);
        Assert.Contains("fail: expected failure", result.Failures);
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
        };
    }

    private static WorkflowInstance CreateStateUpdateWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "01 Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.update",
                    TransitionIds = ["transition.update"],
                },
            ],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "02 Done",
            Groups = [],
        };
        var update = new CommandTransition
        {
            Id = "transition.update",
            Name = "Update",
            WorkflowPhase = "01 Start",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.StateUpdate,
            GuardExpression = new ExpressionDefinition { Source = "true" },
            SucceedExpression = new ExpressionDefinition { Source = "true" },
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.NativeCode,
                Name = "update",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["final_output"] = "expected",
                    },
                },
            },
        };
        return new WorkflowInstance
        {
            InstanceId = "workflow-verification-update",
            RuntimeBinding = "dotnet-so",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [update.Id] = update,
            },
        };
    }
}
