using System.Text.Json;
using System.Text.Json.Nodes;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowDraftImportTests
{
    [Fact]
    public void CanonicalDraftSurvivesNoOpRoundTrip()
    {
        var original = CreateDraft();
        var imported = WorkflowDraftImport.Read(original.ToJsonString());

        Assert.True(JsonNode.DeepEquals(original, JsonNode.Parse(WorkflowJsonSerializer.Serialize(imported))));
    }

    [Fact]
    public void OwningSchemaDemoSurvivesNoOpRoundTrip()
    {
        var demo = WorkflowSchemaDemoExporter.CreateDemoWorkflow("dotnet-so");
        var entry = (StateNode)demo.Nodes[demo.StartNodeId];
        var group = entry.Groups[0];
        group.TimeoutTransition = new ExpressionTransition
        {
            Id = "timeout-fixture",
            TargetNodeId = demo.EndNodeId,
        };
        var json = WorkflowJsonSerializer.Serialize(demo);
        var imported = WorkflowDraftImport.Read(json);

        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(json),
            JsonNode.Parse(WorkflowJsonSerializer.Serialize(imported))));
    }

    [Fact]
    public void UnknownNestedFieldReportsEscapedPointer()
    {
        var original = CreateDraft();
        original["nodes"]!["start"]!["future/~"] = true;

        var exception = Assert.Throws<JsonException>(() => WorkflowDraftImport.Read(original.ToJsonString()));

        Assert.Contains("/nodes/start/future~1~0", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicatePropertyIsRejectedBeforeModelDeserialization()
    {
        const string json = """{"context":{"a/b":false,"a/b":true}}""";

        var exception = Assert.Throws<JsonException>(() => WorkflowDraftImport.Read(json));

        Assert.Contains("/context/a~1b", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NumericNormalizationThatCanChangeContextTypeIsRejected()
    {
        var original = CreateDraft();
        original["context"]!["amount"] = JsonNode.Parse("1e0");

        var exception = Assert.Throws<JsonException>(() => WorkflowDraftImport.Read(original.ToJsonString()));

        Assert.Contains("/context/amount", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NullCollectionIsRejectedWithItsPointer()
    {
        var original = CreateDraft();
        original["nodes"] = null;

        var exception = Assert.Throws<JsonException>(() => WorkflowDraftImport.Read(original.ToJsonString()));

        Assert.Contains("/nodes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadyStatusDoesNotMakeAnEnteredStateEditable()
    {
        var original = CreateDraft();
        original["nodes"]!["start"]!["entranceTime"] = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<InvalidOperationException>(() => WorkflowDraftImport.Read(original.ToJsonString()));
    }

    private static JsonNode CreateDraft()
    {
        var workflow = new WorkflowInstance
        {
            InstanceId = "draft",
            StartNodeId = "start",
            CurrentNodeId = "start",
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["start"] = new StateNode { Id = "start", Name = "Start" },
            },
        };
        return JsonNode.Parse(WorkflowJsonSerializer.Serialize(workflow))!;
    }
}
