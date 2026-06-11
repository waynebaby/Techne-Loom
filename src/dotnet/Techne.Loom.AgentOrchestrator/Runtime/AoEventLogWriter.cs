using System.Text.Json;
using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Runtime;

internal sealed class AoEventLogWriter
{
    private static readonly JsonSerializerOptions JsonOptions = WorkflowJsonSerializer.CreateDefaultOptions(indented: false);

    public async Task AppendAsync(string eventLogFile, AoEventRecord record)
    {
        EnsureParentDirectory(eventLogFile);
        var line = JsonSerializer.Serialize(record, JsonOptions);
        await File.AppendAllTextAsync(eventLogFile, line + Environment.NewLine).ConfigureAwait(false);
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
