using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class ResumeAdmissionAdditionalTests
{
    [Fact]
    public async Task ResumeAsync_RejectsAmbiguousDuplicateWaitGroupsWithNullCorrelation()
    {
        var instance = new WorkflowInstance
        {
            InstanceId = "resume-ambiguous-null-correlation",
            Status = WorkflowStatus.WaitingExternal,
            ActiveWaitGroups =
            [
                CreateWaitGroup("resume-ambiguous-null-correlation"),
                CreateWaitGroup("resume-ambiguous-null-correlation"),
            ],
        };
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(
            instance.InstanceId,
            "transition.external"));

        Assert.Contains("Multiple active wait groups", error.Message, StringComparison.Ordinal);
        Assert.Contains("<null>", error.Message, StringComparison.Ordinal);
    }

    private static PendingWaitGroup CreateWaitGroup(string instanceId)
    {
        var group = new PendingWaitGroup
        {
            InstanceId = instanceId,
            TransitionId = "transition.external",
        };
        group.AddEntry(null);
        return group;
    }
}
