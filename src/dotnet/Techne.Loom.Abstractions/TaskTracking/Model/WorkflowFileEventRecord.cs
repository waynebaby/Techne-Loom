using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record WorkflowFileEventRecord(
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("event_type")] string EventType,
    [property: JsonPropertyName("workflow_file")] string WorkflowFile,
    [property: JsonPropertyName("instance_id")] string InstanceId,
    [property: JsonPropertyName("from_status")] string? FromStatus,
    [property: JsonPropertyName("to_status")] string ToStatus,
    [property: JsonPropertyName("current_node_id")] string CurrentNodeId,
    [property: JsonPropertyName("transition_id")] string? TransitionId,
    [property: JsonPropertyName("step_kind")] string? StepKind,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("operation_id")] string? OperationId = null);
