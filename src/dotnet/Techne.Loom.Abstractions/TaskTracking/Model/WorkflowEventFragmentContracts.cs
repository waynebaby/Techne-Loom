using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record WorkflowEventFragmentLimits(
    int MaxBytes = 32_768,
    int MaxEvents = 32)
{
    public void Validate()
    {
        if (MaxBytes < 128)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxBytes), "MaxBytes must be at least 128.");
        }

        if (MaxEvents < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxEvents), "MaxEvents must be positive.");
        }
    }
}

public sealed record WorkflowEventFragmentResult(
    [property: JsonPropertyName("event_log_file")] string EventLogFile,
    [property: JsonPropertyName("events")] IReadOnlyList<JsonElement> Events,
    [property: JsonPropertyName("returned_bytes")] int ReturnedBytes,
    [property: JsonPropertyName("truncated")] bool Truncated,
    [property: JsonPropertyName("truncation_reason")] string? TruncationReason,
    [property: JsonPropertyName("operation_id")] string? OperationId = null);
