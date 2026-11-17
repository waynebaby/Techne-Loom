using System.Text.Json.Nodes;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Validation;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowFragmentEditPilotTests : SkillOrchestratorBehaviorTestBase
{
    [Fact]
    public async Task GovernedGateEditCannotDropRequiredEvidenceReferences()
    {
        var draft = CreateGovernedWorkflow();
        draft.RuntimeVersion = SkillCli.GetRuntimeVersion();
        var edit = new WorkflowGatePredicateEdit("gate.assessment", "true");
        var session = await WorkflowFragmentDesignSession.StartAsync(
            WorkflowJsonSerializer.Serialize(draft), [edit.Fragment]);
        var initial = session.Current;

        var failure = await Assert.ThrowsAsync<WorkflowValidationException>(
            () => session.ApplyAsync(0, edit, "Try a predicate that omits required evidence."));

        Assert.Contains("does not reference required output family", failure.Message, StringComparison.Ordinal);
        Assert.Same(initial, session.Current);
    }

    [Fact]
    public async Task TypedTopologyAndGateEditsPreserveUnownedJson()
    {
        var scratch = Directory.CreateTempSubdirectory("loom-fragment-edit-test-");
        try
        {
            var demo = await WorkflowSchemaDemoExporter.WriteAsync(scratch.FullName, "dotnet-so", SkillCli.GetRuntimeVersion());
            var json = await File.ReadAllTextAsync(demo.DemoFile);
            var expected = JsonNode.Parse(json)!;
            const string transition = "transition.reset_rework";
            const string newTarget = "state.artifact";
            WorkflowFragmentEdit[] edits =
            [
                new WorkflowTransitionTargetEdit(transition, newTarget),
                new WorkflowGroupLinksEdit("state.review", "group.condition", ["transition.rework", "transition.approve"]),
                new WorkflowGatePredicateEdit("gate.final", "(context.Get<string>(\"completion_manifest\") != null)"),
            ];
            var session = await WorkflowFragmentDesignSession.StartAsync(json, edits.Select(edit => edit.Fragment));
            foreach (var edit in edits)
            {
                await session.ApplyAsync(session.Current.Generation, edit, "Apply an authorized fixture edit.");
            }

            expected["nodes"]![transition]!["targetNodeId"] = newTarget;
            expected["nodes"]!["state.review"]!["groups"]![0]!["transitionIds"] =
                new JsonArray("transition.rework", "transition.approve");
            expected["validation"]!["gates"]!["gate.final"]!["passExpression"]!["source"] =
                "(context.Get<string>(\"completion_manifest\") != null)";
            Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(session.Current.WorkflowJson)));

            await Assert.ThrowsAsync<WorkflowValidationException>(() => session.ApplyAsync(
                3, new WorkflowGatePredicateEdit("gate.final", "1"), "Try a non-boolean predicate."));
            Assert.Equal(3, session.Current.Generation);
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

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
