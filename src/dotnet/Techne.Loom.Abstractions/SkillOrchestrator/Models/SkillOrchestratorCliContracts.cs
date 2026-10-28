using System.Text.Json.Serialization;
using Techne.Loom.Abstractions.Ask;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Models;

public sealed record SoPropertyEnvelope(string Type, DateTimeOffset TimestampUtc, object Payload);

public sealed record SkillResumeEnvelope(
    [property: JsonPropertyName("transition_id")] string TransitionId,
    [property: JsonPropertyName("correlation_key")] string? CorrelationKey,
    [property: JsonPropertyName("payload")] Dictionary<string, object?>? Payload,
    [property: JsonPropertyName("result_id")] string? ResultId = null,
    [property: JsonPropertyName("execution_error")] string? ExecutionError = null,
    [property: JsonPropertyName("wait_id")] string? WaitId = null);

public sealed record SkillStatusPayload(
    [property: JsonPropertyName("workflow_file")] string WorkflowFile,
    [property: JsonPropertyName("instance_id")] string InstanceId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("current_node_id")] string? CurrentNodeId,
    [property: JsonPropertyName("next_node_id")] string? NextNodeId,
    [property: JsonPropertyName("event_log_file")] string EventLogFile,
    [property: JsonPropertyName("must_show_to_user_files")] IReadOnlyList<string> MustShowToUserFiles,
    [property: JsonPropertyName("workflow_location_summary")] string WorkflowLocationSummary,
    [property: JsonPropertyName("can_resume")] bool CanResume = false,
    [property: JsonPropertyName("fresh_instance_required")] bool FreshInstanceRequired = false,
    [property: JsonPropertyName("case_id")] string? CaseId = null,
    [property: JsonPropertyName("run_id")] string? RunId = null,
    [property: JsonPropertyName("fanout_cancellation_signals")] IReadOnlyList<FanoutCancellationSignal>? FanoutCancellationSignals = null);

public sealed record SkillProgressPayload(
    [property: JsonPropertyName("workflow_file")] string WorkflowFile,
    [property: JsonPropertyName("instance_id")] string InstanceId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("current_node_id")] string? CurrentNodeId,
    [property: JsonPropertyName("next_node_id")] string? NextNodeId,
    [property: JsonPropertyName("event_log_file")] string EventLogFile,
    [property: JsonPropertyName("must_show_to_user_files")] IReadOnlyList<string> MustShowToUserFiles,
    [property: JsonPropertyName("workflow_location_summary")] string WorkflowLocationSummary,
    [property: JsonPropertyName("audit_artifacts")] WorkflowAuditArtifacts AuditArtifacts,
    [property: JsonPropertyName("can_resume")] bool CanResume = false,
    [property: JsonPropertyName("fresh_instance_required")] bool FreshInstanceRequired = false,
    [property: JsonPropertyName("case_id")] string? CaseId = null,
    [property: JsonPropertyName("run_id")] string? RunId = null,
    [property: JsonPropertyName("fanout_cancellation_signals")] IReadOnlyList<FanoutCancellationSignal>? FanoutCancellationSignals = null);

public sealed record SkillAskUserPayload(
    [property: JsonPropertyName("workflow_file")] string WorkflowFile,
    [property: JsonPropertyName("instance_id")] string InstanceId,
    [property: JsonPropertyName("endpoints")] IReadOnlyList<AskScopedWorkerEndpoint> Endpoints);

public sealed record SkillBoundaryPayload(
    [property: JsonPropertyName("workflow_file")] string WorkflowFile,
    [property: JsonPropertyName("instance_id")] string InstanceId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("current_node_id")] string CurrentNodeId,
    [property: JsonPropertyName("current_step_kind")] string? CurrentStepKind,
    [property: JsonPropertyName("skill_hint")] string? SkillHint,
    [property: JsonPropertyName("memory_for_next_step")] object MemoryForNextStep,
    [property: JsonPropertyName("required_inputs")] IReadOnlyList<string> RequiredInputs,
    [property: JsonPropertyName("event_log_file")] string EventLogFile,
    [property: JsonPropertyName("must_show_to_user_files")] IReadOnlyList<string> MustShowToUserFiles,
    [property: JsonPropertyName("workflow_location_summary")] string WorkflowLocationSummary,
    [property: JsonPropertyName("audit_artifacts")] WorkflowAuditArtifacts? AuditArtifacts,
    [property: JsonPropertyName("can_resume")] bool CanResume = true,
    [property: JsonPropertyName("fresh_instance_required")] bool FreshInstanceRequired = false,
    [property: JsonPropertyName("gate_evaluation")] GateEvaluationResult? GateEvaluation = null,
    [property: JsonPropertyName("projection_contract")] IReadOnlyDictionary<string, object?>? ProjectionContract = null,
    [property: JsonPropertyName("case_id")] string? CaseId = null,
    [property: JsonPropertyName("run_id")] string? RunId = null,
    [property: JsonPropertyName("fanout_cancellation_signals")] IReadOnlyList<FanoutCancellationSignal>? FanoutCancellationSignals = null,
    [property: JsonPropertyName("context_fields")] IReadOnlyList<ContextFieldWeaveOutField>? ContextFields = null,
    [property: JsonPropertyName("context_field_warnings")] IReadOnlyList<string>? ContextFieldWarnings = null);

public sealed record SkillResultPayload(
    [property: JsonPropertyName("workflow_file")] string WorkflowFile,
    [property: JsonPropertyName("instance_id")] string InstanceId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("current_node_id")] string CurrentNodeId,
    [property: JsonPropertyName("context")] IReadOnlyDictionary<string, object?> Context,
    [property: JsonPropertyName("event_log_file")] string EventLogFile,
    [property: JsonPropertyName("must_show_to_user_files")] IReadOnlyList<string> MustShowToUserFiles,
    [property: JsonPropertyName("workflow_location_summary")] string WorkflowLocationSummary,
    [property: JsonPropertyName("audit_artifacts")] WorkflowAuditArtifacts? AuditArtifacts,
    [property: JsonPropertyName("can_resume")] bool CanResume = false,
    [property: JsonPropertyName("fresh_instance_required")] bool FreshInstanceRequired = false,
    [property: JsonPropertyName("case_id")] string? CaseId = null,
    [property: JsonPropertyName("run_id")] string? RunId = null,
    [property: JsonPropertyName("fanout_cancellation_signals")] IReadOnlyList<FanoutCancellationSignal>? FanoutCancellationSignals = null,
    [property: JsonPropertyName("context_field_warnings")] IReadOnlyList<string>? ContextFieldWarnings = null);

public sealed record SkillErrorPayload(
    [property: JsonPropertyName("workflow_file")] string WorkflowFile,
    [property: JsonPropertyName("instance_id")] string InstanceId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("event_log_file")] string EventLogFile,
    [property: JsonPropertyName("must_show_to_user_files")] IReadOnlyList<string> MustShowToUserFiles,
    [property: JsonPropertyName("workflow_location_summary")] string WorkflowLocationSummary,
    [property: JsonPropertyName("audit_artifacts")] WorkflowAuditArtifacts? AuditArtifacts,
    [property: JsonPropertyName("can_resume")] bool CanResume = false,
    [property: JsonPropertyName("fresh_instance_required")] bool FreshInstanceRequired = false,
    [property: JsonPropertyName("gate_evaluation")] GateEvaluationResult? GateEvaluation = null,
    [property: JsonPropertyName("projection_contract")] IReadOnlyDictionary<string, object?>? ProjectionContract = null,
    [property: JsonPropertyName("current_node_id")] string? CurrentNodeId = null,
    [property: JsonPropertyName("case_id")] string? CaseId = null,
    [property: JsonPropertyName("run_id")] string? RunId = null,
    [property: JsonPropertyName("context_field_warnings")] IReadOnlyList<string>? ContextFieldWarnings = null,
    [property: JsonPropertyName("compile_feedback")] WorkflowCompileFeedback? CompileFeedback = null,
    [property: JsonPropertyName("fanout_cancellation_signals")] IReadOnlyList<FanoutCancellationSignal>? FanoutCancellationSignals = null);
