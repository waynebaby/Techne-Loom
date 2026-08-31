using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class CanonicalWorkflowFileStore
{
    public static string NormalizePath(string workflowFile)
    {
        if (string.IsNullOrWhiteSpace(workflowFile))
        {
            throw new ArgumentException("A workflow file is required.", nameof(workflowFile));
        }

        return Path.GetFullPath(workflowFile);
    }

    public static string GetEventLogPath(string workflowFile)
        => NormalizePath(workflowFile) + ".events.jsonl";

    public static async Task<WorkflowInstance> LoadAsync(string workflowFile, CancellationToken ct = default)
    {
        var normalizedPath = NormalizePath(workflowFile);
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("The workflow file was not found.", normalizedPath);
        }

        var json = await File.ReadAllTextAsync(normalizedPath, ct).ConfigureAwait(false);
        return WorkflowJsonSerializer.Deserialize(json);
    }

    public static async Task SaveAsync(string workflowFile, WorkflowInstance instance, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var normalizedPath = NormalizePath(workflowFile);
        var directory = Path.GetDirectoryName(normalizedPath)
            ?? throw new InvalidOperationException($"Workflow file '{normalizedPath}' must have a parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(normalizedPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var json = WorkflowJsonSerializer.Serialize(instance);
            await File.WriteAllTextAsync(temporaryPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct).ConfigureAwait(false);
            File.Move(temporaryPath, normalizedPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}