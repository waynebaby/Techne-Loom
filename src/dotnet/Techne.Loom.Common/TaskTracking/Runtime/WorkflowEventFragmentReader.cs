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
    [property: JsonPropertyName("truncation_reason")] string? TruncationReason);

public static class WorkflowEventFragmentReader
{
    public static async Task<WorkflowEventFragmentResult> ReadAsync(
        string workflowFile,
        WorkflowEventFragmentLimits? limits = null,
        CancellationToken ct = default)
    {
        var normalizedWorkflowFile = CanonicalWorkflowFileStore.NormalizePath(workflowFile);
        var eventLogFile = CanonicalWorkflowFileStore.GetEventLogPath(normalizedWorkflowFile);
        var effectiveLimits = limits ?? new WorkflowEventFragmentLimits();
        effectiveLimits.Validate();
        if (!File.Exists(normalizedWorkflowFile))
        {
            throw new FileNotFoundException("The workflow file was not found.", normalizedWorkflowFile);
        }

        await using var workflowLock = await WorkflowFileLock.AcquireAsync(normalizedWorkflowFile, ct).ConfigureAwait(false);
        if (!File.Exists(eventLogFile))
        {
            return new WorkflowEventFragmentResult(eventLogFile, [], 0, false, null);
        }

        await using var stream = new FileStream(
            eventLogFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 16 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lines = new Queue<string>();
        var bytes = 0;
        var truncated = false;
        string? truncationReason = null;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            var lineBytes = Encoding.UTF8.GetByteCount(line) + 1;
            if (lineBytes > effectiveLimits.MaxBytes)
            {
                lines.Clear();
                bytes = 0;
                truncated = true;
                truncationReason = "max_bytes";
                continue;
            }

            lines.Enqueue(line);
            bytes += lineBytes;
            while (lines.Count > effectiveLimits.MaxEvents)
            {
                bytes -= Encoding.UTF8.GetByteCount(lines.Dequeue()) + 1;
                truncated = true;
                truncationReason ??= "max_events";
            }

            while (bytes > effectiveLimits.MaxBytes && lines.Count > 0)
            {
                bytes -= Encoding.UTF8.GetByteCount(lines.Dequeue()) + 1;
                truncated = true;
                truncationReason = "max_bytes";
            }
        }

        var events = new List<JsonElement>(lines.Count);
        foreach (var line in lines)
        {
            try
            {
                using var document = JsonDocument.Parse(line, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow });
                events.Add(document.RootElement.Clone());
            }
            catch (JsonException)
            {
                truncated = true;
                truncationReason ??= "invalid_event";
            }
        }

        var returnedBytes = events.Count == 0
            ? 0
            : events.Sum(eventItem => Encoding.UTF8.GetByteCount(eventItem.GetRawText()) + 1);
        if (returnedBytes > effectiveLimits.MaxBytes)
        {
            return new WorkflowEventFragmentResult(eventLogFile, [], 0, true, "max_bytes");
        }

        return new WorkflowEventFragmentResult(eventLogFile, events, returnedBytes, truncated, truncationReason);
    }
}
