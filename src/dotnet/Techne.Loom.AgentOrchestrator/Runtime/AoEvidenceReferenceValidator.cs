using Techne.Loom.AgentOrchestrator.Models;

namespace Techne.Loom.AgentOrchestrator.Runtime;

internal static class AoEvidenceReferenceValidator
{
    public static IReadOnlyList<AoEvidenceReference> Validate(
        IEnumerable<AoEvidenceReference?> references,
        string? evidenceRoot)
    {
        var materialized = references.ToArray();
        if (materialized.Any(static reference => reference is null))
        {
            return [];
        }

        var root = string.IsNullOrWhiteSpace(evidenceRoot)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(evidenceRoot);
        if (!Directory.Exists(root))
        {
            return [];
        }

        var validated = new List<AoEvidenceReference>(materialized.Length);
        foreach (var reference in materialized.Cast<AoEvidenceReference>())
        {
            if (!TryValidate(reference, root))
            {
                return [];
            }

            validated.Add(reference);
        }

        return validated;
    }

    private static bool TryValidate(AoEvidenceReference reference, string root)
    {
        if (string.IsNullOrWhiteSpace(reference.Path)
            || Path.IsPathFullyQualified(reference.Path)
            || reference.StartLine < 1
            || reference.EndLine < reference.StartLine
            || string.IsNullOrWhiteSpace(reference.Role))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(root, reference.Path));
            var relativePath = Path.GetRelativePath(root, fullPath);
            if (Path.IsPathFullyQualified(relativePath)
                || relativePath == ".."
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
            {
                return false;
            }

            var rootAttributes = File.GetAttributes(root);
            if (rootAttributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return false;
            }

            for (var cursor = fullPath; !string.IsNullOrWhiteSpace(cursor); cursor = Directory.GetParent(cursor)?.FullName)
            {
                if (File.GetAttributes(cursor).HasFlag(FileAttributes.ReparsePoint))
                {
                    return false;
                }

                if (string.Equals(cursor.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }

            if (!File.Exists(fullPath))
            {
                return false;
            }

            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            if (File.GetAttributes(fullPath).HasFlag(FileAttributes.ReparsePoint))
            {
                return false;
            }

            using var reader = new StreamReader(stream);
            var lineCount = 0;
            while (reader.ReadLine() is not null)
            {
                lineCount++;
            }

            return reference.EndLine <= lineCount;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
