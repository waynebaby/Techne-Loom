using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Validation;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowFragmentDesignSessionTests
{
    [Fact]
    public async Task ValidatedChangeAutomaticallyReplacesOnlyTheAuthorizedFrozenRevision()
    {
        var scratch = Directory.CreateTempSubdirectory("loom-fragment-session-test-");
        try
        {
            var export = await WorkflowSchemaDemoExporter.WriteAsync(scratch.FullName, "dotnet-so", SkillCli.GetRuntimeVersion());
            var json = await File.ReadAllTextAsync(export.DemoFile);
            var draft = WorkflowDraftImport.Read(json);
            var session = await WorkflowFragmentDesignSession.StartAsync(json, [new(WorkflowFragmentKind.State, draft.StartNodeId)]);
            var rejected = new WorkflowStateDescriptionEdit(draft.EndNodeId!, "Must not change");
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.ApplyAsync(0, rejected, "Outside scope"));

            var edit = new WorkflowStateDescriptionEdit(draft.StartNodeId, "Clarify existing start responsibilities.");
            var next = await session.ApplyAsync(0, edit, "Requested documentation update.");
            var changed = new WorkflowFragmentId(WorkflowFragmentKind.State, draft.StartNodeId);
            var untouched = new WorkflowFragmentId(WorkflowFragmentKind.State, draft.EndNodeId!);

            Assert.Equal(1, next.Generation);
            Assert.Equal(1, next.Fragments[changed].ContentRevision);
            Assert.Equal(0, next.Fragments[untouched].ContentRevision);
            Assert.True(next.Fragments[changed].Frozen);
            Assert.True(next.Fragments[untouched].Frozen);
            Assert.Collection(next.LockEvents.Where(item => item.Generation == 1),
                item => Assert.Equal(WorkflowFragmentLockAction.Unfreeze, item.Action),
                item => Assert.Equal(WorkflowFragmentLockAction.Freeze, item.Action));
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.ApplyAsync(0, edit, "Stale edit."));
            Assert.Same(next, await session.ApplyAsync(1, edit, "Repeat identical edit."));

            var competing = await Task.WhenAll(
                Record.ExceptionAsync(() => session.ApplyAsync(1, edit with { Description = "First candidate" }, "Authorized edit.")),
                Record.ExceptionAsync(() => session.ApplyAsync(1, edit with { Description = "Second candidate" }, "Authorized edit.")));
            Assert.Single(competing, error => error is null);
            Assert.IsType<InvalidOperationException>(Assert.Single(competing, error => error is not null));
            Assert.Equal(2, session.Current.Generation);
            Assert.Equal(2, session.Current.LockEvents.Count(item => item.Generation == 2));
            Assert.Equal(json, await File.ReadAllTextAsync(export.DemoFile));
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }
}
