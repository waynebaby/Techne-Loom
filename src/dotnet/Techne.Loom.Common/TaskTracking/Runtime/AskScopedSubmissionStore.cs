using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed partial class AskScopedSubmissionStore
{
    private const int SupportedStateVersion = 1;
    private const string MachineCapabilityFileName = "machine-capability";
    private static readonly JsonSerializerOptions JsonOptions = WorkflowJsonSerializer.CreateDefaultOptions(indented: false);
    private static readonly UnixFileMode PrivateDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private static readonly UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly AskScopedSubmissionStoreOptions _options;
    private readonly ISystemClock _clock;
    private readonly string _rootDirectory;

    public AskScopedSubmissionStore(AskScopedSubmissionStoreOptions? options = null, ISystemClock? clock = null)
    {
        _options = options ?? new AskScopedSubmissionStoreOptions();
        _clock = clock ?? new SystemClock();
        ValidateOptions(_options);
        _rootDirectory = Path.GetFullPath(_options.RootDirectory);
        Directory.CreateDirectory(_rootDirectory);
        EnsureNotReparsePoint(_rootDirectory);
        EnsurePrivateDirectory(_rootDirectory);
    }

    public string RootDirectory => _rootDirectory;

    public async Task<AskScopedLaunch> CreateAsync(
        string workflowInstanceId,
        CommandTransition askTransition,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowInstanceId);
        ValidateStructuredAsk(askTransition);

        await using var storeLock = await AcquireStoreLockAsync(ct).ConfigureAwait(false);
        await CleanupExpiredCoreAsync(ct).ConfigureAwait(false);
        await EnsureActiveAskCapacityAsync(ct).ConfigureAwait(false);
        return await CreateAskCoreAsync(workflowInstanceId, askTransition, waitId: null, correlationKey: null, ct).ConfigureAwait(false);
    }

    public async Task<AskScopedLaunch> GetOrCreateForWaitAsync(
        WorkflowInstance instance,
        PendingWaitGroup waitGroup,
        CancellationToken ct = default)
        => await GetForWaitCoreAsync(instance, waitGroup, createIfMissing: true, ct: ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A structured ask could not be created for the active wait.");

    public Task<AskScopedLaunch?> GetForWaitAsync(
        WorkflowInstance instance,
        PendingWaitGroup waitGroup,
        CancellationToken ct = default)
        => GetForWaitCoreAsync(instance, waitGroup, createIfMissing: false, ct: ct);

    private async Task<AskScopedLaunch?> GetForWaitCoreAsync(
        WorkflowInstance instance,
        PendingWaitGroup waitGroup,
        bool createIfMissing,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(waitGroup);
        if (instance.Status != WorkflowStatus.WaitingExternal
            || !instance.ActiveWaitGroups.Contains(waitGroup)
            || !string.Equals(waitGroup.InstanceId, instance.InstanceId, StringComparison.Ordinal)
            || waitGroup.Completed
            || waitGroup.TimedOut)
        {
            throw new InvalidOperationException("A structured ask can only be opened for an active pending workflow wait group.");
        }

        var pendingEntry = waitGroup.GetNextPendingEntry()
            ?? throw new InvalidOperationException("The structured ask wait group has no pending entry.");
        if (!IsValidWaitId(pendingEntry.WaitId))
        {
            throw new InvalidOperationException("The structured ask wait entry has an invalid wait id.");
        }

        if (!instance.Nodes.TryGetValue(waitGroup.TransitionId, out var node)
            || node is not CommandTransition askTransition
            || !string.Equals(askTransition.Id, waitGroup.TransitionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The structured ask transition '{waitGroup.TransitionId}' is missing or does not match its wait key.");
        }
        ValidateStructuredAsk(askTransition);

        await using var storeLock = await AcquireStoreLockAsync(ct).ConfigureAwait(false);
        await CleanupExpiredCoreAsync(ct).ConfigureAwait(false);
        AskScopedState? matchingState = null;
        foreach (var askDirectory in EnumerateAskDirectories())
        {
            var candidate = await ReadStateAsync(Path.GetFileName(askDirectory), ct).ConfigureAwait(false);
            if (!string.Equals(candidate.WorkflowInstanceId, instance.InstanceId, StringComparison.Ordinal)
                || !string.Equals(candidate.TransitionId, waitGroup.TransitionId, StringComparison.Ordinal)
                || !string.Equals(candidate.WaitId, pendingEntry.WaitId, StringComparison.Ordinal))
            {
                continue;
            }

            if (matchingState is not null)
            {
                throw new InvalidOperationException("Multiple ask records match the same active workflow wait entry.");
            }

            matchingState = candidate;
        }

        if (matchingState is not null)
        {
            if (!string.Equals(matchingState.CorrelationKey, waitGroup.CorrelationKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The persisted ask correlation key does not match the active workflow wait.");
            }

            var storedContractHash = WorkflowOperationLedger.ComputeRequestHash(matchingState.Contract);
            var activeContractHash = WorkflowOperationLedger.ComputeRequestHash(askTransition.UserInput!);
            if (!string.Equals(storedContractHash, activeContractHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The persisted ask contract does not match the active workflow transition.");
            }

            EnsureNotExpired(matchingState);
            return await ReadLaunchAsync(matchingState, ct).ConfigureAwait(false);
        }

        if (!createIfMissing)
        {
            return null;
        }

        await EnsureActiveAskCapacityAsync(ct).ConfigureAwait(false);
        return await CreateAskCoreAsync(instance.InstanceId, askTransition, pendingEntry.WaitId, waitGroup.CorrelationKey, ct).ConfigureAwait(false);
    }

    private static void ValidateStructuredAsk(CommandTransition askTransition)
    {
        ArgumentNullException.ThrowIfNull(askTransition);
        if (askTransition.StepKind != WorkflowStepKind.AskUser || askTransition.UserInput is null)
        {
            throw new ArgumentException("An ask-scoped store requires an AskUser transition with a structured user input contract.", nameof(askTransition));
        }

        var contractDiagnostics = UserInputContractValidator.Validate([askTransition]);
        if (contractDiagnostics.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, contractDiagnostics.Select(static item => $"{item.Location}: {item.Message}")));
        }
    }

    private async Task EnsureActiveAskCapacityAsync(CancellationToken ct)
    {
        var activeAskCount = await CountActiveAsksCoreAsync(ct).ConfigureAwait(false);
        if (activeAskCount >= _options.MaxActiveAsks)
        {
            throw new AskScopedConflictException($"The ask store has reached its active ask limit of {_options.MaxActiveAsks}.");
        }
    }

    private async Task<AskScopedLaunch> CreateAskCoreAsync(
        string workflowInstanceId,
        CommandTransition askTransition,
        string? waitId,
        string? correlationKey,
        CancellationToken ct)
    {
        var askId = Guid.NewGuid().ToString("N");
        var machineCapability = CreateCapability();
        var now = _clock.UtcNow.ToUniversalTime();
        var askDirectory = GetAskDirectory(askId);
        Directory.CreateDirectory(askDirectory);
        try
        {
            EnsurePrivateDirectory(askDirectory);
            await WriteMachineCapabilityAsync(askDirectory, machineCapability, ct).ConfigureAwait(false);
            var state = new AskScopedState
            {
                AskId = askId,
                WorkflowInstanceId = workflowInstanceId,
                TransitionId = askTransition.Id,
                WaitId = waitId,
                CorrelationKey = correlationKey,
                MachineCapabilityHash = HashSecret(machineCapability),
                CreatedAtUtc = now,
                ExpiresAtUtc = now + _options.DraftLifetime,
                UpdatedAtUtc = now,
                Contract = CloneContract(askTransition.UserInput!),
            };
            await WriteStateAsync(state, ct).ConfigureAwait(false);
            return new AskScopedLaunch(askId, machineCapability, state.Generation, GetEffectiveExpiry(state));
        }
        catch
        {
            if (Directory.Exists(askDirectory))
            {
                Directory.Delete(askDirectory, recursive: true);
            }

            throw;
        }
    }

    private async Task WriteMachineCapabilityAsync(string askDirectory, string machineCapability, CancellationToken ct)
    {
        var capabilityPath = Path.Combine(askDirectory, MachineCapabilityFileName);
        var bytes = Encoding.UTF8.GetBytes(machineCapability);
        await using var stream = new FileStream(
            capabilityPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        EnsurePrivateFile(capabilityPath);
        await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private async Task<AskScopedLaunch> ReadLaunchAsync(AskScopedState state, CancellationToken ct)
    {
        var capabilityPath = Path.Combine(GetAskDirectory(state.AskId), MachineCapabilityFileName);
        if (!File.Exists(capabilityPath))
        {
            throw new InvalidOperationException($"Ask '{state.AskId}' is missing its protected machine capability.");
        }

        EnsureNotReparsePoint(capabilityPath);
        var machineCapability = await File.ReadAllTextAsync(capabilityPath, ct).ConfigureAwait(false);
        Authorize(state, machineCapability);
        return new AskScopedLaunch(state.AskId, machineCapability, state.Generation, GetEffectiveExpiry(state));
    }

    public async Task<AskScopedLaunch?> GetLaunchAsync(string askId, CancellationToken ct = default)
    {
        await using var storeLock = await AcquireStoreLockAsync(ct).ConfigureAwait(false);
        await CleanupExpiredCoreAsync(ct).ConfigureAwait(false);
        var askDirectory = GetAskDirectory(askId);
        if (!Directory.Exists(askDirectory))
        {
            return null;
        }

        var state = await ReadStateAsync(askId, ct).ConfigureAwait(false);
        EnsureNotExpired(state);
        return await ReadLaunchAsync(state, ct).ConfigureAwait(false);
    }

    public async Task<AskScopedSnapshot> GetSnapshotAsync(
        string askId,
        string machineCapability,
        CancellationToken ct = default)
    {
        await using var storeLock = await AcquireStoreLockAsync(ct).ConfigureAwait(false);
        await using var askLock = await AcquireAskLockAsync(askId, ct).ConfigureAwait(false);
        var state = await ReadStateAsync(askId, ct).ConfigureAwait(false);
        Authorize(state, machineCapability);
        EnsureNotExpired(state);
        if (state.Receipt is not null)
        {
            await VerifyReferencedAttachmentContentAsync(state, state.Receipt.Answers, ct).ConfigureAwait(false);
        }

        return ToSnapshot(state);
    }

    public async Task<AskScopedSnapshot> SaveDraftAsync(
        string askId,
        string machineCapability,
        long expectedGeneration,
        IReadOnlyDictionary<string, AskScopedAnswerValue> answers,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(answers);
        await using var storeLock = await AcquireStoreLockAsync(ct).ConfigureAwait(false);
        await using var askLock = await AcquireAskLockAsync(askId, ct).ConfigureAwait(false);
        var state = await ReadStateAsync(askId, ct).ConfigureAwait(false);
        Authorize(state, machineCapability);
        EnsureNotExpired(state);
        if (state.Receipt is not null)
        {
            throw new AskScopedConflictException($"Ask '{askId}' has already been submitted and cannot accept draft changes.");
        }

        EnsureGeneration(state, expectedGeneration);
        var draftAnswers = CloneAnswers(answers);
        var validation = UserInputAnswerValidator.Validate(state.Contract, draftAnswers, state.Attachments, requireComplete: false);
        if (!validation.IsValid)
        {
            throw new AskScopedValidationException(validation.Diagnostics);
        }

        state.DraftAnswers = draftAnswers;
        state.Generation = checked(state.Generation + 1);
        state.UpdatedAtUtc = _clock.UtcNow.ToUniversalTime();
        await WriteStateAsync(state, ct).ConfigureAwait(false);
        return ToSnapshot(state);
    }

    public async Task<AskScopedSubmissionReceipt> SubmitAsync(
        string askId,
        string machineCapability,
        long expectedGeneration,
        string operationId,
        IReadOnlyDictionary<string, AskScopedAnswerValue> answers,
        CancellationToken ct = default)
    {
        WorkflowOperationLedger.ValidateOperationId(operationId);
        ArgumentNullException.ThrowIfNull(answers);
        var submittedAnswers = CloneAnswers(answers);
        var requestHash = WorkflowOperationLedger.ComputeRequestHash(submittedAnswers);

        await using var storeLock = await AcquireStoreLockAsync(ct).ConfigureAwait(false);
        await using var askLock = await AcquireAskLockAsync(askId, ct).ConfigureAwait(false);
        var state = await ReadStateAsync(askId, ct).ConfigureAwait(false);
        Authorize(state, machineCapability);
        EnsureNotExpired(state);

        if (state.Receipt is not null)
        {
            if (string.Equals(state.Receipt.OperationId, operationId, StringComparison.Ordinal)
                && string.Equals(state.Receipt.RequestHash, requestHash, StringComparison.Ordinal))
            {
                await VerifyReferencedAttachmentContentAsync(state, state.Receipt.Answers, ct).ConfigureAwait(false);
                return state.Receipt;
            }

            throw new AskScopedConflictException($"Ask '{askId}' already has a receipt for a different submission.");
        }

        EnsureGeneration(state, expectedGeneration);
        var validation = UserInputAnswerValidator.Validate(state.Contract, submittedAnswers, state.Attachments);
        if (!validation.IsValid)
        {
            throw new AskScopedValidationException(validation.Diagnostics);
        }

        var submittedAtUtc = _clock.UtcNow.ToUniversalTime();
        var nextGeneration = checked(state.Generation + 1);
        var unsignedReceipt = new AskScopedSubmissionReceipt(
            SupportedStateVersion,
            state.AskId,
            state.WorkflowInstanceId,
            state.TransitionId,
            state.Generation,
            nextGeneration,
            operationId,
            requestHash,
            submittedAtUtc,
            validation.NormalizedAnswers,
            string.Empty);
        var receipt = unsignedReceipt with { IntegrityHash = ComputeReceiptIntegrityHash(unsignedReceipt) };

        state.DraftAnswers = submittedAnswers;
        state.Receipt = receipt;
        state.Generation = nextGeneration;
        state.UpdatedAtUtc = submittedAtUtc;
        await WriteStateAsync(state, ct).ConfigureAwait(false);
        return receipt;
    }

    public async Task<AskScopedSnapshot> MarkAppliedAsync(
        string askId,
        string machineCapability,
        long receiptGeneration,
        CancellationToken ct = default)
    {
        await using var storeLock = await AcquireStoreLockAsync(ct).ConfigureAwait(false);
        await using var askLock = await AcquireAskLockAsync(askId, ct).ConfigureAwait(false);
        var state = await ReadStateAsync(askId, ct).ConfigureAwait(false);
        Authorize(state, machineCapability);
        EnsureNotExpired(state);
        if (state.Receipt is null || state.Receipt.Generation != receiptGeneration)
        {
            throw new AskScopedConflictException($"Ask '{askId}' does not have the requested submission receipt.");
        }

        await VerifyReferencedAttachmentContentAsync(state, state.Receipt.Answers, ct).ConfigureAwait(false);
        if (state.AppliedAtUtc is null)
        {
            state.AppliedAtUtc = _clock.UtcNow.ToUniversalTime();
            state.UpdatedAtUtc = state.AppliedAtUtc.Value;
            await WriteStateAsync(state, ct).ConfigureAwait(false);
        }

        return ToSnapshot(state);
    }

    public async Task<AskScopedCleanupResult> CleanupExpiredAsync(CancellationToken ct = default)
    {
        await using var storeLock = await AcquireStoreLockAsync(ct).ConfigureAwait(false);
        return await CleanupExpiredCoreAsync(ct).ConfigureAwait(false);
    }

    private async Task<IAsyncDisposable> AcquireStoreLockAsync(CancellationToken ct)
        => await WorkflowFileLock.AcquireAsync(Path.Combine(_rootDirectory, ".store"), ct).ConfigureAwait(false);

    private async Task<IAsyncDisposable> AcquireAskLockAsync(string askId, CancellationToken ct)
    {
        var askDirectory = GetAskDirectory(askId);
        if (!Directory.Exists(askDirectory))
        {
            throw new FileNotFoundException($"Ask '{askId}' was not found.", askDirectory);
        }

        EnsureNotReparsePoint(askDirectory);
        return await WorkflowFileLock.AcquireAsync(Path.Combine(askDirectory, "state.json"), ct).ConfigureAwait(false);
    }

    private async Task<AskScopedState> ReadStateAsync(string askId, CancellationToken ct)
    {
        ValidateAskId(askId);
        var askDirectory = GetAskDirectory(askId);
        EnsureNotReparsePoint(askDirectory);
        var statePath = Path.Combine(askDirectory, "state.json");
        if (!File.Exists(statePath))
        {
            throw new FileNotFoundException($"Ask '{askId}' was not found.", statePath);
        }

        EnsureNotReparsePoint(statePath);
        byte[] stateBytes;
        await using (var stateStream = new FileStream(
            statePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            if (stateStream.Length > _options.MaxAskBytes || stateStream.Length > int.MaxValue)
            {
                throw new InvalidOperationException($"Ask '{askId}' exceeds the configured per-ask storage limit.");
            }

            stateBytes = new byte[(int)stateStream.Length];
            var bytesRead = 0;
            while (bytesRead < stateBytes.Length)
            {
                var read = await stateStream.ReadAsync(stateBytes.AsMemory(bytesRead), ct).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new InvalidOperationException($"Ask '{askId}' state was truncated while being read.");
                }

                bytesRead += read;
            }

            var extraByte = new byte[1];
            if (await stateStream.ReadAsync(extraByte.AsMemory(), ct).ConfigureAwait(false) != 0)
            {
                throw new InvalidOperationException($"Ask '{askId}' exceeds the configured per-ask storage limit.");
            }
        }

        AskScopedState state;
        try
        {
            state = JsonSerializer.Deserialize<AskScopedState>(stateBytes, JsonOptions)
                ?? throw new JsonException("Ask state was empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Ask '{askId}' contains invalid persisted state.", exception);
        }

        if (state.SchemaVersion != SupportedStateVersion
            || !string.Equals(state.AskId, askId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(state.WorkflowInstanceId)
            || string.IsNullOrWhiteSpace(state.TransitionId)
            || (state.WaitId is not null && !IsValidWaitId(state.WaitId))
            || !IsSha256Hex(state.MachineCapabilityHash)
            || state.CreatedAtUtc == default
            || state.ExpiresAtUtc <= state.CreatedAtUtc
            || state.UpdatedAtUtc < state.CreatedAtUtc
            || state.Generation < 0
            || state.Contract is null
            || state.DraftAnswers is null
            || state.Attachments is null)
        {
            throw new InvalidOperationException($"Ask '{askId}' contains an unsupported or inconsistent state record.");
        }

        ValidatePersistedAttachmentMetadata(state);
        if (state.Receipt is not null && !VerifyReceiptIntegrity(state, state.Receipt))
        {
            throw new InvalidOperationException($"Ask '{askId}' contains a submission receipt with an invalid integrity hash.");
        }

        return state;
    }

    private async Task WriteStateAsync(AskScopedState state, CancellationToken ct)
    {
        var askDirectory = GetAskDirectory(state.AskId);
        var statePath = Path.Combine(askDirectory, "state.json");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
        var oldStateLength = File.Exists(statePath) ? new FileInfo(statePath).Length : 0;
        var askSize = GetDirectorySize(askDirectory);
        var storeSize = GetDirectorySize(_rootDirectory);
        if (askSize - oldStateLength + bytes.LongLength > _options.MaxAskBytes)
        {
            throw new AskScopedConflictException($"Ask '{state.AskId}' would exceed its configured storage limit of {_options.MaxAskBytes} bytes.");
        }

        if (storeSize - oldStateLength + bytes.LongLength > _options.MaxStoreBytes)
        {
            throw new AskScopedConflictException($"The ask store would exceed its configured storage limit of {_options.MaxStoreBytes} bytes.");
        }

        var temporaryPath = Path.Combine(askDirectory, $".state.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                EnsurePrivateFile(temporaryPath);
                await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, statePath, overwrite: true);
            EnsurePrivateFile(statePath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private async Task<AskScopedCleanupResult> CleanupExpiredCoreAsync(CancellationToken ct)
    {
        var removedAsks = 0;
        long freedBytes = 0;
        var now = _clock.UtcNow.ToUniversalTime();
        foreach (var askDirectory in EnumerateAskDirectories())
        {
            ct.ThrowIfCancellationRequested();
            var askId = Path.GetFileName(askDirectory);
            var statePath = Path.Combine(askDirectory, "state.json");
            if (!File.Exists(statePath))
            {
                freedBytes = checked(freedBytes + GetDirectorySize(askDirectory));
                Directory.Delete(askDirectory, recursive: true);
                removedAsks++;
                continue;
            }

            var state = await ReadStateAsync(askId, ct).ConfigureAwait(false);
            if (now < GetEffectiveExpiry(state))
            {
                continue;
            }

            freedBytes = checked(freedBytes + GetDirectorySize(askDirectory));
            Directory.Delete(askDirectory, recursive: true);
            removedAsks++;
        }

        return new AskScopedCleanupResult(removedAsks, freedBytes);
    }

    private async Task<int> CountActiveAsksCoreAsync(CancellationToken ct)
    {
        var count = 0;
        var now = _clock.UtcNow.ToUniversalTime();
        foreach (var askDirectory in EnumerateAskDirectories())
        {
            ct.ThrowIfCancellationRequested();
            var state = await ReadStateAsync(Path.GetFileName(askDirectory), ct).ConfigureAwait(false);
            if (now < GetEffectiveExpiry(state))
            {
                count++;
            }
        }

        return count;
    }

    private IEnumerable<string> EnumerateAskDirectories()
    {
        foreach (var directory in Directory.EnumerateDirectories(_rootDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            EnsureNotReparsePoint(directory);
            var askId = Path.GetFileName(directory);
            if (IsValidAskId(askId))
            {
                yield return directory;
            }
        }
    }

    private static long GetDirectorySize(string directory)
    {
        long size = 0;
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(directory);
        while (pendingDirectories.Count > 0)
        {
            var currentDirectory = pendingDirectories.Pop();
            EnsureNotReparsePoint(currentDirectory);
            foreach (var file in Directory.EnumerateFiles(currentDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                EnsureNotReparsePoint(file);
                size = checked(size + new FileInfo(file).Length);
            }

            foreach (var childDirectory in Directory.EnumerateDirectories(currentDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                EnsureNotReparsePoint(childDirectory);
                pendingDirectories.Push(childDirectory);
            }
        }

        return size;
    }

    private static UserInputContract CloneContract(UserInputContract contract)
    {
        var json = JsonSerializer.Serialize(contract, JsonOptions);
        return JsonSerializer.Deserialize<UserInputContract>(json, JsonOptions)
            ?? throw new InvalidOperationException("The AskUser contract could not be cloned.");
    }

    private static Dictionary<string, AskScopedAnswerValue> CloneAnswers(IReadOnlyDictionary<string, AskScopedAnswerValue> answers)
    {
        var clone = new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal);
        foreach (var (questionId, answer) in answers)
        {
            clone.Add(questionId, answer is null
                ? null!
                : new AskScopedAnswerValue
                {
                    Value = answer.Value?.Clone(),
                    Skipped = answer.Skipped,
                    AttachmentIds = answer.AttachmentIds is null ? null! : [.. answer.AttachmentIds],
                    FreeText = answer.FreeText,
                });
        }

        return clone;
    }

    private AskScopedSnapshot ToSnapshot(AskScopedState state)
        => new(
            state.AskId,
            state.WorkflowInstanceId,
            state.TransitionId,
            state.Generation,
            state.CreatedAtUtc,
            GetEffectiveExpiry(state),
            state.Contract,
            state.DraftAnswers,
            state.Attachments.Values.OrderBy(static attachment => attachment.AttachmentId, StringComparer.Ordinal).ToArray(),
            state.Receipt,
            state.AppliedAtUtc,
            state.WaitId,
            state.CorrelationKey);

    private DateTimeOffset GetEffectiveExpiry(AskScopedState state)
    {
        if (state.AppliedAtUtc is DateTimeOffset appliedAtUtc)
        {
            return appliedAtUtc + _options.AppliedReceiptLifetime;
        }

        if (state.Receipt is not null)
        {
            return state.Receipt.SubmittedAtUtc + _options.UnappliedReceiptLifetime;
        }

        return state.ExpiresAtUtc;
    }

    private void EnsureNotExpired(AskScopedState state)
    {
        if (_clock.UtcNow.ToUniversalTime() >= GetEffectiveExpiry(state))
        {
            throw new AskScopedExpiredException(state.AskId);
        }
    }

    private static void EnsureGeneration(AskScopedState state, long expectedGeneration)
    {
        if (expectedGeneration != state.Generation)
        {
            throw new AskScopedConflictException($"Ask '{state.AskId}' generation changed from {expectedGeneration} to {state.Generation}.");
        }
    }

    private static void Authorize(AskScopedState state, string machineCapability)
    {
        if (string.IsNullOrWhiteSpace(machineCapability))
        {
            throw new UnauthorizedAccessException("A machine capability is required for this ask.");
        }

        var expected = Convert.FromHexString(state.MachineCapabilityHash);
        var actual = Convert.FromHexString(HashSecret(machineCapability));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            throw new UnauthorizedAccessException("The machine capability is invalid for this ask.");
        }
    }

    private static string CreateCapability()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string HashSecret(string secret)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();

    private static string ComputeReceiptIntegrityHash(AskScopedSubmissionReceipt receipt)
        => WorkflowOperationLedger.ComputeRequestHash(receipt with { IntegrityHash = string.Empty });

    private static bool IsSha256Hex(string? value)
        => value is { Length: 64 } && value.All(static character => Uri.IsHexDigit(character));

    private static bool VerifyReceiptIntegrity(AskScopedState state, AskScopedSubmissionReceipt receipt)
    {
        if (receipt.SchemaVersion != SupportedStateVersion
            || !string.Equals(receipt.AskId, state.AskId, StringComparison.Ordinal)
            || !string.Equals(receipt.WorkflowInstanceId, state.WorkflowInstanceId, StringComparison.Ordinal)
            || !string.Equals(receipt.TransitionId, state.TransitionId, StringComparison.Ordinal)
            || receipt.Generation != state.Generation
            || receipt.Generation != receipt.PreviousGeneration + 1
            || receipt.SubmittedAtUtc < state.CreatedAtUtc
            || !WorkflowOperationLedger.IsValidOperationId(receipt.OperationId)
            || !IsSha256Hex(receipt.RequestHash)
            || !IsSha256Hex(receipt.IntegrityHash)
            || receipt.Answers is null
            || state.Attachments is null
            || state.Contract.QuestionGroups is null)
        {
            return false;
        }

        try
        {
            var questions = new Dictionary<string, UserInputQuestion>(StringComparer.Ordinal);
            foreach (var group in state.Contract.QuestionGroups)
            {
                if (group?.Questions is null)
                {
                    return false;
                }

                foreach (var question in group.Questions)
                {
                    if (question is null
                        || string.IsNullOrWhiteSpace(question.Id)
                        || !questions.TryAdd(question.Id, question))
                    {
                        return false;
                    }
                }
            }

            if (questions.Count == 0 || questions.Count != receipt.Answers.Count)
            {
                return false;
            }

            var answerValues = new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal);
            var referencedAttachments = new Dictionary<string, AskScopedAttachmentMetadata>(StringComparer.Ordinal);
            foreach (var normalizedAnswer in receipt.Answers)
            {
                if (normalizedAnswer is null
                    || normalizedAnswer.Attachments is null
                    || !questions.TryGetValue(normalizedAnswer.QuestionId, out var question)
                    || !string.Equals(normalizedAnswer.ContextPath, question.ContextPath, StringComparison.Ordinal))
                {
                    return false;
                }

                var attachmentIds = new List<string>(normalizedAnswer.Attachments.Count);
                foreach (var attachment in normalizedAnswer.Attachments)
                {
                    if (attachment is null
                        || !state.Attachments.TryGetValue(attachment.AttachmentId, out var persistedAttachment)
                        || persistedAttachment != attachment)
                    {
                        return false;
                    }

                    referencedAttachments[attachment.AttachmentId] = attachment;
                    attachmentIds.Add(attachment.AttachmentId);
                }

                if (!answerValues.TryAdd(normalizedAnswer.QuestionId, new AskScopedAnswerValue
                {
                    Value = normalizedAnswer.Value?.Clone(),
                    Skipped = normalizedAnswer.Skipped,
                    AttachmentIds = attachmentIds,
                    FreeText = normalizedAnswer.FreeText,
                }))
                {
                    return false;
                }
            }

            var askTransition = new CommandTransition
            {
                Id = state.TransitionId,
                StepKind = WorkflowStepKind.AskUser,
                Command = new CommandInvocation
                {
                    Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["requiredInputs"] = questions.Values.Select(static question => question.ContextPath).ToArray(),
                    },
                },
                UserInput = state.Contract,
            };
            if (UserInputContractValidator.Validate([askTransition]).Count > 0
                || !UserInputAnswerValidator.Validate(state.Contract, answerValues, referencedAttachments).IsValid)
            {
                return false;
            }

            var requestHash = WorkflowOperationLedger.ComputeRequestHash(answerValues);
            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(requestHash),
                Encoding.ASCII.GetBytes(receipt.RequestHash)))
            {
                return false;
            }

            var expected = ComputeReceiptIntegrityHash(receipt);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected),
                Encoding.ASCII.GetBytes(receipt.IntegrityHash));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException or NullReferenceException)
        {
            return false;
        }
    }

    private string GetAskDirectory(string askId)
    {
        ValidateAskId(askId);
        return Path.Combine(_rootDirectory, askId);
    }

    private static void ValidateAskId(string askId)
    {
        if (!IsValidAskId(askId))
        {
            throw new ArgumentException("Ask IDs must be 32 hexadecimal characters.", nameof(askId));
        }
    }

    private static bool IsValidAskId(string? askId)
        => askId is { Length: 32 } && Guid.TryParseExact(askId, "N", out _);

    private static bool IsValidWaitId(string? waitId)
        => waitId is { Length: 32 } && Guid.TryParseExact(waitId, "N", out _);

    private static void ValidateOptions(AskScopedSubmissionStoreOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RootDirectory))
        {
            throw new ArgumentException("A root directory is required.", nameof(options));
        }

        if (options.MaxActiveAsks <= 0
            || options.MaxAttachmentsPerAsk <= 0
            || options.MaxAskBytes <= 0
            || options.MaxStoreBytes <= 0
            || options.MaxAttachmentBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Ask counts and storage limits must be positive.");
        }

        if (options.DraftLifetime <= TimeSpan.Zero
            || options.UnappliedReceiptLifetime <= TimeSpan.Zero
            || options.AppliedReceiptLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Ask retention periods must be positive.");
        }
    }

    private static void EnsureNotReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException($"Ask storage path '{path}' cannot be a symbolic link or reparse point.");
        }
    }

    private static void EnsurePrivateDirectory(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var owner = identity.User ?? throw new UnauthorizedAccessException("The current Windows identity has no user SID.");
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.SetOwner(owner);
            security.AddAccessRule(new FileSystemAccessRule(
                owner,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            FileSystemAclExtensions.SetAccessControl(new DirectoryInfo(directory), security);
            return;
        }

        File.SetUnixFileMode(directory, PrivateDirectoryMode);
    }

    private static void EnsurePrivateFile(string file)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(file, PrivateFileMode);
        }
    }
}
