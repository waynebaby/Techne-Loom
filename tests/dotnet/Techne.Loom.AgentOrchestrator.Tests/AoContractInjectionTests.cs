using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.AgentOrchestrator.Runtime;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class AoContractInjectionTests
{
    [Fact]
    public async Task BridgeEnrichesCurrentContractFragmentsForBoundaryContext()
    {
        var root = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-contract-{Guid.NewGuid():N}");
        var contractPath = Path.Combine(root, "assets", "so-workflow", "contract.json");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        await File.WriteAllTextAsync(contractPath, """
{
  "name": "ao-contract",
  "inputs": { "request": { "type": "object" } },
  "outputs": { "result": { "type": "object" } },
  "default_assumptions": { "safe": true },
  "rules": { "approved": true }
}
""");

        try
        {
            var done = new StateNode { Id = "state.done", Name = "Done", Groups = [] };
            var transition = new CommandTransition
            {
                Id = "transition.boundary",
                Name = "Boundary",
                TargetNodeId = done.Id,
                StepKind = WorkflowStepKind.WaitResume,
                ContractRefs = ["/rules"],
            };
            var start = new StateNode
            {
                Id = "state.start",
                Name = "Start",
                Groups =
                [
                    new TransitionGroup
                    {
                        Id = "group.boundary",
                        TransitionIds = [transition.Id],
                    },
                ],
            };
            var workflow = new WorkflowInstance
            {
                InstanceId = "ao-contract-instance",
                CurrentNodeId = start.Id,
                StartNodeId = start.Id,
                ContractBinding = new ContractBinding { AssetRootPath = root },
                Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
                {
                    [start.Id] = start,
                    [done.Id] = done,
                    [transition.Id] = transition,
                },
            };

            await AoRuntimeWorkflowBridge.EnrichContractContextAsync(workflow);

            var envelope = Assert.IsType<Dictionary<string, object?>>(workflow.Context["contract_context"]);
            var fragments = Assert.IsType<Dictionary<string, object?>>(envelope["fragments"]);
            var rules = Assert.IsType<JsonElement>(fragments["/rules"]);
            Assert.True(rules.GetProperty("approved").GetBoolean());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task BridgeMergesContractFragmentsFromCurrentBoundaryCandidates()
    {
        var root = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-contract-branches-{Guid.NewGuid():N}");
        var contractPath = Path.Combine(root, "assets", "so-workflow", "contract.json");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        await File.WriteAllTextAsync(contractPath, """
{
  "name": "ao-branch-contract",
  "inputs": { "request": { "type": "object" } },
  "outputs": { "result": { "type": "object" } },
  "default_assumptions": { "safe": true },
  "rules": { "first": true, "second": true }
}
""");

        try
        {
            var done = new StateNode { Id = "state.done", Name = "Done", Groups = [] };
            var first = new CommandTransition
            {
                Id = "transition.first",
                Name = "First",
                TargetNodeId = done.Id,
                StepKind = WorkflowStepKind.WaitResume,
                ContractRefs = ["/rules/first"],
            };
            var second = new CommandTransition
            {
                Id = "transition.second",
                Name = "Second",
                TargetNodeId = done.Id,
                StepKind = WorkflowStepKind.WaitResume,
                ContractRefs = ["/rules/second"],
            };
            var start = new StateNode
            {
                Id = "state.start",
                Name = "Start",
                Groups =
                [
                    new TransitionGroup
                    {
                        Id = "group.boundary",
                        TransitionIds = [first.Id, second.Id],
                    },
                ],
            };
            var workflow = new WorkflowInstance
            {
                InstanceId = "ao-contract-branch-instance",
                CurrentNodeId = start.Id,
                StartNodeId = start.Id,
                ContractBinding = new ContractBinding { AssetRootPath = root },
                Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
                {
                    [start.Id] = start,
                    [done.Id] = done,
                    [first.Id] = first,
                    [second.Id] = second,
                },
            };

            await AoRuntimeWorkflowBridge.EnrichContractContextAsync(workflow);

            var envelope = Assert.IsType<Dictionary<string, object?>>(workflow.Context["contract_context"]);
            var fragments = Assert.IsType<Dictionary<string, object?>>(envelope["fragments"]);
            Assert.True(Assert.IsType<JsonElement>(fragments["/rules/first"]).GetBoolean());
            Assert.True(Assert.IsType<JsonElement>(fragments["/rules/second"]).GetBoolean());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task BridgeKeepsPreviousContractContextWhenReadFails()
    {
        var root = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-contract-failure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var done = new StateNode { Id = "state.done", Name = "Done", Groups = [] };
            var transition = new CommandTransition
            {
                Id = "transition.boundary",
                Name = "Boundary",
                TargetNodeId = done.Id,
                StepKind = WorkflowStepKind.WaitResume,
                ContractRefs = ["/rules"],
            };
            var start = new StateNode
            {
                Id = "state.start",
                Name = "Start",
                Groups =
                [
                    new TransitionGroup
                    {
                        Id = "group.boundary",
                        TransitionIds = [transition.Id],
                    },
                ],
            };
            var workflow = new WorkflowInstance
            {
                InstanceId = "ao-contract-failure-instance",
                CurrentNodeId = start.Id,
                StartNodeId = start.Id,
                ContractBinding = new ContractBinding { AssetRootPath = root },
                Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
                {
                    [start.Id] = start,
                    [done.Id] = done,
                    [transition.Id] = transition,
                },
            };
            var previousContext = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["fragments"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["/rules"] = "previous",
                },
            };
            var previousRead = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["path"] = "previous-contract.json",
            };
            workflow.Context["contract_context"] = previousContext;
            workflow.Context["ao_runtime.contract_read"] = previousRead;

            await Assert.ThrowsAsync<FileNotFoundException>(() => AoRuntimeWorkflowBridge.EnrichContractContextAsync(workflow));

            Assert.Same(previousContext, workflow.Context["contract_context"]);
            Assert.Same(previousRead, workflow.Context["ao_runtime.contract_read"]);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }


    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
