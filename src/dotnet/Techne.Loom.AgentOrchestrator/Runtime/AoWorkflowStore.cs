using System.Text.Json;
using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Runtime;

internal sealed class AoWorkflowStore
{
    private static readonly JsonSerializerOptions JsonOptions = WorkflowJsonSerializer.CreateDefaultOptions(indented: true);

    public async Task SaveAsync(string workflowFile, AoWorkflowSnapshot snapshot)
    {
        EnsureParentDirectory(workflowFile);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await File.WriteAllTextAsync(workflowFile, json).ConfigureAwait(false);
    }

    public async Task<AoWorkflowSnapshot> LoadAsync(string workflowFile)
    {
        var json = await File.ReadAllTextAsync(workflowFile).ConfigureAwait(false);
        return JsonSerializer.Deserialize<AoWorkflowSnapshot>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to parse workflow snapshot '{workflowFile}'.");
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
