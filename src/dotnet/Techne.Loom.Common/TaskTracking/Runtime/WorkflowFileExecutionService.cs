using System.Text;
using System.Text.Json.Serialization;
using Techne.Loom.Abstractions.TaskTracking;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Abstractions.TaskTracking.Runtime;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class WorkflowFileExecutionService
{
    private readonly WorkflowExecutionCore _core;
    private readonly Func<WorkflowInstance, IReadOnlySet<string>, CancellationToken, Task<IReadOnlyList<AskScopedWorkerEndpoint>>> _startAskWorkers;

    public WorkflowFileExecutionService(WorkflowExecutionCore? core = null)
        : this(core, StartAskWorkersForNewWaitsAsync)
    {
    }

    internal WorkflowFileExecutionService(
        WorkflowExecutionCore? core,
        Func<WorkflowInstance, IReadOnlySet<string>, CancellationToken, Task<IReadOnlyList<AskScopedWorkerEndpoint>>> startAskWorkers)
    {
        _core = core ?? new WorkflowExecutionCore();
        _startAskWorkers = startAskWorkers ?? throw new ArgumentNullException(nameof(startAskWorkers));
    }

    public async Task<WorkflowFileExecutionResult> RunAsync(
        string workflowFile,
        Dictionary<string, object?>? contextDelta = null,
        CancellationToken ct = default,
        string? operationId = null)
    {
        if (operationId is not null)
        {
            WorkflowOperationLedger.ValidateOperationId(operationId);
        }

        var normalizedPath = CanonicalWorkflowFileStore.NormalizePath(workflowFile);
        await using var workflowLock = await WorkflowFileLock.AcquireAsync(normalizedPath, ct).ConfigureAwait(false);
        var instance = await CanonicalWorkflowFileStore.LoadAsync(normalizedPath, ct).ConfigureAwait(false);
        var contextFieldCatalog = (await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(instance, ct: ct).ConfigureAwait(false)).Catalog;
        ContextFieldRuntimeValidator.ValidateWorkflowDefinition(instance, contextFieldCatalog, normalizedPath);
        contextFieldCatalog.ValidateContextValues(instance.Context, normalizedPath);
        ValidateContextDelta(contextFieldCatalog, instance.Context, contextDelta, normalizedPath);
        var requestHash = operationId is null ? null : WorkflowOperationLedger.ComputeRequestHash(new { context = contextDelta });
        if (operationId is not null)
        {
            var previous = await WorkflowOperationLedger.BeginIfNewAsync(
                normalizedPath,
                operationId,
                "run",
                requestHash!,
                beforeStart: () => ValidatePlanContracts(instance),
                ct: ct).ConfigureAwait(false);
            if (previous is not null) return await AddAskUserEndpointsAsync(previous, instance, new HashSet<string>(StringComparer.Ordinal), ct).ConfigureAwait(false);
        }
        else
        {
            ValidatePlanContracts(instance);
        }
        var fromStatus = instance.Status;
        var previousAskWaitIds = GetActiveAskWaitIds(instance);
        ApplyContextDelta(instance, contextDelta);
        var outcome = await _core.RunUntilBoundaryAsync(instance, ct: ct).ConfigureAwait(false);
        contextFieldCatalog.ValidateContextValues(instance.Context, normalizedPath);
        instance.Version++;
        instance.LastActivityUtc = DateTimeOffset.UtcNow;
        await CanonicalWorkflowFileStore.SaveAsync(normalizedPath, instance, ct).ConfigureAwait(false);
        var result = CreateResult(normalizedPath, instance, outcome, contextFieldCatalog);
        await AppendEventAsync(result, fromStatus, operationId).ConfigureAwait(false);
        if (operationId is not null)
        {
            await WorkflowOperationLedger.CompleteAsync(normalizedPath, operationId, "run", requestHash!, result, ct).ConfigureAwait(false);
        }
        return await AddAskUserEndpointsAsync(result, instance, previousAskWaitIds, ct).ConfigureAwait(false);
    }

    public Task<WorkflowFileExecutionResult> ResumeAsync(
        string workflowFile,
        string transitionId,
        string? correlationKey,
        Dictionary<string, object?>? payload,
        string? resultId = null,
        CancellationToken ct = default,
        string? operationId = null,
        string? executionError = null,
        string? waitId = null)
    {
        if (operationId is not null)
        {
            WorkflowOperationLedger.ValidateOperationId(operationId);
        }

        return ResumeAsyncCore(
            workflowFile,
            transitionId,
            correlationKey,
            payload,
            resultId,
            operationId,
            askRequestResolver: null,
            askStore: null,
            ct: ct,
            executionError: executionError,
            waitId: waitId);
    }

    public Task<WorkflowFileExecutionResult> ResumeFromAskUserReceiptAsync(
        string workflowFile,
        string askId,
        CancellationToken ct = default)
        => ResumeFromAskUserReceiptAsync(workflowFile, askId, new AskScopedSubmissionStore(), ct);

    internal Task<WorkflowFileExecutionResult> ResumeFromAskUserReceiptAsync(
        string workflowFile,
        string askId,
        AskScopedSubmissionStore store,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(askId);
        ArgumentNullException.ThrowIfNull(store);
        return ResumeAsyncCore(
            workflowFile,
            transitionId: null,
            correlationKey: null,
            payload: null,
            resultId: null,
            operationId: null,
            askRequestResolver: (_, token) => AskScopedSubmissionWorkflow.GetSubmittedReceiptAsync(store, askId, token),
            askStore: store,
            ct: ct);
    }

    public Task<WorkflowFileExecutionResult> ResumeFromAskUserOfflineSubmissionAsync(
        string workflowFile,
        AskScopedOfflineSubmission submission,
        CancellationToken ct = default)
        => ResumeFromAskUserOfflineSubmissionAsync(workflowFile, submission, new AskScopedSubmissionStore(), ct);

    internal Task<WorkflowFileExecutionResult> ResumeFromAskUserOfflineSubmissionAsync(
        string workflowFile,
        AskScopedOfflineSubmission submission,
        AskScopedSubmissionStore store,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(store);
        return ResumeAsyncCore(
            workflowFile,
            transitionId: null,
            correlationKey: null,
            payload: null,
            resultId: null,
            operationId: null,
            askRequestResolver: (instance, token) => AskScopedSubmissionWorkflow.SubmitOfflineAnswersAsync(instance, store, submission, token),
            askStore: store,
            ct: ct);
    }

    private async Task<WorkflowFileExecutionResult> ResumeAsyncCore(
        string workflowFile,
        string? transitionId,
        string? correlationKey,
        Dictionary<string, object?>? payload,
        string? resultId,
        string? operationId,
        Func<WorkflowInstance, CancellationToken, Task<AskScopedResumeRequest>>? askRequestResolver,
        AskScopedSubmissionStore? askStore,
        CancellationToken ct,
        string? executionError = null,
        string? waitId = null)
    {
        var normalizedPath = CanonicalWorkflowFileStore.NormalizePath(workflowFile);
        await using var workflowLock = await WorkflowFileLock.AcquireAsync(normalizedPath, ct).ConfigureAwait(false);
        var instance = await CanonicalWorkflowFileStore.LoadAsync(normalizedPath, ct).ConfigureAwait(false);
        ValidateUserInputContracts(instance);
        var contextFieldCatalog = (await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(instance, ct: ct).ConfigureAwait(false)).Catalog;
        ContextFieldRuntimeValidator.ValidateWorkflowDefinition(instance, contextFieldCatalog, normalizedPath);
        contextFieldCatalog.ValidateContextValues(instance.Context, normalizedPath);
        var askRequest = askRequestResolver is null
            ? null
            : await askRequestResolver(instance, ct).ConfigureAwait(false);
        if (askRequest is not null)
        {
            if (!string.Equals(askRequest.WorkflowInstanceId, instance.InstanceId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The AskUser receipt belongs to a different workflow instance.");
            }

            transitionId = askRequest.TransitionId;
            correlationKey = askRequest.CorrelationKey;
            payload = askRequest.Payload;
            resultId = null;
            operationId = askRequest.OperationId;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(transitionId);
        var originalPayload = payload;
        var admission = ValidateResumeProjection(instance, contextFieldCatalog, transitionId, payload, normalizedPath, executionError);
        payload = admission.Payload;
        if (operationId is not null)
        {
            WorkflowOperationLedger.ValidateOperationId(operationId);
        }
        var requestHash = operationId is null
            ? null
            : WorkflowOperationLedger.ComputeRequestHash(new { transitionId, correlationKey, payload = originalPayload, resultId, executionError, waitId });
        if (operationId is not null)
        {
            var previous = await WorkflowOperationLedger.BeginIfNewAsync(
                normalizedPath,
                operationId,
                "resume",
                requestHash!,
                beforeStart: () => ValidateResumeAdmission(instance, transitionId, askRequest),
                ct: ct).ConfigureAwait(false);
            if (previous is not null)
            {
                if (askRequest is not null)
                {
                    await (askStore ?? throw new InvalidOperationException("The AskUser submission store is unavailable."))
                        .MarkAppliedAsync(askRequest.Launch.AskId, askRequest.Launch.MachineCapability, askRequest.ReceiptGeneration, ct)
                        .ConfigureAwait(false);
                }

                return await AddAskUserEndpointsAsync(previous, instance, new HashSet<string>(StringComparer.Ordinal), ct).ConfigureAwait(false);
            }
        }
        else
        {
            ValidateResumeAdmission(instance, transitionId, askRequest);
        }

        var existingAskWaitIds = GetActiveAskWaitIds(instance);
        if (!string.IsNullOrWhiteSpace(resultId)
            && instance.Nodes.TryGetValue(transitionId, out var consumedNode)
            && consumedNode is CommandTransition { StepKind: WorkflowStepKind.Plan }
            && WorkflowExecutionCore.IsPlanResultConsumed(instance, resultId))
        {
            var duplicateResult = CreateResult(normalizedPath, instance, EngineTickOutcome.NoProgress(instance.CurrentNodeId), contextFieldCatalog, admission.Warnings);
            if (operationId is not null)
            {
                await WorkflowOperationLedger.CompleteAsync(normalizedPath, operationId, "resume", requestHash!, duplicateResult, ct).ConfigureAwait(false);
            }
            return await AddAskUserEndpointsAsync(duplicateResult, instance, existingAskWaitIds, ct).ConfigureAwait(false);
        }

        if (WorkflowExecutionCore.IsLateFanoutResultIgnored(instance, transitionId!, waitId))
        {
            var ignoredResult = CreateResult(normalizedPath, instance, EngineTickOutcome.NoProgress(instance.CurrentNodeId), contextFieldCatalog, admission.Warnings);
            if (operationId is not null)
            {
                await WorkflowOperationLedger.CompleteAsync(normalizedPath, operationId, "resume", requestHash!, ignoredResult, ct).ConfigureAwait(false);
            }

            return await AddAskUserEndpointsAsync(ignoredResult, instance, existingAskWaitIds, ct).ConfigureAwait(false);
        }

        var fromStatus = instance.Status;
        await _core.ResumeAsync(instance, transitionId, correlationKey, payload, resultId, ct, executionError, waitId).ConfigureAwait(false);
        var outcome = await _core.RunUntilBoundaryAsync(instance, ct: ct).ConfigureAwait(false);
        contextFieldCatalog.ValidateContextValues(instance.Context, normalizedPath);
        instance.Version++;
        instance.LastActivityUtc = DateTimeOffset.UtcNow;
        await CanonicalWorkflowFileStore.SaveAsync(normalizedPath, instance, ct).ConfigureAwait(false);
        var result = CreateResult(normalizedPath, instance, outcome, contextFieldCatalog, admission.Warnings);
        await AppendEventAsync(result, fromStatus, operationId).ConfigureAwait(false);
        if (operationId is not null)
        {
            await WorkflowOperationLedger.CompleteAsync(normalizedPath, operationId, "resume", requestHash!, result, ct).ConfigureAwait(false);
        }
        if (askRequest is not null)
        {
            await (askStore ?? throw new InvalidOperationException("The AskUser submission store is unavailable."))
                .MarkAppliedAsync(askRequest.Launch.AskId, askRequest.Launch.MachineCapability, askRequest.ReceiptGeneration, ct)
                .ConfigureAwait(false);
        }

        return await AddAskUserEndpointsAsync(result, instance, existingAskWaitIds, ct).ConfigureAwait(false);
    }

    public async Task<WorkflowFileExecutionResult> GetStatusAsync(string workflowFile, CancellationToken ct = default)
    {
        var normalizedPath = CanonicalWorkflowFileStore.NormalizePath(workflowFile);
        await using var workflowLock = await WorkflowFileLock.AcquireAsync(normalizedPath, ct).ConfigureAwait(false);
        var instance = await CanonicalWorkflowFileStore.LoadAsync(normalizedPath, ct).ConfigureAwait(false);
        var contextFieldCatalog = (await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(instance, ct: ct).ConfigureAwait(false)).Catalog;
        contextFieldCatalog.ValidateContextValues(instance.Context, normalizedPath);
        return CreateResult(normalizedPath, instance, EngineTickOutcome.NoProgress(instance.CurrentNodeId), contextFieldCatalog);
    }

    private static async Task AppendEventAsync(WorkflowFileExecutionResult result, WorkflowStatus fromStatus, string? operationId = null)
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
                result.Outcome.ErrorMessage,
                operationId)).ConfigureAwait(false);
    }

    private static void ValidateResumeAdmission(
        WorkflowInstance instance,
        string? transitionId,
        AskScopedResumeRequest? askRequest)
    {
        ValidatePlanContracts(instance);
        if (askRequest is null)
        {
            AskScopedSubmissionWorkflow.ValidateNoUnvalidatedAskUserResume(instance, transitionId);
            return;
        }

        AskScopedSubmissionWorkflow.ValidateActiveWait(instance, askRequest);
    }

    private static void ValidateUserInputContracts(WorkflowInstance instance)
    {
        var diagnostics = UserInputContractValidator.Validate(instance);
        if (diagnostics.Count == 0)
        {
            return;
        }

        var message = string.Join(
            Environment.NewLine,
            diagnostics.Select(static diagnostic => $"[{diagnostic.Location}] {diagnostic.Message} {diagnostic.Suggestion}"));
        throw new InvalidOperationException(message);
    }

    private static void ValidateContextDelta(
        ContextFieldCatalog catalog,
        IReadOnlyDictionary<string, object?> context,
        Dictionary<string, object?>? contextDelta,
        string sourcePath)
        => ContextFieldRuntimeValidator.ValidateContextDelta(catalog, context, contextDelta, sourcePath);

    private static ContextFieldResumeAdmissionResult ValidateResumeProjection(
        WorkflowInstance instance,
        ContextFieldCatalog catalog,
        string transitionId,
        Dictionary<string, object?>? payload,
        string sourcePath,
        string? executionError)
        => ContextFieldRuntimeValidator.ValidateResumeProjection(instance, catalog, transitionId, payload, sourcePath, executionError);

    private static void ValidatePlanContracts(WorkflowInstance instance)
    {
        var diagnostics = PlanStepContractValidator.Validate(instance)
            .Select(static diagnostic => (diagnostic.Location, diagnostic.Message, diagnostic.Suggestion))
            .Concat(UserInputContractValidator.Validate(instance)
                .Select(static diagnostic => (diagnostic.Location, diagnostic.Message, diagnostic.Suggestion)))
            .ToArray();
        if (diagnostics.Length == 0)
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

    private async Task<WorkflowFileExecutionResult> AddAskUserEndpointsAsync(
        WorkflowFileExecutionResult result,
        WorkflowInstance instance,
        IReadOnlySet<string> existingWaitIds,
        CancellationToken ct)
    {
        if (AskScopedSubmissionWorkflow.GetActiveStructuredWaitGroups(instance).Count == 0)
        {
            return result;
        }

        var endpoints = await _startAskWorkers(instance, existingWaitIds, ct).ConfigureAwait(false);
        return result with { AskUserEndpoints = endpoints };
    }

    private static HashSet<string> GetActiveAskWaitIds(WorkflowInstance instance)
    {
        var waitIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var waitGroup in AskScopedSubmissionWorkflow.GetActiveStructuredWaitGroups(instance))
        {
            if (waitGroup.GetNextPendingEntry() is { } pendingEntry)
            {
                waitIds.Add(pendingEntry.WaitId);
            }
        }

        return waitIds;
    }

    private static Task<IReadOnlyList<AskScopedWorkerEndpoint>> StartAskWorkersForNewWaitsAsync(
        WorkflowInstance instance,
        IReadOnlySet<string> existingWaitIds,
        CancellationToken ct)
    {
        if (AskScopedSubmissionWorkflow.GetActiveStructuredWaitGroups(instance).Count == 0)
        {
            return Task.FromResult<IReadOnlyList<AskScopedWorkerEndpoint>>([]);
        }

        var store = new AskScopedSubmissionStore();
        return AskScopedSubmissionWorkflow.StartWorkersForActiveWaitsAsync(
            instance,
            store,
            AskScopedWorkerProcessLauncher.StartDetachedAsync,
            ct,
            existingWaitIds);
    }

    private static WorkflowFileExecutionResult CreateResult(string workflowFile, WorkflowInstance instance, EngineTickOutcome outcome, ContextFieldCatalog contextFieldCatalog, IReadOnlyList<string>? contextFieldWarnings = null)
    {
        var nextPendingWaitGroup = instance.ActiveWaitGroups.FirstOrDefault(static waitGroup => waitGroup.GetNextPendingEntry() is not null);
        var pendingTransitionId = nextPendingWaitGroup?.TransitionId;
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
                instance.ActiveWaitGroups.Count,
                instance.FanoutCancellationSignals.ToArray()),
            outcome,
            pendingTransitionId,
            pendingTransition?.StepKind,
            plan?.Plan?.ResultFile,
            requiredInputs)
        {
            ContextFields = ContextFieldWeaveOutMetadataBuilder.Build(instance, contextFieldCatalog, pendingTransition) is { Count: > 0 } fields
                ? fields
                : null,
            ContextFieldWarnings = contextFieldWarnings is { Count: > 0 } ? contextFieldWarnings : null,
        };
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