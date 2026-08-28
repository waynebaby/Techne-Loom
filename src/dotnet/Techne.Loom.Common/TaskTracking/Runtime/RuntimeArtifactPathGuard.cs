namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class RuntimeArtifactPathGuard
{
    public static void EnsureOutputFileOutsideSkillDirectory(string outputFile, string optionName)
    {
        EnsureOutsideSkillDirectory(outputFile, treatAsDirectory: false, optionName, "Output files");
    }

    public static void EnsureRuntimeWorkflowFileOutsideSkillDirectory(string workflowFile, string optionName = "--workflow-file")
    {
        EnsureOutsideSkillDirectory(workflowFile, treatAsDirectory: false, optionName, "Runtime workflow files");
    }

    public static void EnsureSessionDirectoryOutsideSkillDirectory(string sessionDirectory, string optionName = "--session-dir")
    {
        EnsureOutsideSkillDirectory(sessionDirectory, treatAsDirectory: true, optionName, "AO session directories");
    }

    public static void EnsureAuditOutputOutsideSkillDirectory(string? auditOutputRoot, string optionName = "--audit-output")
    {
        if (string.IsNullOrWhiteSpace(auditOutputRoot))
        {
            return;
        }

        EnsureOutsideSkillDirectory(auditOutputRoot, treatAsDirectory: true, optionName, "Audit output roots");
    }

    public static void EnsureWorkspaceRootOutsideSkillDirectory(string? workspaceRoot, string optionName = "--workspace-root")
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            return;
        }

        EnsureOutsideSkillDirectory(workspaceRoot, treatAsDirectory: true, optionName, "Mermaid workspace roots");
        var fullPath = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(fullPath))
        {
            throw new InvalidOperationException($"Option '{optionName}' must point to an existing directory. Resolved path: '{fullPath}'.");
        }
    }

    private static void EnsureOutsideSkillDirectory(string path, bool treatAsDirectory, string optionName, string purpose)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"Option '{optionName}' requires a non-empty path.");
        }

        var fullPath = Path.GetFullPath(path);
        var directoryToInspect = treatAsDirectory
            ? fullPath
            : Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException($"Unable to resolve parent directory for '{fullPath}'.");

        var skillRoot = FindOwningSkillRoot(directoryToInspect);
        if (skillRoot is null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{purpose} cannot be placed inside the skill-owned directory '{skillRoot}'. " +
            $"Option '{optionName}' resolved to '{fullPath}'. Explicit output roots still must stay outside the skill folder. Copy the source template into a runtime temp folder or explicit execution output root, and keep runtime outputs outside the skill folder.");
    }

    private static string? FindOwningSkillRoot(string directoryPath)
    {
        DirectoryInfo? current = new(directoryPath);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "SKILL.md")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}