using System.Text.Json;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowFragmentReaderTests
{
    [Fact]
    public async Task ReadAsync_DefaultReturnsSummaryWithoutWorkflowValues()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-fragment-summary-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(workflowFile, """
{
  "instanceId": "fragment-summary",
  "status": "running",
  "startNodeId": "state.start",
  "currentNodeId": "state.start",
  "endNodeId": "state.done",
  "version": 7,
  "context": {
    "secret": "do-not-return",
    "request_kind": "analysis"
  },
  "nodes": {
    "state.start": { "$kind": "state", "groups": [] },
    "state.done": { "$kind": "state", "groups": [] }
  },
  "history": [],
  "activeWaitGroups": []
}
""");

            var result = await WorkflowFragmentReader.ReadAsync(workflowFile);

            Assert.Null(result.Fragment);
            Assert.False(result.Truncated);
            Assert.Equal("fragment-summary", result.Summary.InstanceId);
            Assert.Equal(7, result.Summary.Version);
            Assert.Equal(["secret", "request_kind"], result.Summary.ContextKeys);
            var serialized = JsonSerializer.Serialize(result);
            Assert.DoesNotContain("do-not-return", serialized);
            Assert.DoesNotContain(workflowFile, serialized);
        }
        finally
        {
            DeleteFile(workflowFile);
        }
    }

    [Fact]
    public async Task ReadAsync_ProjectsJsonPointerAndBoundsArrayItems()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-fragment-array-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(workflowFile, """
{
  "instanceId": "fragment-array",
  "status": "readyToStart",
  "context": {
    "items": ["one", "two", "three"]
  },
  "nodes": {},
  "history": [],
  "activeWaitGroups": []
}
""");

            var result = await WorkflowFragmentReader.ReadAsync(
                workflowFile,
                "/context/items",
                new WorkflowFragmentLimits(MaxBytes: 256, MaxArrayItems: 2, MaxDepth: 4));

            Assert.NotNull(result.Fragment);
            Assert.Equal(JsonValueKind.Array, result.Fragment!.Value.ValueKind);
            Assert.Equal(2, result.Fragment.Value.GetArrayLength());
            Assert.True(result.Truncated);
            Assert.Equal("max_array_items", result.TruncationReason);
        }
        finally
        {
            DeleteFile(workflowFile);
        }
    }

    [Fact]
    public async Task ReadAsync_RejectsOversizedExplicitRootFragment()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-fragment-root-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(workflowFile, """
{
  "instanceId": "fragment-root",
  "context": {
    "large": "012345678901234567890123456789012345678901234567890123456789"
  },
  "nodes": {},
  "history": [],
  "activeWaitGroups": []
}
""");

            var result = await WorkflowFragmentReader.ReadAsync(
                workflowFile,
                string.Empty,
                new WorkflowFragmentLimits(MaxBytes: 128, MaxArrayItems: 4, MaxDepth: 4));

            Assert.Null(result.Fragment);
            Assert.True(result.Truncated);
            Assert.Equal("max_bytes", result.TruncationReason);
        }
        finally
        {
            DeleteFile(workflowFile);
        }
    }

    [Fact]
    public async Task ReadAsync_ResolvesEscapedSegmentsAndAllowsExplicitContextFragments()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-fragment-escaped-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(workflowFile, """
{
  "instanceId": "fragment-escaped",
  "context": {
    "a/b": "slash",
    "m~n": "tilde",
    "scalar": "value"
  },
  "nodes": {},
  "history": [],
  "activeWaitGroups": []
}
""");

            var slash = await WorkflowFragmentReader.ReadAsync(workflowFile, "/context/a~1b");
            var tilde = await WorkflowFragmentReader.ReadAsync(workflowFile, "/context/m~0n");
            var explicitContext = await WorkflowFragmentReader.ReadAsync(workflowFile, "/context/scalar");

            Assert.Equal("slash", slash.Fragment!.Value.GetString());
            Assert.Equal("tilde", tilde.Fragment!.Value.GetString());
            Assert.Equal("value", explicitContext.Fragment!.Value.GetString());
        }
        finally
        {
            DeleteFile(workflowFile);
        }
    }

    [Fact]
    public async Task ReadAsync_RejectsScalarDescentAndInvalidArrayIndexes()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-fragment-index-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(workflowFile, """
{
  "instanceId": "fragment-index",
  "context": {
    "scalar": "value",
    "items": ["one", "two"]
  },
  "nodes": {},
  "history": [],
  "activeWaitGroups": []
}
""");

            var scalarError = await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowFragmentReader.ReadAsync(workflowFile, "/context/scalar/child"));
            var leadingZeroError = await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowFragmentReader.ReadAsync(workflowFile, "/context/items/01"));
            var negativeError = await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowFragmentReader.ReadAsync(workflowFile, "/context/items/-1"));
            var outOfRangeError = await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowFragmentReader.ReadAsync(workflowFile, "/context/items/2"));

            Assert.Contains("cannot descend", scalarError.Message, StringComparison.Ordinal);
            Assert.Contains("array index", leadingZeroError.Message, StringComparison.Ordinal);
            Assert.Contains("array index", negativeError.Message, StringComparison.Ordinal);
            Assert.Contains("array index", outOfRangeError.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteFile(workflowFile);
        }
    }

    [Fact]
    public async Task ReadAsync_BoundsObjectPropertiesSeparately()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-fragment-object-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(workflowFile, """
{
  "instanceId": "fragment-object",
  "context": {
    "object": {
      "first": "one",
      "second": "two"
    }
  },
  "nodes": {},
  "history": [],
  "activeWaitGroups": []
}
""");

            var result = await WorkflowFragmentReader.ReadAsync(
                workflowFile,
                "/context/object",
                new WorkflowFragmentLimits(MaxBytes: 256, MaxArrayItems: 8, MaxDepth: 4) { MaxObjectProperties = 1 });

            Assert.NotNull(result.Fragment);
            Assert.Equal(JsonValueKind.Object, result.Fragment!.Value.ValueKind);
            Assert.Single(result.Fragment.Value.EnumerateObject());
            Assert.True(result.Truncated);
            Assert.Equal("max_properties", result.TruncationReason);
        }
        finally
        {
            DeleteFile(workflowFile);
        }
    }

    [Fact]
    public async Task ReadAsync_RejectsInvalidLimits()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-fragment-limits-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(workflowFile, "{\"instanceId\":\"fragment-limits\",\"nodes\":{},\"history\":[],\"activeWaitGroups\":[]}");

            var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => WorkflowFragmentReader.ReadAsync(workflowFile, limits: new WorkflowFragmentLimits(MaxBytes: 64)));

            Assert.Equal("MaxBytes", error.ParamName);
        }
        finally
        {
            DeleteFile(workflowFile);
        }
    }

    [Fact]
    public async Task ReadAsync_RejectsInvalidJsonPointer()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-fragment-pointer-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(workflowFile, "{\"instanceId\":\"fragment-pointer\",\"nodes\":{}}");

            var error = await Assert.ThrowsAsync<ArgumentException>(() => WorkflowFragmentReader.ReadAsync(workflowFile, "context/items"));

            Assert.Contains("must be empty or start with '/'", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteFile(workflowFile);
        }
    }

    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var lockFile = path + ".lock";
        if (File.Exists(lockFile))
        {
            File.Delete(lockFile);
        }
    }
}