using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Validation;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowFragmentSessionStoreTests
{
    [Fact]
    public async Task ReopenedSessionPublishesOneCompleteGenerationAndIgnoresStaging()
    {
        var sourceRoot = Directory.CreateTempSubdirectory("loom-fragment-source-test-");
        WorkflowFragmentSessionStore? store = null;
        try
        {
            var demo = await WorkflowSchemaDemoExporter.WriteAsync(sourceRoot.FullName, "dotnet-so", SkillCli.GetRuntimeVersion());
            var baseline = await File.ReadAllTextAsync(demo.DemoFile);
            var draft = WorkflowDraftImport.Read(baseline);
            var first = new WorkflowStateDescriptionEdit(draft.StartNodeId, "First review");
            var second = first with { Description = "Second review" };
            (store, _) = await WorkflowFragmentSessionStore.CreateAsync(demo.DemoFile, [first.Fragment]);
            var reopened = WorkflowFragmentSessionStore.Open(store.Root);
            await File.WriteAllTextAsync(Path.Combine(store.Root, "head.orphan.tmp"), "incomplete");

            var competing = await Task.WhenAll(
                Record.ExceptionAsync(() => store.ApplyAsync(0, first, "Authorized description update.")),
                Record.ExceptionAsync(() => reopened.ApplyAsync(0, second, "Authorized description update.")));
            Assert.Single(competing, failure => failure is null);
            Assert.IsType<InvalidOperationException>(Assert.Single(competing, failure => failure is not null));

            var restored = await WorkflowFragmentSessionStore.Open(store.Root).ReadAsync();
            Assert.Equal(1, restored.Generation);
            Assert.Equal(1, restored.Fragments[first.Fragment].ContentRevision);
            Assert.True(restored.Fragments[first.Fragment].Frozen);
            Assert.Equal(2, restored.LockEvents.Count(item => item.Generation == 1));
            Assert.Equal(2, Directory.GetFiles(store.Root, "*.json").Length);
            Assert.Throws<InvalidOperationException>(() => WorkflowFragmentDesignSession.Restore(
                [first.Fragment], restored with { LockEvents = [] }));
            Assert.Equal(baseline, await File.ReadAllTextAsync(demo.DemoFile));
        }
        finally
        {
            if (store is not null)
            {
                Directory.Delete(store.Root, recursive: true);
            }
            sourceRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ChangedSourceOrCatalogAndCorruptedSnapshotPreventReuse()
    {
        var sourceRoot = Directory.CreateTempSubdirectory("loom-fragment-source-test-");
        WorkflowFragmentSessionStore? store = null;
        try
        {
            var demo = await WorkflowSchemaDemoExporter.WriteAsync(sourceRoot.FullName, "dotnet-so", SkillCli.GetRuntimeVersion());
            (store, _) = await WorkflowFragmentSessionStore.CreateAsync(demo.DemoFile, []);
            var source = await File.ReadAllTextAsync(demo.DemoFile);
            await File.AppendAllTextAsync(demo.DemoFile, " ");
            var sourceDrift = await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadAsync());
            Assert.Contains("source workflow changed", sourceDrift.Message, StringComparison.Ordinal);
            await File.WriteAllTextAsync(demo.DemoFile, source);

            var catalog = await File.ReadAllTextAsync(demo.ContextContractFile);
            await File.AppendAllTextAsync(demo.ContextContractFile, " ");
            var catalogDrift = await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadAsync());
            Assert.Contains("context catalog changed", catalogDrift.Message, StringComparison.Ordinal);
            await File.WriteAllTextAsync(demo.ContextContractFile, catalog);

            await File.AppendAllTextAsync(Assert.Single(Directory.GetFiles(store.Root, "*.json")), " ");
            var corruption = await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadAsync());
            Assert.Contains("checksum", corruption.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (store is not null)
            {
                Directory.Delete(store.Root, recursive: true);
            }
            sourceRoot.Delete(recursive: true);
        }
    }
}
