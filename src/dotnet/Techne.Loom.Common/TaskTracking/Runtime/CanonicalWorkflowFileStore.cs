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

    public const string SkillFileNameErrorCode = "LOOM.SKILL_FILE_NAME";
    public const string SkillFileNameSuggestedFix = "Name each skill workflow template and execution copy '<ownerSkillSlug>--<purpose>.json' and set ownerSkillSlug to the exact skill directory slug.";

    public static string? GetSkillFileNameError(WorkflowInstance instance, string? workflowFile = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var pathOwnerSkillSlug = string.IsNullOrWhiteSpace(workflowFile) ? null : GetSkillSlugFromPath(workflowFile);
        if (!WorkflowIdentityContract.IsSkillOwnedWorkflow(instance) && pathOwnerSkillSlug is null)
        {
            return null;
        }

        var ownerSkillSlug = instance.OwnerSkillSlug;
        if (string.IsNullOrWhiteSpace(ownerSkillSlug))
        {
            return "Skill-owned workflow templates and instances must declare ownerSkillSlug.";
        }

        if (!IsValidSkillSlug(ownerSkillSlug))
        {
            return $"ownerSkillSlug '{ownerSkillSlug}' must be a lowercase kebab-case skill directory slug.";
        }

        if (pathOwnerSkillSlug is not null && !string.Equals(pathOwnerSkillSlug, ownerSkillSlug, StringComparison.Ordinal))
        {
            return $"ownerSkillSlug '{ownerSkillSlug}' must match skill directory '{pathOwnerSkillSlug}'.";
        }

        if (string.IsNullOrWhiteSpace(workflowFile))
        {
            return null;
        }

        var fileName = Path.GetFileName(workflowFile);
        var prefix = ownerSkillSlug + "--";
        if (!fileName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return $"Workflow file '{fileName}' must begin with '{prefix}' to match ownerSkillSlug.";
        }

        if (!fileName.EndsWith(".json", StringComparison.Ordinal))
        {
            return $"Workflow file '{fileName}' must use the '.json' extension.";
        }

        if (fileName.Length <= prefix.Length + ".json".Length)
        {
            return $"Workflow file '{fileName}' must include a non-empty purpose before the '.json' extension.";
        }

        return null;
    }

    public static void EnsureSkillFileName(WorkflowInstance instance, string workflowFile)
    {
        var error = GetSkillFileNameError(instance, workflowFile);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }
    }

    private static bool IsValidSkillSlug(string value)
    {
        if (value.Length == 0 || value[0] == '-' || value[^1] == '-' || value.Contains("--", StringComparison.Ordinal))
        {
            return false;
        }

        return value.All(static character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    }

    private static string? GetSkillSlugFromPath(string workflowFile)
    {
        var pathSegments = Path.GetFullPath(workflowFile)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        for (var index = 1; index < pathSegments.Length - 1; index++)
        {
            var isSkillDirectory = string.Equals(pathSegments[index], "skills", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(pathSegments[index - 1], ".agents", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pathSegments[index - 1], ".github", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pathSegments[index - 1], ".claude", StringComparison.OrdinalIgnoreCase));
            if (isSkillDirectory)
            {
                return pathSegments[index + 1];
            }
        }

        return null;
    }

    public static async Task<WorkflowInstance> LoadAsync(string workflowFile, CancellationToken ct = default)
    {
        var normalizedPath = NormalizePath(workflowFile);
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("The workflow file was not found.", normalizedPath);
        }

        var json = await File.ReadAllTextAsync(normalizedPath, ct).ConfigureAwait(false);
        var instance = WorkflowJsonSerializer.Deserialize(json);
        EnsureSkillFileName(instance, normalizedPath);
        return instance;
    }

    public static async Task SaveAsync(string workflowFile, WorkflowInstance instance, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var normalizedPath = NormalizePath(workflowFile);
        EnsureSkillFileName(instance, normalizedPath);
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