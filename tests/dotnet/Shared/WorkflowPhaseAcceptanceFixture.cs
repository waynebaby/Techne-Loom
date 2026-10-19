using System.Text.Json;
using System.Text.Json.Nodes;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.Tests.Shared;

internal sealed class WorkflowPhaseAcceptanceFixture
{
    private WorkflowPhaseAcceptanceFixture(
        int authoredStateCount,
        string workflowId,
        string contractJson,
        string skeletonJson,
        WorkflowPhaseSkeleton skeleton,
        ContextFieldCatalog catalog,
        IReadOnlyList<WorkflowPhaseFragment> fragments,
        string endNodeId)
    {
        AuthoredStateCount = authoredStateCount;
        WorkflowId = workflowId;
        ContractJson = contractJson;
        SkeletonJson = skeletonJson;
        Skeleton = skeleton;
        Catalog = catalog;
        Fragments = fragments;
        EndNodeId = endNodeId;
    }

    public int AuthoredStateCount { get; }

    public string WorkflowId { get; }

    public string ContractJson { get; }

    public string SkeletonJson { get; }

    public WorkflowPhaseSkeleton Skeleton { get; }

    public ContextFieldCatalog Catalog { get; }

    public IReadOnlyList<WorkflowPhaseFragment> Fragments { get; }

    public string EndNodeId { get; }

    public static WorkflowPhaseAcceptanceFixture Create(int authoredStateCount, int softPhaseLimit = 25)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(authoredStateCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(softPhaseLimit);

        var workflowId = $"phase-acceptance-{authoredStateCount}";
        var phaseCount = (authoredStateCount + softPhaseLimit - 1) / softPhaseLimit;
        var phases = new JsonArray();
        var routes = new JsonArray();
        var fragments = new List<WorkflowPhaseFragment>(phaseCount);
        var stateOffset = 0;
        var endNodeId = string.Empty;

        for (var phaseIndex = 0; phaseIndex < phaseCount; phaseIndex++)
        {
            var phaseId = $"p{phaseIndex:D2}_phase";
            var prefix = phaseId + ".";
            var stateCount = Math.Min(softPhaseLimit, authoredStateCount - stateOffset);
            var isLastPhase = phaseIndex == phaseCount - 1;
            var entryId = $"{prefix}state000";
            var exitId = $"{prefix}done";
            var nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal);

            for (var stateIndex = 0; stateIndex < stateCount; stateIndex++)
            {
                var stateId = $"{prefix}state{stateIndex:D3}";
                var isLastState = stateIndex == stateCount - 1;
                var isFinalState = isLastPhase && isLastState;
                var hasTransition = !isFinalState;
                var transitionId = stateIndex == stateCount - 1 || (isLastPhase && stateIndex == stateCount - 2)
                    ? exitId
                    : $"{prefix}step{stateIndex:D3}";
                var phaseName = $"{phaseIndex + 1:D2} Phase {phaseIndex + 1:D2}";
                nodes.Add(stateId, new StateNode
                {
                    Id = stateId,
                    Name = $"State {stateOffset + stateIndex + 1:D3}",
                    WorkflowPhase = phaseName,
                    Groups = hasTransition
                        ? [new TransitionGroup
                        {
                            Id = $"state{stateIndex:D3}",
                            Strategy = ConcurrencyStrategy.FirstPass,
                            TransitionIds = [transitionId],
                        }]
                        : [],
                });

                if (!hasTransition)
                {
                    endNodeId = stateId;
                    continue;
                }

                var targetNodeId = stateIndex + 1 < stateCount
                    ? $"{prefix}state{stateIndex + 1:D3}"
                    : null;
                nodes.Add(transitionId, new ExpressionTransition
                {
                    Id = transitionId,
                    Name = stateIndex == stateCount - 1 ? "Continue to next phase" : "Continue",
                    WorkflowPhase = phaseName,
                    TargetNodeId = targetNodeId,
                    StepKind = WorkflowStepKind.ConditionBranch,
                    GuardExpression = new ExpressionDefinition { Source = "true" },
                    PassExpression = new ExpressionDefinition { Source = "true" },
                });
            }

            fragments.Add(WorkflowPhaseFragment.Create(workflowId, phaseId, nodes));
            phases.Add(new JsonObject
            {
                ["id"] = phaseId,
                ["prefix"] = prefix,
                ["mode"] = "phaseSliced",
                ["entries"] = new JsonArray(JsonValue.Create(entryId)),
                ["exits"] = new JsonArray(JsonValue.Create(exitId)),
                ["imports"] = new JsonArray(),
                ["exports"] = new JsonArray(),
                ["dependencies"] = phaseIndex == 0
                    ? new JsonArray()
                    : new JsonArray(JsonValue.Create($"p{phaseIndex - 1:D2}_phase")),
                ["gateIds"] = new JsonArray(),
                ["terminalRoutes"] = new JsonArray(),
            });

            if (!isLastPhase)
            {
                var nextPhaseId = $"p{phaseIndex + 1:D2}_phase";
                routes.Add(new JsonObject
                {
                    ["fromPhaseId"] = phaseId,
                    ["fromExitId"] = exitId,
                    ["toPhaseId"] = nextPhaseId,
                    ["toEntryId"] = $"{nextPhaseId}.state000",
                });
            }

            stateOffset += stateCount;
        }

        var skeletonJson = new JsonObject
        {
            ["schemaVersion"] = WorkflowPhaseSkeleton.CurrentSchemaVersion,
            ["workflowId"] = workflowId,
            ["phases"] = phases,
            ["routes"] = routes,
            ["backedges"] = new JsonArray(),
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        using var skeletonDocument = JsonDocument.Parse(skeletonJson);
        var skeleton = WorkflowPhaseSkeleton.Parse(skeletonDocument.RootElement, "phase-acceptance-skeleton.json");

        const string contractJson = """
            {
              "name": "phase-acceptance",
              "inputs": {},
              "outputs": {},
              "default_assumptions": {},
              "context_fields": {
                "schemaVersion": "loom.context-fields.v1",
                "fields": [
                  {"path":"p00_phase.acceptance","type":"string","nullable":false,"owner":"agent","description":"Acceptance fixture marker."}
                ]
              }
            }
            """;
        using var contractDocument = JsonDocument.Parse(contractJson);
        var catalog = ContextFieldCatalog.Parse(contractDocument.RootElement, "phase-acceptance-contract.json");

        return new WorkflowPhaseAcceptanceFixture(
            authoredStateCount,
            workflowId,
            contractJson,
            skeletonJson,
            skeleton,
            catalog,
            fragments,
            endNodeId);
    }

    public WorkflowInstance CreateTemplate(string assetRootPath)
    {
        var startNodeId = Skeleton.Phases[0].Entries[0];
        return new WorkflowInstance
        {
            InstanceId = WorkflowId,
            StartNodeId = startNodeId,
            CurrentNodeId = startNodeId,
            EndNodeId = EndNodeId,
            ContractBinding = new ContractBinding { Path = "contract.json", AssetRootPath = assetRootPath },
        };
    }
}
