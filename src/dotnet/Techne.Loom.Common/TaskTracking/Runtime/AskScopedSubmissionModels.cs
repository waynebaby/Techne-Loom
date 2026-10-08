using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class AskScopedSubmissionStoreOptions
{
    public string RootDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Techne",
        "Loom",
        "asks");

    public int MaxActiveAsks { get; init; } = 128;

    public long MaxAskBytes { get; init; } = 16 * 1024 * 1024;

    public long MaxStoreBytes { get; init; } = 256 * 1024 * 1024;

    public long MaxAttachmentBytes { get; init; } = 8 * 1024 * 1024;

    public int MaxAttachmentsPerAsk { get; init; } = 32;

    public TimeSpan DraftLifetime { get; init; } = TimeSpan.FromHours(24);

    public TimeSpan UnappliedReceiptLifetime { get; init; } = TimeSpan.FromDays(30);

    public TimeSpan AppliedReceiptLifetime { get; init; } = TimeSpan.FromDays(7);
}

public sealed class AskScopedAnswerValue
{
    public JsonElement? Value { get; init; }

    public bool Skipped { get; init; }

    public List<string> AttachmentIds { get; init; } = [];
}

public sealed record AskScopedAttachmentMetadata(
    string AttachmentId,
    string QuestionId,
    string FileName,
    string MediaType,
    long Length,
    string Sha256,
    DateTimeOffset StoredAtUtc);

public sealed record AskScopedAttachmentUploadResult(
    AskScopedAttachmentMetadata Attachment,
    long Generation);

public sealed record AskScopedNormalizedAnswer(
    string QuestionId,
    string ContextPath,
    JsonElement? Value,
    bool Skipped,
    IReadOnlyList<AskScopedAttachmentMetadata> Attachments);

public sealed record AskScopedSubmissionReceipt(
    int SchemaVersion,
    string AskId,
    string WorkflowInstanceId,
    string TransitionId,
    long PreviousGeneration,
    long Generation,
    string OperationId,
    string RequestHash,
    DateTimeOffset SubmittedAtUtc,
    IReadOnlyList<AskScopedNormalizedAnswer> Answers,
    string IntegrityHash);

public sealed record AskScopedLaunch(
    string AskId,
    string MachineCapability,
    long Generation,
    DateTimeOffset ExpiresAtUtc);

public sealed record AskScopedSnapshot(
    string AskId,
    string WorkflowInstanceId,
    string TransitionId,
    long Generation,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    UserInputContract Contract,
    IReadOnlyDictionary<string, AskScopedAnswerValue> DraftAnswers,
    IReadOnlyList<AskScopedAttachmentMetadata> Attachments,
    AskScopedSubmissionReceipt? Receipt,
    DateTimeOffset? AppliedAtUtc);

public sealed record AskScopedCleanupResult(int RemovedAsks, long FreedBytes);

public sealed class AskScopedConflictException : InvalidOperationException
{
    public AskScopedConflictException(string message) : base(message)
    {
    }
}

public sealed class AskScopedExpiredException : InvalidOperationException
{
    public AskScopedExpiredException(string askId) : base($"Ask '{askId}' has expired.")
    {
    }
}

public sealed class AskScopedValidationException : InvalidOperationException
{
    public AskScopedValidationException(IReadOnlyList<UserInputAnswerDiagnostic> diagnostics)
        : base(string.Join(Environment.NewLine, diagnostics.Select(static item => $"{item.Location}: {item.Message}")))
    {
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<UserInputAnswerDiagnostic> Diagnostics { get; }
}

internal sealed class AskScopedState
{
    public int SchemaVersion { get; init; } = 1;

    public string AskId { get; init; } = string.Empty;

    public string WorkflowInstanceId { get; init; } = string.Empty;

    public string TransitionId { get; init; } = string.Empty;

    public string MachineCapabilityHash { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public long Generation { get; set; }

    public UserInputContract Contract { get; init; } = new();

    public Dictionary<string, AskScopedAnswerValue> DraftAnswers { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, AskScopedAttachmentMetadata> Attachments { get; set; } = new(StringComparer.Ordinal);

    public AskScopedSubmissionReceipt? Receipt { get; set; }

    public DateTimeOffset? AppliedAtUtc { get; set; }
}

public sealed record UserInputAnswerDiagnostic(string Location, string Message);
