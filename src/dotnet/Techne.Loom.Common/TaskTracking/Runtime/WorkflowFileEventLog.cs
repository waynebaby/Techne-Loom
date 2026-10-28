using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class WorkflowFileEventLog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    public static async Task AppendAsync(string workflowFile, WorkflowFileEventRecord record, CancellationToken ct = default)
    {
        var eventLogFile = CanonicalWorkflowFileStore.GetEventLogPath(workflowFile);
        var directory = Path.GetDirectoryName(eventLogFile);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(
            eventLogFile,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await writer.WriteLineAsync(JsonSerializer.Serialize(record, JsonOptions)).ConfigureAwait(false);
        await writer.FlushAsync(ct).ConfigureAwait(false);
    }
}