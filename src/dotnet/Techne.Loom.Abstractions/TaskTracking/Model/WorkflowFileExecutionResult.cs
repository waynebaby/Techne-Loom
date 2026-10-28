using System.Text;
using System.Text.Json.Serialization;
using Techne.Loom.Abstractions.TaskTracking;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Abstractions.TaskTracking.Runtime;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record WorkflowFileExecutionResult(
    string WorkflowFile,
    string EventLogFile,
    WorkflowInstanceStatus Status,
    EngineTickOutcome Outcome,
    string? PendingTransitionId,
    WorkflowStepKind? PendingStepKind,
    string? ResultFile,
    IReadOnlyList<string> RequiredInputs)
{
    [JsonIgnore]
    public IReadOnlyList<AskScopedWorkerEndpoint> AskUserEndpoints { get; init; } = [];

    [JsonPropertyName("context_fields")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ContextFieldWeaveOutField>? ContextFields { get; init; }

    [JsonPropertyName("context_field_warnings")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? ContextFieldWarnings { get; init; }
}
