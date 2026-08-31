using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpFragmentReadersTests
{
    [Fact]
    public async Task EventReaderReturnsOnlyTheBoundedTail()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"techne-loom-mcp-events-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var workflowFile = Path.Combine(directory, "workflow.json");
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, new WorkflowInstance
        {
            InstanceId = "event-fragment-test",
            CurrentNodeId = "state.start",
            StartNodeId = "state.start",
        });
        await WorkflowFileEventLog.AppendAsync(workflowFile, CreateEvent("state.one", "first"));
        await WorkflowFileEventLog.AppendAsync(workflowFile, CreateEvent("state.two", "second"));

        try
        {
            var result = await WorkflowEventFragmentReader.ReadAsync(
                workflowFile,
                new WorkflowEventFragmentLimits(MaxBytes: 1024, MaxEvents: 1));

            Assert.Single(result.Events);
            Assert.True(result.Truncated);
            Assert.Equal("max_events", result.TruncationReason);
            Assert.Equal("state.two", result.Events[0].GetProperty("current_node_id").GetString());
            Assert.True(result.ReturnedBytes <= 1024);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EventReaderReportsByteTruncationWithoutReturningAnOversizedEvent()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"techne-loom-mcp-event-limit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var workflowFile = Path.Combine(directory, "workflow.json");
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, new WorkflowInstance
        {
            InstanceId = "event-limit-test",
            CurrentNodeId = "state.start",
            StartNodeId = "state.start",
        });
        await WorkflowFileEventLog.AppendAsync(workflowFile, CreateEvent("state.start", new string('x', 600)));

        try
        {
            var result = await WorkflowEventFragmentReader.ReadAsync(
                workflowFile,
                new WorkflowEventFragmentLimits(MaxBytes: 128, MaxEvents: 4));

            Assert.Empty(result.Events);
            Assert.True(result.Truncated);
            Assert.Equal("max_bytes", result.TruncationReason);
            Assert.Equal(0, result.ReturnedBytes);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ArtifactManifestListsOnlyCanonicalWorkflowCompanions()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"techne-loom-mcp-manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var workflowFile = Path.Combine(directory, "workflow.json");
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, new WorkflowInstance
        {
            InstanceId = "manifest-test",
            CurrentNodeId = "state.start",
            StartNodeId = "state.start",
        });
        await WorkflowFileEventLog.AppendAsync(workflowFile, CreateEvent("state.start", "created"));

        try
        {
            var result = WorkflowArtifactManifestReader.Read(workflowFile);

            Assert.Equal(2, result.Artifacts.Count);
            Assert.Contains(result.Artifacts, item => item.Kind == "workflow" && item.Exists && item.Path == workflowFile);
            Assert.Contains(result.Artifacts, item => item.Kind == "events" && item.Exists && item.Path == workflowFile + ".events.jsonl");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static WorkflowFileEventRecord CreateEvent(string currentNodeId, string error)
        => new(
            DateTimeOffset.UtcNow,
            "execution",
            "workflow.json",
            "event-fragment-test",
            "running",
            "waitingExternal",
            currentNodeId,
            "transition.test",
            WorkflowStepKind.Plan.ToString(),
            error);
}
