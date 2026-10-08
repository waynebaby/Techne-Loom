using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed partial class AskScopedSubmissionStore
{
    public async Task<AskScopedAttachmentUploadResult> StoreAttachmentAsync(
        string askId,
        string machineCapability,
        long expectedGeneration,
        string questionId,
        string fileName,
        string mediaType,
        Stream content,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(questionId);
        ArgumentNullException.ThrowIfNull(content);
        var normalizedFileName = NormalizeAttachmentFileName(questionId, fileName);
        var normalizedMediaType = NormalizeAttachmentMediaType(questionId, mediaType);
        var attachmentId = Guid.NewGuid().ToString("N");
        var storedAtUtc = _clock.UtcNow.ToUniversalTime();
        long policyLimit;

        await using (var storeLock = await AcquireStoreLockAsync(ct).ConfigureAwait(false))
        await using (var askLock = await AcquireAskLockAsync(askId, ct).ConfigureAwait(false))
        {
            var state = await ReadStateAsync(askId, ct).ConfigureAwait(false);
            Authorize(state, machineCapability);
            EnsureNotExpired(state);
            if (state.Receipt is not null)
            {
                throw new AskScopedConflictException($"Ask '{askId}' has already been submitted and cannot accept attachments.");
            }

            EnsureGeneration(state, expectedGeneration);
            if (state.Attachments.Count >= _options.MaxAttachmentsPerAsk)
            {
                throw new AskScopedConflictException($"Ask '{askId}' has reached its attachment limit of {_options.MaxAttachmentsPerAsk}.");
            }

            var questions = BuildQuestionMap(state.Contract);
            if (!questions.TryGetValue(questionId, out var question)
                || question.Type is not (UserInputQuestionTypes.File or UserInputQuestionTypes.Audio))
            {
                throw CreateAttachmentValidationException(questionId, "Attachments can only be uploaded for file or audio questions.");
            }

            policyLimit = Math.Min(_options.MaxAttachmentBytes, question.Constraints?.MaxAttachmentBytes ?? long.MaxValue);
            if (policyLimit <= 0)
            {
                throw CreateAttachmentValidationException(questionId, "The configured attachment byte limit must be positive.");
            }

            var projectedMetadata = new AskScopedAttachmentMetadata(
                attachmentId,
                questionId,
                normalizedFileName,
                normalizedMediaType,
                policyLimit,
                new string('0', 64),
                storedAtUtc);
            if (GetAvailableAttachmentQuota(state, projectedMetadata) <= 0)
            {
                throw new AskScopedConflictException("The ask store has no remaining capacity for another attachment.");
            }

            var candidateAttachments = new Dictionary<string, AskScopedAttachmentMetadata>(state.Attachments, StringComparer.Ordinal)
            {
                [attachmentId] = projectedMetadata,
            };
            EnsureAttachmentCanBeReferenced(state.Contract, question, projectedMetadata, candidateAttachments);
        }

        var stagingDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ask-upload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);
        EnsureNotReparsePoint(stagingDirectory);
        EnsurePrivateDirectory(stagingDirectory);
        var stagingPath = Path.Combine(stagingDirectory, "content.bin");
        try
        {
            long length = 0;
            string sha256;
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                await using (var output = new FileStream(
                    stagingPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    EnsurePrivateFile(stagingPath);
                    var buffer = new byte[81920];
                    while (true)
                    {
                        var bytesRead = await content.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
                        if (bytesRead == 0)
                        {
                            break;
                        }

                        var nextLength = checked(length + bytesRead);
                        if (nextLength > policyLimit)
                        {
                            throw CreateAttachmentValidationException(questionId, "The uploaded attachment exceeds the configured byte limit.");
                        }

                        await output.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                        hash.AppendData(buffer, 0, bytesRead);
                        length = nextLength;
                    }

                    await output.FlushAsync(ct).ConfigureAwait(false);
                    output.Flush(flushToDisk: true);
                }

                if (length == 0)
                {
                    throw CreateAttachmentValidationException(questionId, "Uploaded attachments cannot be empty.");
                }

                sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }

            var metadata = new AskScopedAttachmentMetadata(
                attachmentId,
                questionId,
                normalizedFileName,
                normalizedMediaType,
                length,
                sha256,
                _clock.UtcNow.ToUniversalTime());
            await using var commitStoreLock = await AcquireStoreLockAsync(ct).ConfigureAwait(false);
            await using var commitAskLock = await AcquireAskLockAsync(askId, ct).ConfigureAwait(false);
            var currentState = await ReadStateAsync(askId, ct).ConfigureAwait(false);
            Authorize(currentState, machineCapability);
            EnsureNotExpired(currentState);
            if (currentState.Receipt is not null)
            {
                throw new AskScopedConflictException($"Ask '{askId}' has already been submitted and cannot accept attachments.");
            }

            EnsureGeneration(currentState, expectedGeneration);
            if (currentState.Attachments.Count >= _options.MaxAttachmentsPerAsk)
            {
                throw new AskScopedConflictException($"Ask '{askId}' has reached its attachment limit of {_options.MaxAttachmentsPerAsk}.");
            }

            var currentQuestions = BuildQuestionMap(currentState.Contract);
            if (!currentQuestions.TryGetValue(questionId, out var currentQuestion)
                || currentQuestion.Type is not (UserInputQuestionTypes.File or UserInputQuestionTypes.Audio))
            {
                throw CreateAttachmentValidationException(questionId, "Attachments can only be uploaded for file or audio questions.");
            }

            var currentPolicyLimit = Math.Min(_options.MaxAttachmentBytes, currentQuestion.Constraints?.MaxAttachmentBytes ?? long.MaxValue);
            if (length > currentPolicyLimit)
            {
                throw CreateAttachmentValidationException(questionId, "The uploaded attachment exceeds the configured byte limit.");
            }

            var quotaLimit = GetAvailableAttachmentQuota(currentState, metadata);
            if (length > quotaLimit)
            {
                throw new AskScopedConflictException("The uploaded attachment exceeds the remaining ask-store capacity.");
            }

            var candidateAttachments = new Dictionary<string, AskScopedAttachmentMetadata>(currentState.Attachments, StringComparer.Ordinal)
            {
                [attachmentId] = metadata,
            };
            EnsureAttachmentCanBeReferenced(currentState.Contract, currentQuestion, metadata, candidateAttachments);

            var askDirectory = GetAskDirectory(askId);
            var attachmentDirectory = Path.Combine(askDirectory, "attachments");
            Directory.CreateDirectory(attachmentDirectory);
            EnsureNotReparsePoint(attachmentDirectory);
            EnsurePrivateDirectory(attachmentDirectory);
            var temporaryPath = Path.Combine(attachmentDirectory, $".{Guid.NewGuid():N}.tmp");
            var attachmentPath = Path.Combine(attachmentDirectory, $"{attachmentId}.bin");
            try
            {
                File.Copy(stagingPath, temporaryPath);
                EnsurePrivateFile(temporaryPath);
                File.Move(temporaryPath, attachmentPath);
                EnsurePrivateFile(attachmentPath);
                currentState.Attachments.Add(attachmentId, metadata);
                currentState.Generation = checked(currentState.Generation + 1);
                currentState.UpdatedAtUtc = _clock.UtcNow.ToUniversalTime();
                await WriteStateAsync(currentState, ct).ConfigureAwait(false);
                return new AskScopedAttachmentUploadResult(metadata, currentState.Generation);
            }
            catch
            {
                currentState.Attachments.Remove(attachmentId);
                if (File.Exists(attachmentPath))
                {
                    File.Delete(attachmentPath);
                }

                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }

                throw;
            }
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                EnsureNotReparsePoint(stagingDirectory);
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    public async Task<Stream> OpenAttachmentReadStreamAsync(
        string askId,
        string machineCapability,
        string attachmentId,
        CancellationToken ct = default)
    {
        ValidateAttachmentId(attachmentId);
        await using var storeLock = await AcquireStoreLockAsync(ct).ConfigureAwait(false);
        await using var askLock = await AcquireAskLockAsync(askId, ct).ConfigureAwait(false);
        var state = await ReadStateAsync(askId, ct).ConfigureAwait(false);
        Authorize(state, machineCapability);
        EnsureNotExpired(state);
        if (!state.Attachments.TryGetValue(attachmentId, out var metadata))
        {
            throw new FileNotFoundException($"Attachment '{attachmentId}' was not found for ask '{askId}'.");
        }

        return await OpenVerifiedAttachmentReadStreamAsync(askId, metadata, ct).ConfigureAwait(false);
    }

    private async Task VerifyReferencedAttachmentContentAsync(
        AskScopedState state,
        IReadOnlyList<AskScopedNormalizedAnswer> answers,
        CancellationToken ct)
    {
        var verified = new HashSet<string>(StringComparer.Ordinal);
        foreach (var metadata in answers.SelectMany(static answer => answer.Attachments))
        {
            ct.ThrowIfCancellationRequested();
            if (!verified.Add(metadata.AttachmentId))
            {
                continue;
            }

            await using var stream = await OpenVerifiedAttachmentReadStreamAsync(state.AskId, metadata, ct).ConfigureAwait(false);
        }
    }

    private async Task<FileStream> OpenVerifiedAttachmentReadStreamAsync(
        string askId,
        AskScopedAttachmentMetadata metadata,
        CancellationToken ct)
    {
        var attachmentPath = GetAttachmentPath(askId, metadata.AttachmentId);
        EnsureNotReparsePoint(attachmentPath);
        var stream = new FileStream(
            attachmentPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        try
        {
            if (stream.Length != metadata.Length)
            {
                throw new InvalidOperationException($"Attachment '{metadata.AttachmentId}' has an unexpected stored length.");
            }

            var actualHash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(actualHash, Convert.FromHexString(metadata.Sha256)))
            {
                throw new InvalidOperationException($"Attachment '{metadata.AttachmentId}' failed its SHA-256 integrity check.");
            }

            stream.Position = 0;
            return stream;
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private long GetAvailableAttachmentQuota(AskScopedState state, AskScopedAttachmentMetadata projectedMetadata)
    {
        var askDirectory = GetAskDirectory(state.AskId);
        var statePath = Path.Combine(askDirectory, "state.json");
        var currentStateLength = new FileInfo(statePath).Length;
        var askBytes = GetDirectorySize(askDirectory);
        var storeBytes = GetDirectorySize(_rootDirectory);
        state.Attachments.Add(projectedMetadata.AttachmentId, projectedMetadata);
        long projectedStateLength;
        try
        {
            projectedStateLength = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions).LongLength;
        }
        finally
        {
            state.Attachments.Remove(projectedMetadata.AttachmentId);
        }

        var stateGrowth = Math.Max(0, projectedStateLength - currentStateLength);
        var askRemaining = GetRemainingBytes(_options.MaxAskBytes, askBytes, stateGrowth);
        var storeRemaining = GetRemainingBytes(_options.MaxStoreBytes, storeBytes, stateGrowth);
        return Math.Min(askRemaining, storeRemaining);
    }

    private static long GetRemainingBytes(long maximum, long used, long reserved)
    {
        if (used >= maximum || reserved >= maximum - used)
        {
            return 0;
        }

        return maximum - used - reserved;
    }

    private void ValidatePersistedAttachmentMetadata(AskScopedState state)
    {
        if (state.Attachments.Count > _options.MaxAttachmentsPerAsk)
        {
            throw new InvalidOperationException($"Ask '{state.AskId}' exceeds its configured attachment count limit.");
        }

        if (state.Attachments.Count == 0)
        {
            return;
        }

        var questions = BuildQuestionMap(state.Contract);
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
        if (UserInputContractValidator.Validate([askTransition]).Count > 0)
        {
            throw new InvalidOperationException($"Ask '{state.AskId}' contains an invalid user input contract.");
        }

        var attachmentDirectory = Path.Combine(GetAskDirectory(state.AskId), "attachments");
        if (!Directory.Exists(attachmentDirectory))
        {
            throw new InvalidOperationException($"Ask '{state.AskId}' is missing its attachment directory.");
        }

        EnsureNotReparsePoint(attachmentDirectory);
        foreach (var (attachmentId, metadata) in state.Attachments)
        {
            if (metadata is null
                || !IsValidAttachmentId(attachmentId)
                || !string.Equals(metadata.AttachmentId, attachmentId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(metadata.QuestionId)
                || !questions.TryGetValue(metadata.QuestionId, out var question)
                || question.Type is not (UserInputQuestionTypes.File or UserInputQuestionTypes.Audio)
                || !IsSafeAttachmentFileName(metadata.FileName)
                || string.IsNullOrWhiteSpace(metadata.MediaType)
                || !MediaTypeHeaderValue.TryParse(metadata.MediaType, out var parsedMediaType)
                || parsedMediaType is null
                || !string.Equals(parsedMediaType.MediaType, metadata.MediaType, StringComparison.OrdinalIgnoreCase)
                || metadata.Length <= 0
                || metadata.Length > _options.MaxAttachmentBytes
                || question.Constraints?.MaxAttachmentBytes is long questionLimit && metadata.Length > questionLimit
                || !IsSha256Hex(metadata.Sha256)
                || metadata.StoredAtUtc == default)
            {
                throw new InvalidOperationException($"Ask '{state.AskId}' contains invalid attachment metadata.");
            }

            var attachmentPath = Path.Combine(attachmentDirectory, $"{attachmentId}.bin");
            if (!File.Exists(attachmentPath))
            {
                throw new InvalidOperationException($"Attachment '{attachmentId}' is missing from ask storage.");
            }

            EnsureNotReparsePoint(attachmentPath);
            if (new FileInfo(attachmentPath).Length != metadata.Length)
            {
                throw new InvalidOperationException($"Attachment '{attachmentId}' has an unexpected stored length.");
            }

            EnsureAttachmentCanBeReferenced(
                state.Contract,
                question,
                metadata,
                state.Attachments);
        }
    }

    private static Dictionary<string, UserInputQuestion> BuildQuestionMap(UserInputContract contract)
    {
        if (contract.QuestionGroups is null)
        {
            throw new InvalidOperationException("The AskUser contract has no question groups.");
        }

        var questions = new Dictionary<string, UserInputQuestion>(StringComparer.Ordinal);
        foreach (var group in contract.QuestionGroups)
        {
            if (group?.Questions is null)
            {
                throw new InvalidOperationException("The AskUser contract contains an invalid question group.");
            }

            foreach (var question in group.Questions)
            {
                if (question is null
                    || string.IsNullOrWhiteSpace(question.Id)
                    || !questions.TryAdd(question.Id, question))
                {
                    throw new InvalidOperationException("The AskUser contract contains an invalid or duplicate question id.");
                }
            }
        }

        return questions;
    }

    private static void EnsureAttachmentCanBeReferenced(
        UserInputContract contract,
        UserInputQuestion question,
        AskScopedAttachmentMetadata metadata,
        IReadOnlyDictionary<string, AskScopedAttachmentMetadata> attachments)
    {
        var answers = new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
        {
            [question.Id] = new AskScopedAnswerValue { AttachmentIds = [metadata.AttachmentId] },
        };
        var validation = UserInputAnswerValidator.Validate(contract, answers, attachments, requireComplete: false);
        if (!validation.IsValid)
        {
            throw new AskScopedValidationException(validation.Diagnostics);
        }
    }

    private string GetAttachmentPath(string askId, string attachmentId)
    {
        ValidateAttachmentId(attachmentId);
        var askDirectory = GetAskDirectory(askId);
        EnsureNotReparsePoint(askDirectory);
        var attachmentDirectory = Path.Combine(askDirectory, "attachments");
        EnsureNotReparsePoint(attachmentDirectory);
        return Path.Combine(attachmentDirectory, $"{attachmentId}.bin");
    }

    private static string NormalizeAttachmentFileName(string questionId, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw CreateAttachmentValidationException(questionId, "An attachment file name is required.");
        }

        string normalized;
        try
        {
            normalized = fileName.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            throw CreateAttachmentValidationException(questionId, "The attachment file name is invalid.");
        }

        if (normalized.Length > 255
            || normalized != normalized.Trim()
            || normalized is "." or ".."
            || normalized.Contains('/')
            || normalized.Contains('\\')
            || normalized.Any(char.IsControl)
            || Path.IsPathRooted(normalized)
            || !string.Equals(Path.GetFileName(normalized), normalized, StringComparison.Ordinal))
        {
            throw CreateAttachmentValidationException(questionId, "The attachment file name must be a single safe file name.");
        }

        return normalized;
    }

    private static string NormalizeAttachmentMediaType(string questionId, string mediaType)
    {
        if (!MediaTypeHeaderValue.TryParse(mediaType, out var parsedMediaType)
            || parsedMediaType is null
            || string.IsNullOrWhiteSpace(parsedMediaType.MediaType))
        {
            throw CreateAttachmentValidationException(questionId, "The attachment media type is invalid.");
        }

        return parsedMediaType.MediaType;
    }

    private static bool IsSafeAttachmentFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Length > 255
            || fileName != fileName.Trim()
            || fileName is "." or ".."
            || fileName.Contains('/')
            || fileName.Contains('\\')
            || fileName.Any(char.IsControl)
            || Path.IsPathRooted(fileName))
        {
            return false;
        }

        try
        {
            return string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
                && string.Equals(fileName.Normalize(NormalizationForm.FormC), fileName, StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static AskScopedValidationException CreateAttachmentValidationException(string questionId, string message)
        => new([new UserInputAnswerDiagnostic($"questions.{questionId}", message)]);

    private static void ValidateAttachmentId(string attachmentId)
    {
        if (!IsValidAttachmentId(attachmentId))
        {
            throw new ArgumentException("Attachment IDs must be 32 hexadecimal characters.", nameof(attachmentId));
        }
    }

    private static bool IsValidAttachmentId(string? attachmentId)
        => attachmentId is { Length: 32 } && Guid.TryParseExact(attachmentId, "N", out _);
}
