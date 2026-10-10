using System.Text.Json;
using System.Text.Json.Serialization;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record AskScopedResumeRequest(
    AskScopedLaunch Launch,
    string WorkflowInstanceId,
    string WaitId,
    string TransitionId,
    string? CorrelationKey,
    string OperationId,
    long ReceiptGeneration,
    Dictionary<string, object?> Payload);

public sealed record AskScopedOfflineSubmission(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("askId")] string AskId,
    [property: JsonPropertyName("expectedGeneration")] long ExpectedGeneration,
    [property: JsonPropertyName("operationId")] string OperationId,
    [property: JsonPropertyName("answers")] Dictionary<string, AskScopedAnswerValue> Answers);

public static class AskScopedSubmissionWorkflow
{
    private const int SupportedOfflineSubmissionVersion = 1;

    public static IReadOnlyList<PendingWaitGroup> GetActiveStructuredWaitGroups(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (instance.Status != WorkflowStatus.WaitingExternal)
        {
            return [];
        }

        return instance.ActiveWaitGroups
            .Where(group => !group.Completed
                && !group.TimedOut
                && group.GetNextPendingEntry() is not null
                && instance.Nodes.TryGetValue(group.TransitionId, out var node)
                && node is CommandTransition { StepKind: WorkflowStepKind.AskUser, UserInput: not null })
            .ToArray();
    }

    public static void ValidateActiveWait(WorkflowInstance instance, AskScopedResumeRequest request)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(request);
        if (instance.Status != WorkflowStatus.WaitingExternal
            || !string.Equals(request.WorkflowInstanceId, instance.InstanceId, StringComparison.Ordinal)
            || !instance.Nodes.TryGetValue(request.TransitionId, out var node)
            || node is not CommandTransition { StepKind: WorkflowStepKind.AskUser, UserInput: not null })
        {
            throw new InvalidOperationException("The AskUser receipt does not identify an active structured ask transition.");
        }

        var matches = GetActiveStructuredWaitGroups(instance)
            .Where(waitGroup => string.Equals(waitGroup.TransitionId, request.TransitionId, StringComparison.Ordinal)
                && string.Equals(waitGroup.CorrelationKey, request.CorrelationKey, StringComparison.Ordinal)
                && string.Equals(waitGroup.GetNextPendingEntry()?.WaitId, request.WaitId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException("The AskUser receipt does not match the active workflow wait.");
        }
    }

    public static void ValidateNoUnvalidatedAskUserResume(WorkflowInstance instance, string? transitionId)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (transitionId is not null
            && instance.Nodes.TryGetValue(transitionId, out var node)
            && node is CommandTransition { StepKind: WorkflowStepKind.AskUser, UserInput: not null })
        {
            throw new InvalidOperationException("Structured AskUser transitions must be resumed from a validated submission receipt.");
        }
    }

    public static Task<IReadOnlyList<AskScopedWorkerEndpoint>> StartWorkersForActiveWaitsAsync(
        WorkflowInstance instance,
        CancellationToken ct = default)
        => StartWorkersForActiveWaitsAsync(instance, new HashSet<string>(StringComparer.Ordinal), ct);

    public static async Task<IReadOnlyList<AskScopedWorkerEndpoint>> StartWorkersForActiveWaitsAsync(
        WorkflowInstance instance,
        IReadOnlySet<string> existingWaitIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existingWaitIds);
        if (GetActiveStructuredWaitGroups(instance).All(waitGroup =>
            waitGroup.GetNextPendingEntry() is { } entry && existingWaitIds.Contains(entry.WaitId)))
        {
            return [];
        }

        return await StartWorkersForActiveWaitsAsync(
            instance,
            new AskScopedSubmissionStore(),
            AskScopedWorkerProcessLauncher.StartDetachedAsync,
            ct,
            existingWaitIds).ConfigureAwait(false);
    }

    internal static async Task<IReadOnlyList<AskScopedWorkerEndpoint>> StartWorkersForActiveWaitsAsync(
        WorkflowInstance instance,
        AskScopedSubmissionStore store,
        Func<AskScopedSubmissionStore, AskScopedLaunch, CancellationToken, Task<AskScopedWorkerEndpoint>> startWorker,
        CancellationToken ct = default,
        IReadOnlySet<string>? existingWaitIds = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(startWorker);
        var endpoints = new List<AskScopedWorkerEndpoint>();
        foreach (var waitGroup in GetActiveStructuredWaitGroups(instance))
        {
            ct.ThrowIfCancellationRequested();
            var pendingEntry = waitGroup.GetNextPendingEntry()
                ?? throw new InvalidOperationException("The structured ask wait group has no pending entry.");
            if (existingWaitIds?.Contains(pendingEntry.WaitId) == true)
            {
                continue;
            }

            var launch = await store.GetOrCreateForWaitAsync(instance, waitGroup, ct).ConfigureAwait(false);
            var snapshot = await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability, ct).ConfigureAwait(false);
            if (snapshot.Receipt is null)
            {
                endpoints.Add(await startWorker(store, launch, ct).ConfigureAwait(false));
            }
        }

        return endpoints;
    }

    public static async Task<AskScopedResumeRequest?> TryGetSubmittedReceiptAsync(
        WorkflowInstance instance,
        PendingWaitGroup waitGroup,
        AskScopedSubmissionStore store,
        string? expectedAskId = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        var launch = await store.GetForWaitAsync(instance, waitGroup, ct).ConfigureAwait(false);
        if (launch is null)
        {
            if (!string.IsNullOrWhiteSpace(expectedAskId))
            {
                throw new InvalidOperationException($"Ask '{expectedAskId}' does not belong to the active workflow wait.");
            }

            return null;
        }

        if (!string.IsNullOrWhiteSpace(expectedAskId)
            && !string.Equals(launch.AskId, expectedAskId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Ask '{expectedAskId}' does not belong to the active workflow wait.");
        }

        var snapshot = await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability, ct).ConfigureAwait(false);
        if (snapshot.Receipt is null)
        {
            if (!string.IsNullOrWhiteSpace(expectedAskId))
            {
                throw new InvalidOperationException($"Ask '{expectedAskId}' has not been submitted.");
            }

            return null;
        }

        return CreateResumeRequest(instance, waitGroup, launch, snapshot);
    }

    public static async Task<AskScopedResumeRequest> GetSubmittedReceiptAsync(
        AskScopedSubmissionStore store,
        string askId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(askId);
        var launch = await store.GetLaunchAsync(askId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Ask '{askId}' was not found.");
        var snapshot = await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability, ct).ConfigureAwait(false);
        return CreateResumeRequest(launch, snapshot);
    }

    public static async Task<AskScopedResumeRequest> SubmitOfflineAnswersAsync(
        WorkflowInstance instance,
        AskScopedSubmissionStore store,
        AskScopedOfflineSubmission submission,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(store);
        ValidateOfflineSubmission(submission);

        var matchingWaitGroups = new List<PendingWaitGroup>();
        foreach (var waitGroup in GetActiveStructuredWaitGroups(instance))
        {
            var activeLaunch = await store.GetForWaitAsync(instance, waitGroup, ct).ConfigureAwait(false);
            if (activeLaunch is not null && string.Equals(activeLaunch.AskId, submission.AskId, StringComparison.Ordinal))
            {
                matchingWaitGroups.Add(waitGroup);
            }
        }

        if (matchingWaitGroups.Count > 1)
        {
            throw new InvalidOperationException($"Ask '{submission.AskId}' matches multiple active workflow waits.");
        }

        if (matchingWaitGroups.Count == 1)
        {
            return await SubmitOfflineAnswersAsync(instance, matchingWaitGroups[0], store, submission, ct).ConfigureAwait(false);
        }

        var launch = await store.GetLaunchAsync(submission.AskId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Ask '{submission.AskId}' does not belong to an active workflow wait.");
        var snapshot = await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability, ct).ConfigureAwait(false);
        if (!string.Equals(snapshot.WorkflowInstanceId, instance.InstanceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The offline AskUser submission belongs to a different workflow instance.");
        }

        if (snapshot.Receipt is null)
        {
            throw new InvalidOperationException($"Ask '{submission.AskId}' does not belong to an active workflow wait.");
        }

        var receipt = await store.SubmitAsync(
            launch.AskId,
            launch.MachineCapability,
            submission.ExpectedGeneration,
            submission.OperationId,
            submission.Answers,
            ct).ConfigureAwait(false);
        if (!string.Equals(snapshot.Receipt.IntegrityHash, receipt.IntegrityHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The offline AskUser submission does not match its persisted receipt.");
        }

        return CreateResumeRequest(launch, snapshot);
    }

    public static async Task<AskScopedResumeRequest> SubmitOfflineAnswersAsync(
        WorkflowInstance instance,
        PendingWaitGroup waitGroup,
        AskScopedSubmissionStore store,
        AskScopedOfflineSubmission submission,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ValidateOfflineSubmission(submission);

        var launch = await store.GetForWaitAsync(instance, waitGroup, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Ask '{submission.AskId}' does not belong to the active workflow wait.");
        if (!string.Equals(launch.AskId, submission.AskId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Ask '{submission.AskId}' does not belong to the active workflow wait.");
        }

        var receipt = await store.SubmitAsync(
            launch.AskId,
            launch.MachineCapability,
            submission.ExpectedGeneration,
            submission.OperationId,
            submission.Answers,
            ct).ConfigureAwait(false);
        var snapshot = await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability, ct).ConfigureAwait(false);
        if (snapshot.Receipt is null || !string.Equals(snapshot.Receipt.IntegrityHash, receipt.IntegrityHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The offline AskUser submission receipt could not be verified after persistence.");
        }

        return CreateResumeRequest(instance, waitGroup, launch, snapshot);
    }

    private static void ValidateOfflineSubmission(AskScopedOfflineSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (submission.SchemaVersion != SupportedOfflineSubmissionVersion)
        {
            throw new InvalidOperationException($"Offline AskUser submission version '{submission.SchemaVersion}' is not supported.");
        }

        if (submission.ExpectedGeneration < 0 || string.IsNullOrWhiteSpace(submission.AskId)
            || string.IsNullOrWhiteSpace(submission.OperationId) || submission.Answers is null)
        {
            throw new InvalidOperationException("The offline AskUser submission is incomplete.");
        }

        WorkflowOperationLedger.ValidateOperationId(submission.OperationId);
    }

    public static string CreateResumeOperationId(AskScopedSubmissionReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var operationId = $"loom-ask-{receipt.AskId}-{receipt.Generation}";
        WorkflowOperationLedger.ValidateOperationId(operationId);
        return operationId;
    }

    private static AskScopedResumeRequest CreateResumeRequest(
        WorkflowInstance instance,
        PendingWaitGroup waitGroup,
        AskScopedLaunch launch,
        AskScopedSnapshot snapshot)
    {
        var pendingEntry = waitGroup.GetNextPendingEntry()
            ?? throw new InvalidOperationException("The structured ask wait group has no pending entry.");
        if (instance.Status != WorkflowStatus.WaitingExternal
            || !instance.ActiveWaitGroups.Contains(waitGroup)
            || !string.Equals(snapshot.WorkflowInstanceId, instance.InstanceId, StringComparison.Ordinal)
            || !string.Equals(snapshot.TransitionId, waitGroup.TransitionId, StringComparison.Ordinal)
            || !string.Equals(snapshot.WaitId, pendingEntry.WaitId, StringComparison.Ordinal)
            || !string.Equals(snapshot.CorrelationKey, waitGroup.CorrelationKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The AskUser receipt does not match the active workflow wait.");
        }

        return CreateResumeRequest(launch, snapshot);
    }

    private static AskScopedResumeRequest CreateResumeRequest(
        AskScopedLaunch launch,
        AskScopedSnapshot snapshot)
    {
        var receipt = snapshot.Receipt
            ?? throw new InvalidOperationException($"Ask '{launch.AskId}' has not been submitted.");
        if (snapshot.ConsumerKind != AskScopedConsumerKind.WorkflowNode
            || receipt.ConsumerKind != AskScopedConsumerKind.WorkflowNode
            || !string.Equals(snapshot.AskId, launch.AskId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(snapshot.WorkflowInstanceId)
            || string.IsNullOrWhiteSpace(snapshot.WaitId)
            || string.IsNullOrWhiteSpace(snapshot.TransitionId)
            || !string.Equals(receipt.AskId, snapshot.AskId, StringComparison.Ordinal)
            || !string.Equals(receipt.WorkflowInstanceId, snapshot.WorkflowInstanceId, StringComparison.Ordinal)
            || !string.Equals(receipt.TransitionId, snapshot.TransitionId, StringComparison.Ordinal)
            || receipt.SchemaVersion != 1
            || receipt.Generation != snapshot.Generation
            || receipt.PreviousGeneration < 0
            || receipt.Generation != checked(receipt.PreviousGeneration + 1))
        {
            throw new InvalidOperationException("The AskUser receipt does not match its persisted ask identity.");
        }

        var answers = new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal);
        foreach (var answer in receipt.Answers)
        {
            if (answer is null || string.IsNullOrWhiteSpace(answer.QuestionId) || answer.Attachments is null
                || !answers.TryAdd(answer.QuestionId, new AskScopedAnswerValue
                {
                    Value = answer.Value is JsonElement value ? value.Clone() : null,
                    Skipped = answer.Skipped,
                    AttachmentIds = answer.Attachments.Select(static attachment => attachment.AttachmentId).ToList(),
                    FreeText = answer.FreeText,
                }))
            {
                throw new InvalidOperationException("The AskUser receipt contains duplicate or invalid normalized answers.");
            }
        }

        var attachmentMap = snapshot.Attachments.ToDictionary(static attachment => attachment.AttachmentId, StringComparer.Ordinal);
        var validation = UserInputAnswerValidator.Validate(snapshot.Contract, answers, attachmentMap);
        if (!validation.IsValid || validation.NormalizedAnswers.Count != receipt.Answers.Count)
        {
            throw new AskScopedValidationException(validation.Diagnostics.Count > 0
                ? validation.Diagnostics
                : [new UserInputAnswerDiagnostic("receipt", "The normalized answer set is incomplete.")]);
        }

        var questionMap = snapshot.Contract.QuestionGroups
            .SelectMany(static group => group.Questions ?? [])
            .ToDictionary(static question => question.Id, StringComparer.Ordinal);

        object? ProjectContextValue(UserInputQuestion question, AskScopedNormalizedAnswer answer)
        {
            if (answer.Skipped)
            {
                return null;
            }

            var freeText = string.IsNullOrWhiteSpace(answer.FreeText) ? null : answer.FreeText;
            object? typedValue = answer.Value is JsonElement jsonValue ? jsonValue.Clone() : null;
            if (freeText is null)
            {
                return answer.Attachments.Count == 1 ? answer.Attachments[0] : typedValue;
            }

            switch (question.Type)
            {
                case UserInputQuestionTypes.SingleChoice:
                    return freeText;
                case UserInputQuestionTypes.MultipleChoice:
                {
                    var selections = new List<JsonElement>();
                    if (typedValue is JsonElement selectedValues && selectedValues.ValueKind == JsonValueKind.Array)
                    {
                        selections.AddRange(selectedValues.EnumerateArray().Select(static item => item.Clone()));
                    }

                    selections.Add(JsonSerializer.SerializeToElement(freeText));
                    return JsonSerializer.SerializeToElement(selections);
                }
                case UserInputQuestionTypes.Number:
                case UserInputQuestionTypes.Boolean:
                    if (typedValue is null)
                    {
                        return freeText;
                    }

                    return new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["value"] = typedValue,
                        ["freeText"] = freeText,
                    };
                case UserInputQuestionTypes.File:
                case UserInputQuestionTypes.Audio:
                    if (answer.Attachments.Count == 1)
                    {
                        return new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["attachment"] = answer.Attachments[0],
                            ["freeText"] = freeText,
                        };
                    }

                    return freeText;
                default:
                    return typedValue;
            }
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var answer in validation.NormalizedAnswers)
        {
            if (!questionMap.TryGetValue(answer.QuestionId, out var question))
            {
                throw new InvalidOperationException("The normalized AskUser answer references an unknown question.");
            }

            var contextPath = answer.ContextPath;
            if (string.IsNullOrWhiteSpace(contextPath))
            {
                throw new InvalidOperationException("A workflow-node AskUser answer is missing its context path.");
            }

            PathValueAccessor.SetValue(payload, contextPath, ProjectContextValue(question, answer));
        }

        return new AskScopedResumeRequest(
            launch,
            snapshot.WorkflowInstanceId!,
            snapshot.WaitId!,
            snapshot.TransitionId!,
            snapshot.CorrelationKey,
            CreateResumeOperationId(receipt),
            receipt.Generation,
            payload);
    }
}
