using System.Text.Json;
using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Cli;

internal static class AoPromptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = WorkflowJsonSerializer.CreateDefaultOptions(indented: true);
    private const string JsonContentType = "application/json";
    private const string GuideContractBlockKind = "guide-contract";
    private const string GuideExampleBlockKind = "guide-example";
    private const string GuideTemplateBlockKind = "guide-template";

    public static string PromptTemplateVersion => "ao.workflow.prompt.v3";

    public static AoPromptBuildResult BuildPlanPromptArtifacts(string objective, IReadOnlyDictionary<string, object?> context)
    {
        var exampleInstance = BuildExampleWorkflowInstance();
        var allowedNodeKinds = GetAllowedNodeKinds();
        var allowedCommandKinds = GetAllowedCommandKinds();
        var blocks = BuildCommonBlocks(exampleInstance, allowedNodeKinds, allowedCommandKinds).ToList();
        blocks.Add(
            CreateBlock(
                blockId: "prompt.plan.task-contract",
                blockKind: GuideContractBlockKind,
                semanticRole: "task-contract",
                title: "Planner Task Contract",
                order: 120,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Required,
                content: SerializeJson(
                    new PlannerTaskContractBlockContent(
                        TaskType: "file-generation",
                        RequiresSingleJsonObject: true,
                        RequiresCompleteWorkflowInstanceFile: true,
                        RequiresViableTerminalPath: true,
                        RequiresReachableTbrPath: true,
                        RequiresConcreteWorkPath: true,
                        PreserveMeaningfulTbr: true,
                        ProhibitedResults:
                        [
                            "Do not emit commentary or markdown fences around the final answer.",
                            "Do not collapse the graph into a zero-tbr fully concrete workflow.",
                            "Do not invent node ids, targetNodeId links, or top-level WorkflowInstance fields that do not validate.",
                        ])),
                tags: ["plan", "contract", "workflow"]));
        blocks.Add(
            CreateBlock(
                blockId: "prompt.plan.runtime-context",
                blockKind: GuideTemplateBlockKind,
                semanticRole: "runtime-context",
                title: "Planner Runtime Context",
                order: 130,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Required,
                content: SerializeJson(new PlanningContextBlockContent(context)),
                tags: ["plan", "context"]));
        blocks.Add(
            CreateBlock(
                blockId: "prompt.plan.user-objective",
                blockKind: GuideTemplateBlockKind,
                semanticRole: "user-objective",
                title: "Planner User Objective",
                order: 140,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Required,
                content: SerializeJson(new UserObjectiveBlockContent(objective)),
                tags: ["plan", "objective"]));

        return new AoPromptBuildResult(BuildPlanPrompt(blocks), blocks, allowedNodeKinds, allowedCommandKinds);
    }

    public static AoPromptBuildResult BuildReplanPromptArtifacts(
        string objective,
        AoWorkflowSnapshot snapshot,
        WorkflowInstance instance,
        ToBeRefinedTransition selectedTbr,
        IReadOnlyList<string> predecessorStateIds,
        string selectedFrontierAction,
        IReadOnlyList<string> remainingTbrIds)
    {
        var exampleInstance = BuildExampleWorkflowInstance();
        var allowedNodeKinds = GetAllowedNodeKinds();
        var allowedCommandKinds = GetAllowedCommandKinds();
        var blocks = BuildCommonBlocks(exampleInstance, allowedNodeKinds, allowedCommandKinds).ToList();
        blocks.Add(
            CreateBlock(
                blockId: "prompt.replan.task-contract",
                blockKind: GuideContractBlockKind,
                semanticRole: "task-contract",
                title: "Replanner Task Contract",
                order: 120,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Required,
                content: SerializeJson(
                    new ReplannerTaskContractBlockContent(
                        TaskType: "file-modification",
                        RequiresSingleJsonObject: true,
                        RequiresModifyCurrentRuntimeInstance: true,
                        RequiresReplaceSelectedTbr: true,
                        RequiredSelectedTbrId: selectedTbr.Id,
                        RequiredPredecessorStateIds: predecessorStateIds,
                        RequiredTargetNodeId: RequireNonEmpty(selectedTbr.TargetNodeId, $"selected tbr '{selectedTbr.Id}' targetNodeId"),
                        PreserveMeaningfulTbr: true,
                        PreserveUnrelatedExistingNodes: true,
                        ProhibitedResults:
                        [
                            "Do not rewrite unrelated graph regions from scratch.",
                            "Do not remove every remaining tbr from the graph.",
                            "Do not break predecessor or target connectivity for the selected seam.",
                        ])),
                tags: ["replan", "contract", "workflow"]));
        blocks.Add(
            CreateBlock(
                blockId: "prompt.replan.runtime-context",
                blockKind: GuideTemplateBlockKind,
                semanticRole: "runtime-context",
                title: "Replanner Runtime Context",
                order: 125,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Required,
                content: SerializeJson(new PlanningContextBlockContent(snapshot.Context)),
                tags: ["replan", "context"]));
        blocks.Add(
            CreateBlock(
                blockId: "prompt.replan.blocked-boundary-context",
                blockKind: GuideTemplateBlockKind,
                semanticRole: "runtime-context",
                title: "Blocked Boundary Context",
                order: 130,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Required,
                content: SerializeJson(
                    new BlockedBoundaryContextBlockContent(
                        Status: snapshot.Status,
                        CurrentNodeId: snapshot.CurrentNodeId,
                        BoundaryReason: snapshot.LastBoundaryReason,
                        PendingRequirements: snapshot.PendingRequirements,
                        NextFrontier: snapshot.NextFrontier,
                        HumanOrAgentHint: snapshot.HumanOrAgentHint,
                        LastTransitionId: snapshot.LastTransitionId,
                        SelectedFrontierAction: selectedFrontierAction,
                        SelectedTbrId: selectedTbr.Id,
                        SelectedTbrPredecessorStateIds: predecessorStateIds,
                        SelectedTbrTargetNodeId: RequireNonEmpty(selectedTbr.TargetNodeId, $"selected tbr '{selectedTbr.Id}' targetNodeId"),
                        SelectedTbrDesignNotes: selectedTbr.DesignNotes,
                        RemainingTbrIds: remainingTbrIds)),
                tags: ["replan", "boundary", "context"]));
        blocks.Add(
            CreateBlock(
                blockId: "prompt.replan.selected-tbr-projection",
                blockKind: GuideExampleBlockKind,
                semanticRole: "selected-seam",
                title: "Selected TBR Projection",
                order: 140,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Required,
                content: SerializeJson(
                    new SelectedTbrProjectionBlockContent(
                        SelectedTbrId: selectedTbr.Id,
                        SelectedFrontierAction: selectedFrontierAction,
                        PredecessorStateIds: predecessorStateIds,
                        TargetNodeId: RequireNonEmpty(selectedTbr.TargetNodeId, $"selected tbr '{selectedTbr.Id}' targetNodeId"),
                        DesignNotes: selectedTbr.DesignNotes,
                        RemainingTbrIds: remainingTbrIds)),
                tags: ["replan", "tbr", "projection"]));
        blocks.Add(
            CreateBlock(
                blockId: "prompt.replan.current-workflow-projection",
                blockKind: GuideExampleBlockKind,
                semanticRole: "runtime-projection",
                title: "Current Workflow Projection",
                order: 150,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Required,
                content: BuildWorkflowProjection(instance),
                tags: ["replan", "workflow", "projection"]));
        blocks.Add(
            CreateBlock(
                blockId: "prompt.replan.current-workflow-instance",
                blockKind: GuideExampleBlockKind,
                semanticRole: "runtime-instance",
                title: "Current Workflow Instance",
                order: 160,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Required,
                content: WorkflowJsonSerializer.Serialize(instance, indented: true),
                tags: ["replan", "workflow", "instance"]));
        blocks.Add(
            CreateBlock(
                blockId: "prompt.replan.user-objective",
                blockKind: GuideTemplateBlockKind,
                semanticRole: "user-objective",
                title: "Replanner User Objective",
                order: 170,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Required,
                content: SerializeJson(new UserObjectiveBlockContent(objective)),
                tags: ["replan", "objective"]));

        return new AoPromptBuildResult(BuildReplanPrompt(blocks, selectedFrontierAction, selectedTbr, predecessorStateIds), blocks, allowedNodeKinds, allowedCommandKinds);
    }

    public static string BuildPlanPrompt(string objective, IReadOnlyDictionary<string, object?> context)
    {
        return BuildPlanPromptArtifacts(objective, context).Prompt;
    }

    public static string BuildReplanPrompt(
        string objective,
        AoWorkflowSnapshot snapshot,
        WorkflowInstance instance,
        ToBeRefinedTransition selectedTbr,
        IReadOnlyList<string> predecessorStateIds,
        string selectedFrontierAction,
        IReadOnlyList<string> remainingTbrIds)
    {
        return BuildReplanPromptArtifacts(objective, snapshot, instance, selectedTbr, predecessorStateIds, selectedFrontierAction, remainingTbrIds).Prompt;
    }

    private static string BuildPlanPrompt(IReadOnlyList<AoPromptBlock> blocks)
    {
        return $$"""
You are an AO workflow planner.
Generate the contents of a WorkflowInstance JSON file for the requested objective.

Return one valid WorkflowInstance JSON object only. No commentary. No wrapping markdown fences around the final answer.
Honor every `guide-contract` block below.
Treat every `guide-template` block as live runtime input.
Treat every `guide-example` block as a shape reference, not as a verbatim copy target.
When required blocks include durable runtime facts or domain decision reports, carry those decisions forward into the updated WorkflowInstance seam and preserve their stable report or payload keys for the next resume step.
Use `block_id` as the stable machine-ingestible lookup key.
When the current guide controls a decision, cite the actual `guide_path` returned by the latest successful `dotnet ao.dll --guide` JSON result. Convert that absolute runtime path to a workspace-relative or runtime-output-relative `path` before placing it in `evidence_references`; include verified output line numbers. Do not invent a synthetic guide filename or cite only guide source prose; do not list the full context pack.
When the input contains `replan_history`, preserve the blocker, ordered attempted actions and outcomes, event/audit references, terminal business objective, and prior route decisions. Select exactly one explicit replan strategy: `continue_from_current`, `rollback_to_unconfirmed`, `redesign_from_current`, `full_redesign`, or `reversible_workaround`. Return a viable path from the selected anchor to the terminal business outcome; a workaround must include a one-step rollback plan. A completion flag alone is never terminal evidence; require a non-empty `terminal_evidence` object or reference before claiming completion.
Strongly prefer `{{SerializeJson(CommandInvocationKind.PythonScript).Trim('"')}}` for multi-step calculations, text shaping, regex work, or batch data transformation.

{{RenderBlocks(blocks)}}

Return ONLY the WorkflowInstance JSON file content.
""";
    }

    private static string BuildReplanPrompt(
        IReadOnlyList<AoPromptBlock> blocks,
        string selectedFrontierAction,
        ToBeRefinedTransition selectedTbr,
        IReadOnlyList<string> predecessorStateIds)
    {
        return $$"""
You are an AO workflow replanner.
Produce an updated WorkflowInstance JSON file by modifying the current runtime workflow instance.

The most recent selected frontier action '{{selectedFrontierAction}}' did not converge.
You must expand the `tbr` node '{{selectedTbr.Id}}' into a viable replacement path.
Reconnect the replacement path from predecessor state(s) {{string.Join(", ", predecessorStateIds)}} to downstream target '{{selectedTbr.TargetNodeId}}'.
Return one valid WorkflowInstance JSON object only. No commentary. No wrapping markdown fences around the final answer.
Honor every `guide-contract` block below.
Treat every `guide-template` block as live runtime input.
Treat every `guide-example` block as a shape reference, not as a verbatim copy target.
When required blocks include durable runtime facts or domain decision reports, carry those decisions forward into the updated WorkflowInstance seam and preserve their stable report or payload keys for the next resume step.
Use `block_id` as the stable machine-ingestible lookup key.
When the current guide controls a decision, cite the actual `guide_path` returned by the latest successful `dotnet ao.dll --guide` JSON result. Convert that absolute runtime path to a workspace-relative or runtime-output-relative `path` before placing it in `evidence_references`; include verified output line numbers. Do not invent a synthetic guide filename or cite only guide source prose; do not list the full context pack.
When the input contains `replan_history`, preserve the blocker, ordered attempted actions and outcomes, event/audit references, terminal business objective, and prior route decisions. Select exactly one explicit replan strategy: `continue_from_current`, `rollback_to_unconfirmed`, `redesign_from_current`, `full_redesign`, or `reversible_workaround`. Return a viable path from the selected anchor to the terminal business outcome; a workaround must include a one-step rollback plan. A completion flag alone is never terminal evidence; require a non-empty `terminal_evidence` object or reference before claiming completion.
Strongly prefer `{{SerializeJson(CommandInvocationKind.PythonScript).Trim('"')}}` for multi-step calculations, text shaping, regex work, or batch data transformation.

{{RenderBlocks(blocks)}}

Return ONLY the updated WorkflowInstance JSON file content.
""";
    }

    private static IReadOnlyList<AoPromptBlock> BuildCommonBlocks(
        WorkflowInstance exampleInstance,
        IReadOnlyList<string> allowedNodeKinds,
        IReadOnlyList<string> allowedCommandKinds)
    {
        var startState = (StateNode)exampleInstance.Nodes["state.start"];
        var expressionTransition = (ExpressionTransition)exampleInstance.Nodes["transition.route_to_work"];
        var commandTransition = (CommandTransition)exampleInstance.Nodes["transition.local_analysis"];
        var tbrTransition = (ToBeRefinedTransition)exampleInstance.Nodes["transition.multi_source_tbr"];

        return
        [
            CreateBlock(
                blockId: "prompt.block-consumption-contract",
                blockKind: GuideContractBlockKind,
                semanticRole: "block-ingestion-contract",
                title: "Prompt Block Consumption Contract",
                order: 0,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Required,
                content: SerializeJson(
                    new PromptBlockConsumptionContractBlockContent(
                        StableLookupField: "block_id",
                        StableBlockKinds: [GuideContractBlockKind, GuideExampleBlockKind, GuideTemplateBlockKind],
                        HonorGuideContractBlocks: true,
                        TreatGuideExampleBlocksAsShapeReferences: true,
                        TreatGuideTemplateBlocksAsLiveRuntimeInputs: true,
                        RequiredBlockRule: "Every block with consumption_requirement = required must be honored when authoring or editing the WorkflowInstance result.",
                        OptionalBlockRule: "Blocks with consumption_requirement = optional are reference-only and may be skipped when they do not help the current authoring move.",
                        FinalAnswerShape: "single WorkflowInstance JSON object",
                        FinalAnswerAllowsCommentary: false,
                        FinalAnswerAllowsMarkdownFence: false)),
                tags: ["prompt", "contract", "blocks"]),
            CreateBlock(
                blockId: "workflow.output-schema",
                blockKind: GuideContractBlockKind,
                semanticRole: "schema",
                title: "Workflow Output Schema",
                order: 10,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Required,
                content: SerializeJson(BuildWorkflowSchemaContractContent()),
                tags: ["workflow", "schema"]),
            CreateBlock(
                blockId: "workflow.root-field-contract",
                blockKind: GuideContractBlockKind,
                semanticRole: "root-field-contract",
                title: "Workflow Root Field Contract",
                order: 20,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Required,
                content: SerializeJson(BuildRootFieldContractContent()),
                tags: ["workflow", "contract"]),
            CreateBlock(
                blockId: "workflow.allowed-node-kinds",
                blockKind: GuideContractBlockKind,
                semanticRole: "enum-allowlist",
                title: "Allowed Node Kinds",
                order: 30,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Required,
                content: SerializeJson(new AllowedValuesBlockContent("nodeKinds", allowedNodeKinds)),
                tags: ["workflow", "allowlist"]),
            CreateBlock(
                blockId: "workflow.allowed-command-kinds",
                blockKind: GuideContractBlockKind,
                semanticRole: "enum-allowlist",
                title: "Allowed Command Kinds",
                order: 40,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Required,
                content: SerializeJson(new AllowedValuesBlockContent("commandKinds", allowedCommandKinds)),
                tags: ["workflow", "allowlist"]),
            CreateBlock(
                blockId: "workflow.state-node-example",
                blockKind: GuideExampleBlockKind,
                semanticRole: "node-example",
                title: "State Node Example",
                order: 50,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Optional,
                content: SerializeTaskNode(startState),
                tags: ["workflow", "state", "example"]),
            CreateBlock(
                blockId: "workflow.transition-group-example",
                blockKind: GuideExampleBlockKind,
                semanticRole: "group-example",
                title: "Transition Group Example",
                order: 60,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Optional,
                content: SerializeJson(startState.Groups[0]),
                tags: ["workflow", "group", "example"]),
            CreateBlock(
                blockId: "workflow.expression-transition-example",
                blockKind: GuideExampleBlockKind,
                semanticRole: "transition-example",
                title: "Expression Transition Example",
                order: 70,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Optional,
                content: SerializeTaskNode(expressionTransition),
                tags: ["workflow", "expression", "example"]),
            CreateBlock(
                blockId: "workflow.command-transition-example",
                blockKind: GuideExampleBlockKind,
                semanticRole: "transition-example",
                title: "Command Transition Example",
                order: 80,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Optional,
                content: SerializeTaskNode(commandTransition),
                tags: ["workflow", "command", "example"]),
            CreateBlock(
                blockId: "workflow.tbr-transition-example",
                blockKind: GuideExampleBlockKind,
                semanticRole: "transition-example",
                title: "TBR Transition Example",
                order: 90,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Optional,
                content: SerializeTaskNode(tbrTransition),
                tags: ["workflow", "tbr", "example"]),
            CreateBlock(
                blockId: "workflow.example-projection",
                blockKind: GuideExampleBlockKind,
                semanticRole: "workflow-projection",
                title: "Example Workflow Projection",
                order: 100,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Optional,
                content: BuildWorkflowProjection(exampleInstance),
                tags: ["workflow", "projection", "example"]),
            CreateBlock(
                blockId: "workflow.example-instance",
                blockKind: GuideExampleBlockKind,
                semanticRole: "workflow-instance",
                title: "Example Workflow Instance",
                order: 110,
                consumptionRequirement: AoPromptBlockConsumptionRequirement.Optional,
                content: WorkflowJsonSerializer.Serialize(exampleInstance, indented: true),
                tags: ["workflow", "instance", "example"]),
        ];
    }

    private static IReadOnlyList<string> GetAllowedNodeKinds()
        =>
        [
            JsonPolymorphicConsts.StateKind,
            JsonPolymorphicConsts.CommandKind,
            JsonPolymorphicConsts.ExpressionKind,
            JsonPolymorphicConsts.ToBeRefinedKind,
        ];

    private static IReadOnlyList<string> GetAllowedCommandKinds()
        => GetAllowedEnumValues<CommandInvocationKind>();

    private static string SerializeTaskNode(ITaskNode node)
        => JsonSerializer.Serialize(node, typeof(ITaskNode), JsonOptions);

    private static string SerializeJson<TValue>(TValue value)
        => JsonSerializer.Serialize(value, JsonOptions);

    private static IReadOnlyList<string> GetAllowedEnumValues<TEnum>()
        where TEnum : struct, Enum
        => Enum.GetValues<TEnum>()
            .Select(static value => JsonSerializer.Serialize(value, JsonOptions).Trim('"'))
            .ToArray();

    private static AoPromptBlock CreateBlock(
        string blockId,
        string blockKind,
        string semanticRole,
        string title,
        int order,
        AoPromptBlockConsumptionRequirement consumptionRequirement,
        string content,
        IReadOnlyList<string>? tags = null)
        => new(
            BlockId: blockId,
            BlockKind: blockKind,
            SemanticRole: semanticRole,
            Title: title,
            ContentType: JsonContentType,
            Order: order,
            ConsumptionRequirement: consumptionRequirement,
            Content: content,
            Tags: tags);

    private static string RenderBlocks(IReadOnlyList<AoPromptBlock> blocks)
    {
        var builder = new StringBuilder();

        foreach (var block in blocks.OrderBy(static block => block.Order))
        {
            builder.AppendLine($"```{block.BlockKind}");
            builder.AppendLine($"block_id: {block.BlockId}");
            builder.AppendLine($"semantic_role: {block.SemanticRole}");
            builder.AppendLine($"title: {block.Title}");
            builder.AppendLine($"content_type: {block.ContentType}");
            builder.AppendLine($"order: {block.Order}");
            builder.AppendLine($"consumption_requirement: {SerializeJson(block.ConsumptionRequirement).Trim('"')}");
            builder.AppendLine($"tags: {JsonSerializer.Serialize(block.Tags ?? Array.Empty<string>())}");
            builder.AppendLine("---");
            builder.AppendLine(block.Content.TrimEnd());
            builder.AppendLine("```");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildWorkflowProjection(WorkflowInstance instance)
    {
        var projection = new WorkflowProjectionBlockContent(
            InstanceId: instance.InstanceId,
            StartNodeId: instance.StartNodeId,
            CurrentNodeId: instance.CurrentNodeId,
            EndNodeId: RequireNonEmpty(instance.EndNodeId, $"workflow instance '{instance.InstanceId}' endNodeId"),
            Status: instance.Status,
            Version: instance.Version,
            ContextKeys: instance.Context.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray(),
            States: instance.GetStateNodes().Values
                .OrderBy(static state => state.Id, StringComparer.Ordinal)
                .Select(state => new WorkflowStateProjection(
                    Id: state.Id,
                    Name: state.Name,
                    Description: state.Description,
                    TransitionIds: state.Groups
                        .SelectMany(static group => group.TransitionIds)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(static id => id, StringComparer.Ordinal)
                        .ToArray(),
                    WaitBehavior: state.WaitBehavior,
                    StateFailedExpression: state.StateFailedExpression))
                .ToArray(),
            Transitions: instance.GetTransitionNodes().Values
                .OrderBy(static transition => transition.Id, StringComparer.Ordinal)
                .Select(transition => CreateTransitionProjection(instance, transition))
                .ToArray());

        return SerializeJson(projection);
    }

    private static WorkflowTransitionProjection CreateTransitionProjection(WorkflowInstance instance, TransitionBase transition)
    {
        return new WorkflowTransitionProjection(
            Id: transition.Id,
            Kind: transition switch
            {
                CommandTransition => JsonPolymorphicConsts.CommandKind,
                ExpressionTransition => JsonPolymorphicConsts.ExpressionKind,
                ToBeRefinedTransition => JsonPolymorphicConsts.ToBeRefinedKind,
                _ => "unknown",
            },
            Name: transition.Name,
            Description: transition.Description,
            FromStateIds: FindPredecessorStateIds(instance, transition.Id),
            TargetNodeId: RequireNonEmpty(transition.TargetNodeId, $"transition '{transition.Id}' targetNodeId"),
            GuardExpression: transition.GuardExpression,
            SucceedExpression: transition.SucceedExpression,
            StepKind: transition.StepKind,
            Priority: transition.Priority,
            CommandKind: transition is CommandTransition commandTransition ? commandTransition.Command.Kind.ToString() : null,
            CommandName: transition is CommandTransition commandTransition2 ? commandTransition2.Command.Name : null,
            ParameterKeys: transition is CommandTransition commandTransition3
                ? commandTransition3.Command.Parameters?.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray()
                : null,
            DesignNotes: transition is ToBeRefinedTransition toBeRefinedTransition ? toBeRefinedTransition.DesignNotes : null);
    }

    private static IReadOnlyList<string> FindPredecessorStateIds(WorkflowInstance instance, string transitionId)
    {
        return instance.GetStateNodes().Values
            .Where(state => state.Groups.Any(group => group.TransitionIds.Any(id => string.Equals(id, transitionId, StringComparison.Ordinal))))
            .Select(static state => state.Id)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
    }

    private static string RequireNonEmpty(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Workflow projection requires {fieldName}.");
        }

        return value;
    }

    private static bool TryGetContextString(IReadOnlyDictionary<string, object?> context, string key, out string value)
    {
        value = string.Empty;
        if (!context.TryGetValue(key, out var raw) || raw is null)
        {
            return false;
        }

        switch (raw)
        {
            case string text when !string.IsNullOrWhiteSpace(text):
                value = text;
                return true;
            case JsonElement element when element.ValueKind == JsonValueKind.String:
                value = element.GetString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(value);
            default:
                value = raw.ToString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(value);
        }
    }

    private static WorkflowInstance BuildExampleWorkflowInstance()
    {
        var startState = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Description = "Enter the AO-authored workflow.",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.start",
                    Strategy = ConcurrencyStrategy.FirstSuccess,
                    TransitionIds = ["transition.route_to_work", "transition.fast_answer_tbr"],
                },
            ],
        };

        var routeState = new StateNode
        {
            Id = "state.route",
            Name = "Route Work",
            Description = "Perform concrete work while retaining a future refinement seam.",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.route",
                    Strategy = ConcurrencyStrategy.FirstSuccess,
                    TransitionIds = ["transition.local_analysis", "transition.multi_source_tbr"],
                },
            ],
        };

        var endState = new StateNode
        {
            Id = "state.end",
            Name = "End",
            Description = "Return the final answer or artifact.",
            Groups = [],
        };

        var routeToWork = new ExpressionTransition
        {
            Id = "transition.route_to_work",
            Name = "RouteToWork",
            Description = "Continue into the concrete work state.",
            TargetNodeId = "state.route",
            GuardExpression = "ctx.get('needsResearch') == True",
            SucceedExpression = "True",
            Priority = 0,
            StepKind = WorkflowStepKind.ConditionBranch,
        };

        var localAnalysis = new CommandTransition
        {
            Id = "transition.local_analysis",
            Name = "RunLocalAnalysis",
            Description = "Perform local analysis and complete through a concrete path.",
            TargetNodeId = "state.end",
            StepKind = WorkflowStepKind.ToolCall,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.PythonScript,
                Name = "RunLocalAnalysis",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["script"] = "topic = vars.get('topic', '')\nresult = {'summary': topic, 'ok': True}",
                    ["variables"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["topic"] = "sample",
                    },
                },
            },
            SucceedExpression = "ctx.get('transition.local_analysis_result') is not None",
        };

        var fastAnswerTbr = new ToBeRefinedTransition
        {
            Id = "transition.fast_answer_tbr",
            Name = "FastAnswerTbr",
            Description = "Alternative fast answer route.",
            TargetNodeId = "state.end",
            StepKind = WorkflowStepKind.ModelThink,
            DesignNotes = "Keep a future refinement seam for a direct high-confidence answer path that still reaches the end state.",
        };

        var multiSourceTbr = new ToBeRefinedTransition
        {
            Id = "transition.multi_source_tbr",
            Name = "MultiSourceTbr",
            Description = "Future refinement route for multi-source synthesis.",
            TargetNodeId = "state.end",
            StepKind = WorkflowStepKind.ModelThink,
            DesignNotes = "Expand later into retrieval, ranking, and synthesis while preserving a route to the end state.",
        };

        return new WorkflowInstance
        {
            InstanceId = "ao-prompt-example",
            StartNodeId = startState.Id,
            CurrentNodeId = startState.Id,
            EndNodeId = endState.Id,
            Status = WorkflowStatus.ReadyToStart,
            Version = 0,
            Context = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["topic"] = "sample",
                ["needsResearch"] = true,
            },
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [startState.Id] = startState,
                [routeState.Id] = routeState,
                [endState.Id] = endState,
                [routeToWork.Id] = routeToWork,
                [localAnalysis.Id] = localAnalysis,
                [fastAnswerTbr.Id] = fastAnswerTbr,
                [multiSourceTbr.Id] = multiSourceTbr,
            },
        };
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
        {
            return value;
        }

        return string.Create(value.Length, value, static (buffer, source) =>
        {
            buffer[0] = char.ToLowerInvariant(source[0]);
            source.AsSpan(1).CopyTo(buffer[1..]);
        });
    }

    public sealed record AoPromptBuildResult(
        string Prompt,
        IReadOnlyList<AoPromptBlock> Blocks,
        IReadOnlyList<string> AllowedNodeKinds,
        IReadOnlyList<string> AllowedCommandKinds);

    private sealed record PromptBlockConsumptionContractBlockContent(
        string StableLookupField,
        IReadOnlyList<string> StableBlockKinds,
        bool HonorGuideContractBlocks,
        bool TreatGuideExampleBlocksAsShapeReferences,
        bool TreatGuideTemplateBlocksAsLiveRuntimeInputs,
        string RequiredBlockRule,
        string OptionalBlockRule,
        string FinalAnswerShape,
        bool FinalAnswerAllowsCommentary,
        bool FinalAnswerAllowsMarkdownFence);

    private sealed record WorkflowSchemaContractBlockContent(
        IReadOnlyList<string> RootFields,
        string NodesFieldType,
        string NodeKindDiscriminatorField,
        IReadOnlyList<string> AllowedNodeKinds,
        string StateTransitionIdsPath,
        string TransitionTargetField,
        string TbrDesignNotesField,
        IReadOnlyList<string> AllowedWorkflowStatuses,
        IReadOnlyList<string> AllowedConcurrencyStrategies,
        IReadOnlyList<string> AllowedWaitBehaviors,
        IReadOnlyList<string> AllowedWorkflowStepKinds);

    private sealed record WorkflowRootFieldContractBlockContent(IReadOnlyList<WorkflowRootFieldRule> Fields);

    private sealed record WorkflowRootFieldRule(
        string FieldName,
        string SemanticRole,
        string Requirement,
        bool RequiredForNewWorkflow);

    private sealed record AllowedValuesBlockContent(string Category, IReadOnlyList<string> Values);

    private sealed record PlannerTaskContractBlockContent(
        string TaskType,
        bool RequiresSingleJsonObject,
        bool RequiresCompleteWorkflowInstanceFile,
        bool RequiresViableTerminalPath,
        bool RequiresReachableTbrPath,
        bool RequiresConcreteWorkPath,
        bool PreserveMeaningfulTbr,
        IReadOnlyList<string> ProhibitedResults);

    private sealed record ReplannerTaskContractBlockContent(
        string TaskType,
        bool RequiresSingleJsonObject,
        bool RequiresModifyCurrentRuntimeInstance,
        bool RequiresReplaceSelectedTbr,
        string RequiredSelectedTbrId,
        IReadOnlyList<string> RequiredPredecessorStateIds,
        string RequiredTargetNodeId,
        bool PreserveMeaningfulTbr,
        bool PreserveUnrelatedExistingNodes,
        IReadOnlyList<string> ProhibitedResults);

    private sealed record PlanningContextBlockContent(IReadOnlyDictionary<string, object?> Context);

    private sealed record UserObjectiveBlockContent(string Objective);

    private sealed record BlockedBoundaryContextBlockContent(
        string Status,
        string CurrentNodeId,
        string? BoundaryReason,
        IReadOnlyList<string>? PendingRequirements,
        IReadOnlyList<string>? NextFrontier,
        string? HumanOrAgentHint,
        string? LastTransitionId,
        string SelectedFrontierAction,
        string SelectedTbrId,
        IReadOnlyList<string> SelectedTbrPredecessorStateIds,
        string SelectedTbrTargetNodeId,
        string? SelectedTbrDesignNotes,
        IReadOnlyList<string> RemainingTbrIds);

    private sealed record SelectedTbrProjectionBlockContent(
        string SelectedTbrId,
        string SelectedFrontierAction,
        IReadOnlyList<string> PredecessorStateIds,
        string TargetNodeId,
        string? DesignNotes,
        IReadOnlyList<string> RemainingTbrIds);

    private sealed record SubtitleBatchDecisionReentryContractBlockContent(
        string PlanKind,
        string SelectedFrontierAction,
        IReadOnlyList<string> PresentRuntimeKeys,
        IReadOnlyList<string> StableReportKeys,
        IReadOnlyList<string> SupportedAoDecisionSurfaces,
        IReadOnlyList<string> RequiredResumePayloadKeys,
        IReadOnlyList<string> RequiredWorkflowEditRules,
        IReadOnlyList<string> ProhibitedResults);

    private sealed record WorkflowProjectionBlockContent(
        string InstanceId,
        string StartNodeId,
        string CurrentNodeId,
        string EndNodeId,
        WorkflowStatus Status,
        int Version,
        IReadOnlyList<string> ContextKeys,
        IReadOnlyList<WorkflowStateProjection> States,
        IReadOnlyList<WorkflowTransitionProjection> Transitions);

    private sealed record WorkflowStateProjection(
        string Id,
        string? Name,
        string? Description,
        IReadOnlyList<string> TransitionIds,
        WaitBehavior WaitBehavior,
        string? StateFailedExpression);

    private sealed record WorkflowTransitionProjection(
        string Id,
        string Kind,
        string? Name,
        string? Description,
        IReadOnlyList<string> FromStateIds,
        string TargetNodeId,
        string? GuardExpression,
        string? SucceedExpression,
        WorkflowStepKind StepKind,
        int Priority,
        string? CommandKind,
        string? CommandName,
        IReadOnlyList<string>? ParameterKeys,
        string? DesignNotes);

    private static WorkflowSchemaContractBlockContent BuildWorkflowSchemaContractContent()
    {
        return new WorkflowSchemaContractBlockContent(
            RootFields: typeof(WorkflowInstance).GetProperties()
                .Where(static property => property.Name is not nameof(WorkflowInstance.TemplateKind) and not nameof(WorkflowInstance.Validation))
                .Select(static property => ToCamelCase(property.Name))
                .ToArray(),
            NodesFieldType: "dictionary keyed by node id",
            NodeKindDiscriminatorField: JsonPolymorphicConsts.TypeDiscriminatorPropertyName,
            AllowedNodeKinds: GetAllowedNodeKinds(),
            StateTransitionIdsPath: "state.groups[*].transitionIds",
            TransitionTargetField: "targetNodeId",
            TbrDesignNotesField: "designNotes",
            AllowedWorkflowStatuses: GetAllowedEnumValues<WorkflowStatus>(),
            AllowedConcurrencyStrategies: GetAllowedEnumValues<ConcurrencyStrategy>(),
            AllowedWaitBehaviors: GetAllowedEnumValues<WaitBehavior>(),
            AllowedWorkflowStepKinds: GetAllowedEnumValues<WorkflowStepKind>());
    }

    private static WorkflowRootFieldContractBlockContent BuildRootFieldContractContent()
    {
        return new WorkflowRootFieldContractBlockContent(
        [
            new WorkflowRootFieldRule("instanceId", "caller-managed workflow identifier", "Keep stable for the generated or modified file.", true),
            new WorkflowRootFieldRule("nodes", "node dictionary", "Serialize every node with the polymorphic kind discriminator and stable ids.", true),
            new WorkflowRootFieldRule("startNodeId", "entry state", "Point to the first state node for a new workflow.", true),
            new WorkflowRootFieldRule("currentNodeId", "current execution state", "For new files this usually matches startNodeId; for replans it must reflect the current runtime seam.", true),
            new WorkflowRootFieldRule("endNodeId", "terminal state", "Point to the intended completion state.", true),
            new WorkflowRootFieldRule("status", "workflow status", "Use only allowed workflow status values.", true),
            new WorkflowRootFieldRule("context", "durable runtime facts", "Persist planning or execution facts the caller needs across steps.", true),
            new WorkflowRootFieldRule("history", "execution history", "Can be empty for a new authored file and should remain coherent for a modified runtime file.", true),
            new WorkflowRootFieldRule("version", "revision counter", "Keep as an integer revision field.", true),
            new WorkflowRootFieldRule("activeWaitGroups", "wait metadata", "Populate only when the workflow genuinely requires wait/resume coordination.", true),
        ]);
    }
}