using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class TargetContractEvidenceTests
{
    [Fact]
    public async Task MemoryReadProducesTargetContractEvidenceThroughOutputBinding()
    {
        var root = Path.Combine(Path.GetTempPath(), $"techne-loom-target-contract-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "assets", "so-workflow"));
        await File.WriteAllTextAsync(Path.Combine(root, "assets", "so-workflow", "contract.json"), """
{
  "name": "target-contract",
  "inputs": {},
  "outputs": {},
  "default_assumptions": {},
  "rules": { "ready": true }
}
""");

        try
        {
            var done = new StateNode { Id = "state.done", Name = "Done", Groups = [] };
            var start = new StateNode
            {
                Id = "state.start",
                Name = "Start",
                Groups =
                [
                    new TransitionGroup
                    {
                        Id = "group.inspect",
                        TransitionIds = ["transition.inspect"],
                    },
                ],
            };
            var inspect = new CommandTransition
            {
                Id = "transition.inspect",
                Name = "Inspect",
                TargetNodeId = done.Id,
                StepKind = WorkflowStepKind.MemoryRead,
                OutputPath = "internal_document_evidence",
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.NativeCode,
                    Name = "workflow.inspectExistingWorkflowAssets",
                    Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["assetRootInput"] = "target_skill_path",
                        ["checkedInAssets"] = new List<object?> { "assets/so-workflow/contract.json" },
                        ["targetContractPath"] = "assets/so-workflow/contract.json",
                        ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["target_skill_contract_evidence"] = "$context:internal_document_evidence.target_skill_contract_evidence",
                        },
                    },
                },
            };
            var instance = new WorkflowInstance
            {
                InstanceId = "target-contract-evidence",
                StartNodeId = start.Id,
                CurrentNodeId = start.Id,
                EndNodeId = done.Id,
                Status = WorkflowStatus.ReadyToStart,
                Context = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["target_skill_path"] = root,
                },
                Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
                {
                    [start.Id] = start,
                    [done.Id] = done,
                    [inspect.Id] = inspect,
                },
            };

            var engine = new DefaultTaskTrackingEngine(new InMemoryInstanceStore());
            var outcome = await engine.TickAsync(instance);

            Assert.True(outcome.Progressed);
            var internalEvidence = Assert.IsType<Dictionary<string, object?>>(instance.Context["internal_document_evidence"]);
            var evidence = Assert.IsType<Dictionary<string, object?>>(internalEvidence["target_skill_contract_evidence"]);
            Assert.True((bool)evidence["parsed"]!);
            Assert.Equal("assets/so-workflow/contract.json", evidence["path"]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
