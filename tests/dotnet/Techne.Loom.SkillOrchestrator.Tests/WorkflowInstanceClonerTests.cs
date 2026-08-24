using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowInstanceClonerTests
{
    [Fact]
    public void Clone_DeepClonesArrayValuesAndNestedObjects()
    {
        var source = new WorkflowInstance
        {
            InstanceId = "array-clone",
            Context = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["items"] = new object?[]
                {
                    "original",
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["state"] = "original" },
                },
            },
        };

        var clone = WorkflowInstanceCloner.Clone(source);
        var sourceItems = Assert.IsType<object[]>(source.Context["items"]);
        var clonedItems = Assert.IsType<object[]>(clone.Context["items"]);

        Assert.NotSame(sourceItems, clonedItems);
        Assert.NotSame(sourceItems[1], clonedItems[1]);

        var clonedNested = Assert.IsAssignableFrom<IDictionary<string, object?>>(clonedItems[1]);
        clonedNested["state"] = "changed";
        Assert.Equal("original", Assert.IsAssignableFrom<IDictionary<string, object?>>(sourceItems[1])["state"]);
    }
}
