using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class AskScopedSubmissionStoreOptions
{
    public string RootDirectory { get; init; } = ResolveRootDirectory();

    private static string ResolveRootDirectory()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("TECHNE_LOOM_ASK_STORE_ROOT");
        return string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Techne",
                "Loom",
                "asks")
            : configuredRoot;
    }

    public int MaxActiveAsks { get; init; } = 128;

    public long MaxAskBytes { get; init; } = 51 * 1024 * 1024;

    public long MaxStoreBytes { get; init; } = 256 * 1024 * 1024;

    public long MaxAttachmentBytes { get; init; } = 50 * 1024 * 1024;

    public int MaxAttachmentsPerAsk { get; init; } = 32;

    public TimeSpan DraftLifetime { get; init; } = TimeSpan.FromHours(24);

    public TimeSpan UnappliedReceiptLifetime { get; init; } = TimeSpan.FromDays(30);

    public TimeSpan AppliedReceiptLifetime { get; init; } = TimeSpan.FromDays(7);
}

internal sealed class AskScopedState
{
    public int SchemaVersion { get; init; } = 1;

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public AskScopedConsumerKind ConsumerKind { get; init; }

    public string AskId { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkflowInstanceId { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? TransitionId { get; init; }

    public string? WaitId { get; init; }

    public string? CorrelationKey { get; init; }

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
