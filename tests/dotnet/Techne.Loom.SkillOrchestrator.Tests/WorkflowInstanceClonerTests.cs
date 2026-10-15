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
    [Fact]
    public void Clone_CopiesContractBinding()
    {
        var source = new WorkflowInstance
        {
            ContractBinding = new ContractBinding
            {
                Path = "contract.json",
                Format = "json",
                AssetRootInput = "skill.root",
                AssetRootPath = "C:/skills/example",
            },
        };

        var clone = WorkflowInstanceCloner.Clone(source);

        Assert.NotNull(clone.ContractBinding);
        Assert.Equal(source.ContractBinding.Path, clone.ContractBinding.Path);
        Assert.Equal(source.ContractBinding.Format, clone.ContractBinding.Format);
        Assert.Equal(source.ContractBinding.AssetRootInput, clone.ContractBinding.AssetRootInput);
        Assert.Equal(source.ContractBinding.AssetRootPath, clone.ContractBinding.AssetRootPath);
    }
}
