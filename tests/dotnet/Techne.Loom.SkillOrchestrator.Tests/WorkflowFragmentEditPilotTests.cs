using System.Text.Json.Nodes;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Validation;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowFragmentEditPilotTests
{
    [Fact]
    public async Task VersionMismatchDoesNotCreateAScript()
    {
        var draft = WorkflowSchemaDemoExporter.CreateDemoWorkflow("dotnet-so");
        draft.RuntimeVersion = "0.0.0-mismatch";
        var scratch = Directory.CreateTempSubdirectory("loom-fragment-edit-test-");

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowFragmentEditPilot.ApplyAsync(
                WorkflowJsonSerializer.Serialize(draft),
                new WorkflowStateDescriptionEdit(draft.StartNodeId, "Revised description"),
                scratch.FullName));
            Assert.Empty(scratch.GetFiles());
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task StateEditUsesRoslynAndOwningValidatorWithoutChangingOtherFragments()
    {
        const string description = "Revised description: \"quoted\"\nworkflow.Nodes.Clear();";
        var scratch = Directory.CreateTempSubdirectory("loom-fragment-edit-test-");

        try
        {
            var demo = await WorkflowSchemaDemoExporter.WriteAsync(scratch.FullName, "dotnet-so", SkillCli.GetRuntimeVersion());
            var json = await File.ReadAllTextAsync(demo.DemoFile);
            var draft = WorkflowDraftImport.Read(json);
            var result = await WorkflowFragmentEditPilot.ApplyAsync(
                json,
                new WorkflowStateDescriptionEdit(draft.StartNodeId, description),
                scratch.FullName);

            var expected = JsonNode.Parse(json)!;
            expected["nodes"]![draft.StartNodeId]!["description"] = description;
            Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(WorkflowJsonSerializer.Serialize(result))));
            Assert.Single(scratch.GetFiles("fragment-edit-*.cs"));
            Assert.Equal(json, await File.ReadAllTextAsync(demo.DemoFile));
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }
}
