using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public enum AskScopedConsumerKind
{
    WorkflowNode,
    Standalone,
}

public sealed class AskScopedAnswerValue
{
    public JsonElement? Value { get; init; }

    public string? FreeText { get; init; }

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
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? ContextPath,
    JsonElement? Value,
    bool Skipped,
    IReadOnlyList<AskScopedAttachmentMetadata> Attachments,
    string? FreeText = null);

public sealed record AskScopedSubmissionReceipt(
    int SchemaVersion,
    string AskId,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? WorkflowInstanceId,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? TransitionId,
    long PreviousGeneration,
    long Generation,
    string OperationId,
    string RequestHash,
    DateTimeOffset SubmittedAtUtc,
    IReadOnlyList<AskScopedNormalizedAnswer> Answers,
    string IntegrityHash,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    AskScopedConsumerKind ConsumerKind = AskScopedConsumerKind.WorkflowNode);

public sealed record AskScopedLaunch(
    string AskId,
    string MachineCapability,
    long Generation,
    DateTimeOffset ExpiresAtUtc);

public sealed record AskScopedSnapshot(
    string AskId,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? WorkflowInstanceId,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? TransitionId,
    long Generation,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    UserInputContract Contract,
    IReadOnlyDictionary<string, AskScopedAnswerValue> DraftAnswers,
    IReadOnlyList<AskScopedAttachmentMetadata> Attachments,
    AskScopedSubmissionReceipt? Receipt,
    DateTimeOffset? AppliedAtUtc,
    string? WaitId = null,
    string? CorrelationKey = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    AskScopedConsumerKind ConsumerKind = AskScopedConsumerKind.WorkflowNode);

public sealed record AskScopedStandaloneResult(
    string AskId,
    AskScopedConsumerKind ConsumerKind,
    bool Submitted,
    long Generation,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<AskScopedNormalizedAnswer> Answers,
    AskScopedSubmissionReceipt? Receipt);

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

public sealed record UserInputAnswerDiagnostic(string Location, string Message);
