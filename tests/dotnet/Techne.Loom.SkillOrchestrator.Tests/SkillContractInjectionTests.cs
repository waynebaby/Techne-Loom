using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Abstractions.TaskTracking.Runtime;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class SkillContractInjectionTests
{
    [Fact]
    public async Task TickAsyncInjectsContractFragmentsBeforeReferencedCommand()
    {
        var root = Path.Combine(Path.GetTempPath(), $"techne-loom-contract-engine-{Guid.NewGuid():N}");
        var contractPath = Path.Combine(root, "assets", "so-workflow", "contract.json");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        await File.WriteAllTextAsync(contractPath, """
{
  "name": "engine-contract",
  "inputs": { "request": { "type": "object" } },
  "outputs": { "result": { "type": "object" } },
  "default_assumptions": { "safe": true },
  "rules": { "required": true }
}
""");

        try
        {
            var done = new StateNode
            {
                Id = "state.done",
                Name = "Done",
                Groups = [],
            };
            var start = new StateNode
            {
                Id = "state.start",
                Name = "Start",
                Groups =
                [
                    new TransitionGroup
                    {
                        Id = "group.run",
                        TransitionIds = ["transition.run"],
                    },
                ],
            };
            var transition = new CommandTransition
            {
                Id = "transition.run",
                Name = "Run",
                TargetNodeId = done.Id,
                StepKind = WorkflowStepKind.ToolCall,
                ContractRefs = ["/rules"],
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.Tool,
                    Name = "capture",
                },
            };
            var dispatcher = new CapturingDispatcher();
            var instance = new WorkflowInstance
            {
                InstanceId = $"contract-engine-{Guid.NewGuid():N}",
                StartNodeId = start.Id,
                CurrentNodeId = start.Id,
                EndNodeId = done.Id,
                Status = WorkflowStatus.ReadyToStart,
                ContractBinding = new ContractBinding { AssetRootPath = root },
                Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
                {
                    [start.Id] = start,
                    [done.Id] = done,
                    [transition.Id] = transition,
                },
            };

            var engine = new DefaultTaskTrackingEngine(new InMemoryInstanceStore(), commandDispatcher: dispatcher);
            var outcome = await engine.TickAsync(instance);

            Assert.True(outcome.Progressed);
            Assert.NotNull(dispatcher.Context);
            var envelope = Assert.IsType<Dictionary<string, object?>>(dispatcher.Context!["contract_context"]);
            var fragments = Assert.IsType<Dictionary<string, object?>>(envelope["fragments"]);
            var rules = Assert.IsType<JsonElement>(fragments["/rules"]);
            Assert.True(rules.GetProperty("required").GetBoolean());
            var readMetadata = Assert.IsType<Dictionary<string, object?>>(dispatcher.Context["loom_runtime.contract_read"]);
            Assert.EndsWith(Path.Combine("assets", "so-workflow", "contract.json"), Assert.IsType<string>(readMetadata["path"]), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task FailedContractReadKeepsPreviousContractContext()
    {
        var root = Path.Combine(Path.GetTempPath(), $"techne-loom-contract-failure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var done = new StateNode
            {
                Id = "state.done",
                Name = "Done",
                Groups = [],
            };
            var start = new StateNode
            {
                Id = "state.start",
                Name = "Start",
                Groups =
                [
                    new TransitionGroup
                    {
                        Id = "group.run",
                        TransitionIds = ["transition.run"],
                    },
                ],
            };
            var transition = new CommandTransition
            {
                Id = "transition.run",
                Name = "Run",
                TargetNodeId = done.Id,
                StepKind = WorkflowStepKind.ToolCall,
                ContractRefs = ["/rules"],
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.Tool,
                    Name = "capture",
                },
            };
            var instance = new WorkflowInstance
            {
                InstanceId = $"contract-failure-{Guid.NewGuid():N}",
                StartNodeId = start.Id,
                CurrentNodeId = start.Id,
                EndNodeId = done.Id,
                Status = WorkflowStatus.ReadyToStart,
                ContractBinding = new ContractBinding { AssetRootPath = root },
                Context = new Dictionary<string, object?>(StringComparer.Ordinal),
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
            instance.Context["contract_context"] = previousContext;
            instance.Context["loom_runtime.contract_read"] = previousRead;

            var engine = new DefaultTaskTrackingEngine(new InMemoryInstanceStore());
            var outcome = await engine.TickAsync(instance);

            Assert.True(outcome.Failed);
            Assert.Same(previousContext, instance.Context["contract_context"]);
            Assert.Same(previousRead, instance.Context["loom_runtime.contract_read"]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class CapturingDispatcher : ICommandDispatcher
    {
        public IReadOnlyDictionary<string, object?>? Context { get; private set; }

        public Task<object?> ExecuteAsync(
            CommandInvocation invocation,
            IReadOnlyDictionary<string, object?> workflowContextReference,
            IProgress<object>? progress,
            CancellationToken ct)
        {
            Context = workflowContextReference;
            return Task.FromResult<object?>(null);
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
