using System.Text.Json.Serialization;

namespace Techne.Loom.AgentOrchestrator.Models;

public sealed record AoPromptPayload(
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("prompt_kind")] string PromptKind,
    [property: JsonPropertyName("prompt_template_version")] string PromptTemplateVersion,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("blocks")] IReadOnlyList<AoPromptBlock>? Blocks = null,
    [property: JsonPropertyName("allowed_node_kinds")] IReadOnlyList<string>? AllowedNodeKinds = null,
    [property: JsonPropertyName("allowed_command_kinds")] IReadOnlyList<string>? AllowedCommandKinds = null,
    [property: JsonPropertyName("objective_file")] string? ObjectiveFile = null,
    [property: JsonPropertyName("context_file")] string? ContextFile = null,
    [property: JsonPropertyName("session_id")] string? SessionId = null,
    [property: JsonPropertyName("workflow_file")] string? WorkflowFile = null,
    [property: JsonPropertyName("workflow_instance_file")] string? WorkflowInstanceFile = null,
    [property: JsonPropertyName("boundary_reason")] string? BoundaryReason = null,
    [property: JsonPropertyName("pending_requirements")] IReadOnlyList<string>? PendingRequirements = null,
    [property: JsonPropertyName("next_frontier")] IReadOnlyList<string>? NextFrontier = null,
    [property: JsonPropertyName("human_or_agent_hint")] string? HumanOrAgentHint = null,
    [property: JsonPropertyName("last_transition_id")] string? LastTransitionId = null,
    [property: JsonPropertyName("selected_frontier_action")] string? SelectedFrontierAction = null,
    [property: JsonPropertyName("selected_tbr_id")] string? SelectedTbrId = null,
    [property: JsonPropertyName("selected_tbr_predecessor_state_ids")] IReadOnlyList<string>? SelectedTbrPredecessorStateIds = null,
    [property: JsonPropertyName("selected_tbr_target_node_id")] string? SelectedTbrTargetNodeId = null,
    [property: JsonPropertyName("selected_tbr_design_notes")] string? SelectedTbrDesignNotes = null,
    [property: JsonPropertyName("remaining_tbr_ids")] IReadOnlyList<string>? RemainingTbrIds = null,
    [property: JsonPropertyName("requires_terminal_tbr_path")] bool? RequiresTerminalTbrPath = null);