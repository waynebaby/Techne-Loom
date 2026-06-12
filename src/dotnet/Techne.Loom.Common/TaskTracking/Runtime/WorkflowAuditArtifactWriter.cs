using System.Text;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class WorkflowAuditArtifactWriter
{
    private static readonly Lazy<string> DefaultTemporaryOutputRoot = new(CreateDefaultTemporaryOutputRoot, LazyThreadSafetyMode.ExecutionAndPublication);

    public static async Task<WorkflowAuditArtifacts> WriteAsync(
        string workflowId,
        int sequence,
        string action,
        string workflowJson,
        string mermaidMarkdown,
        string html,
        string? outputRoot = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
        {
            throw new InvalidOperationException("A non-empty workflow identifier is required for audit output.");
        }

        var normalizedAction = SanitizeSegment(action, "action");
        var normalizedOutputRoot = ResolveOutputRoot(outputRoot);
        var stepDirectory = Path.Combine(
            normalizedOutputRoot,
            $"wf-{SanitizeSegment(workflowId, "wf")}",
            $"step-{Math.Max(sequence, 1):D4}-{normalizedAction}");

        Directory.CreateDirectory(stepDirectory);

        var mermaidFile = Path.Combine(stepDirectory, "workflow.mermaid.md");
        var htmlFile = Path.Combine(stepDirectory, "workflow.html");
        var workflowBackupFile = Path.Combine(stepDirectory, "workflow.json");

        var existingFiles = new[] { mermaidFile, htmlFile, workflowBackupFile }
            .Where(File.Exists)
            .ToArray();

        if (existingFiles.Length > 0)
        {
            throw new InvalidOperationException(
                $"Refusing to overwrite existing audit artifacts for workflow '{workflowId}' at '{stepDirectory}'. " +
                $"Existing files: {string.Join(", ", existingFiles.Select(path => $"'{path}'"))}. " +
                "Choose a different audit output root, clean the existing step directory, or let the runtime use a temporary output root.");
        }

        await File.WriteAllTextAsync(mermaidFile, mermaidMarkdown, Encoding.UTF8, ct).ConfigureAwait(false);
        await File.WriteAllTextAsync(htmlFile, html, Encoding.UTF8, ct).ConfigureAwait(false);
        await File.WriteAllTextAsync(workflowBackupFile, workflowJson, Encoding.UTF8, ct).ConfigureAwait(false);

        return new WorkflowAuditArtifacts(
            normalizedOutputRoot,
            workflowId,
            Math.Max(sequence, 1),
            normalizedAction,
            stepDirectory,
            mermaidFile,
            htmlFile,
            workflowBackupFile);
    }

    public static string ResolveOutputRoot(string? outputRoot)
    {
        var root = string.IsNullOrWhiteSpace(outputRoot)
            ? DefaultTemporaryOutputRoot.Value
            : outputRoot;

        var normalizedRoot = Path.GetFullPath(root);
        Directory.CreateDirectory(normalizedRoot);
        return normalizedRoot;
    }

    private static string CreateDefaultTemporaryOutputRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "techne-loom-audit",
            $"exec-{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}-{Environment.ProcessId}-{Guid.NewGuid():N}");
    }

    private static string SanitizeSegment(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        var normalized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }
}
