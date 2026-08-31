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
    IReadOnlyList<string> RequiredInputs);

public sealed class WorkflowFileExecutionService
{
    private readonly WorkflowExecutionCore _core;

    public WorkflowFileExecutionService(WorkflowExecutionCore? core = null)
    {
        _core = core ?? new WorkflowExecutionCore();
    }

    public async Task<WorkflowFileExecutionResult> RunAsync(
        string workflowFile,
        Dictionary<string, object?>? contextDelta = null,
        CancellationToken ct = default)
    {
        var normalizedPath = CanonicalWorkflowFileStore.NormalizePath(workflowFile);
        await using var workflowLock = await WorkflowFileLock.AcquireAsync(normalizedPath, ct).ConfigureAwait(false);
        var instance = await CanonicalWorkflowFileStore.LoadAsync(normalizedPath, ct).ConfigureAwait(false);
        ValidatePlanContracts(instance);
        var fromStatus = instance.Status;
        ApplyContextDelta(instance, contextDelta);
        var outcome = await _core.RunUntilBoundaryAsync(instance, ct: ct).ConfigureAwait(false);
        instance.Version++;
        instance.LastActivityUtc = DateTimeOffset.UtcNow;
        await CanonicalWorkflowFileStore.SaveAsync(normalizedPath, instance, ct).ConfigureAwait(false);
        var result = CreateResult(normalizedPath, instance, outcome);
        await AppendEventAsync(result, fromStatus).ConfigureAwait(false);
        return result;
    }

    public async Task<WorkflowFileExecutionResult> ResumeAsync(
        string workflowFile,
        string transitionId,
        string? correlationKey,
        Dictionary<string, object?>? payload,
        string? resultId = null,
        CancellationToken ct = default)
    {
        var normalizedPath = CanonicalWorkflowFileStore.NormalizePath(workflowFile);
        await using var workflowLock = await WorkflowFileLock.AcquireAsync(normalizedPath, ct).ConfigureAwait(false);
        var instance = await CanonicalWorkflowFileStore.LoadAsync(normalizedPath, ct).ConfigureAwait(false);
        ValidatePlanContracts(instance);
        if (!string.IsNullOrWhiteSpace(resultId)
            && instance.Nodes.TryGetValue(transitionId, out var consumedNode)
            && consumedNode is CommandTransition { StepKind: WorkflowStepKind.Plan }
            && WorkflowExecutionCore.IsPlanResultConsumed(instance, resultId))
        {
            return CreateResult(normalizedPath, instance, EngineTickOutcome.NoProgress(instance.CurrentNodeId));
        }

        var fromStatus = instance.Status;
        await _core.ResumeAsync(instance, transitionId, correlationKey, payload, resultId, ct).ConfigureAwait(false);
        var outcome = await _core.RunUntilBoundaryAsync(instance, ct: ct).ConfigureAwait(false);
        instance.Version++;
        instance.LastActivityUtc = DateTimeOffset.UtcNow;
        await CanonicalWorkflowFileStore.SaveAsync(normalizedPath, instance, ct).ConfigureAwait(false);
        var result = CreateResult(normalizedPath, instance, outcome);
        await AppendEventAsync(result, fromStatus).ConfigureAwait(false);
        return result;
    }

    public async Task<WorkflowFileExecutionResult> GetStatusAsync(string workflowFile, CancellationToken ct = default)
    {
        var normalizedPath = CanonicalWorkflowFileStore.NormalizePath(workflowFile);
        await using var workflowLock = await WorkflowFileLock.AcquireAsync(normalizedPath, ct).ConfigureAwait(false);
        var instance = await CanonicalWorkflowFileStore.LoadAsync(normalizedPath, ct).ConfigureAwait(false);
        return CreateResult(normalizedPath, instance, EngineTickOutcome.NoProgress(instance.CurrentNodeId));
    }

    private static async Task AppendEventAsync(WorkflowFileExecutionResult result, WorkflowStatus fromStatus)
    {
        await WorkflowFileEventLog.AppendAsync(
            result.WorkflowFile,
            new WorkflowFileEventRecord(
                DateTimeOffset.UtcNow,
                "execution",
                result.WorkflowFile,
                result.Status.InstanceId,
                fromStatus.ToString(),
                result.Status.Status.ToString(),
                result.Status.CurrentNodeId,
                result.PendingTransitionId,
                result.PendingStepKind?.ToString(),
                result.Outcome.ErrorMessage)).ConfigureAwait(false);
    }

    private static void ValidatePlanContracts(WorkflowInstance instance)
    {
        var diagnostics = PlanStepContractValidator.Validate(instance);
        if (diagnostics.Count == 0)
        {
            return;
        }

        var message = string.Join(
            Environment.NewLine,
            diagnostics.Select(static diagnostic => $"[{diagnostic.Location}] {diagnostic.Message} {diagnostic.Suggestion}"));
        throw new InvalidOperationException(message);
    }

    private static void ApplyContextDelta(WorkflowInstance instance, Dictionary<string, object?>? contextDelta)
    {
        if (contextDelta is null)
        {
            return;
        }

        foreach (var pair in contextDelta)
        {
            instance.Context[pair.Key] = pair.Value;
        }
    }

    private static WorkflowFileExecutionResult CreateResult(string workflowFile, WorkflowInstance instance, EngineTickOutcome outcome)
    {
        var pendingTransitionId = instance.ActiveWaitGroups.FirstOrDefault()?.TransitionId;
        var pendingTransition = pendingTransitionId is not null && instance.Nodes.TryGetValue(pendingTransitionId, out var node)
            ? node as TransitionBase
            : null;
        var plan = pendingTransition as CommandTransition;
        var requiredInputs = plan?.Plan?.InputPaths ?? GetRequiredInputs(plan?.Command.Parameters);
        return new WorkflowFileExecutionResult(
            workflowFile,
            CanonicalWorkflowFileStore.GetEventLogPath(workflowFile),
            new WorkflowInstanceStatus(
                instance.InstanceId,
                instance.Status,
                instance.StartNodeId,
                instance.CurrentNodeId,
                instance.EndNodeId,
                instance.Version,
                instance.LastActivityUtc,
                instance.ActiveWaitGroups.Count),
            outcome,
            pendingTransitionId,
            pendingTransition?.StepKind,
            plan?.Plan?.ResultFile,
            requiredInputs);
    }

    private static IReadOnlyList<string> GetRequiredInputs(Dictionary<string, object?>? parameters)
    {
        if (parameters?.TryGetValue("requiredInputs", out var value) != true || value is not IEnumerable<object?> items)
        {
            return [];
        }

        return items.Select(Convert.ToString).Where(static item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToArray();
    }
}