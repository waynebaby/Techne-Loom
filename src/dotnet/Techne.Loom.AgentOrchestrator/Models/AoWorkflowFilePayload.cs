using System.Text.Json.Serialization;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Models;

public sealed record AoWorkflowFilePayload(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("workflow_file")] string WorkflowFile,
    [property: JsonPropertyName("event_log_file")] string EventLogFile,
    [property: JsonPropertyName("instance_id")] string InstanceId,
    [property: JsonPropertyName("current_node_id")] string CurrentNodeId,
    [property: JsonPropertyName("current_step_kind")] string? CurrentStepKind = null,
    [property: JsonPropertyName("transition_id")] string? TransitionId = null,
    [property: JsonPropertyName("result_file")] string? ResultFile = null,
    [property: JsonPropertyName("required_inputs")] IReadOnlyList<string>? RequiredInputs = null,
    [property: JsonPropertyName("summary")] WorkflowFragmentSummary? Summary = null,
    [property: JsonPropertyName("error")] string? Error = null);