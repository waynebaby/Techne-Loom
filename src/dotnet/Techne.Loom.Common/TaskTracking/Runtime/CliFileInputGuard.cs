namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class CliFileInputGuard
{
    public static IReadOnlyDictionary<string, string> RequireExistingFiles(
        params (string OptionName, string? Path)[] requirements)
    {
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (optionName, path) in requirements)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            resolved[optionName] = RequireExistingFile(path, optionName);
        }

        return resolved;
    }

    public static string RequireExistingFile(string path, string optionName)
    {
        if (string.IsNullOrWhiteSpace(optionName))
        {
            throw new ArgumentException("A file option name is required.", nameof(optionName));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"File option '{optionName}' requires a path to an existing file.");
        }

        string fullPath;
        try
        {
            fullPath = System.IO.Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidOperationException($"File option '{optionName}' received an invalid file path.", exception);
        }

        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"File option '{optionName}' requires an existing file, but '{fullPath}' was not found.");
        }

        if (Directory.Exists(fullPath))
        {
            throw new InvalidOperationException($"File option '{optionName}' requires a file, but '{fullPath}' is a directory.");
        }

        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new InvalidOperationException($"File option '{optionName}' requires a readable file, but '{fullPath}' could not be opened.", exception);
        }

        return fullPath;
    }

    public static void RejectInlineContentOptions(
        IReadOnlyList<string> args,
        string operation,
        params string[] optionNames)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(optionNames);

        var rejected = optionNames.ToHashSet(StringComparer.Ordinal);
        foreach (var argument in args)
        {
            if (!rejected.Contains(argument))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"{operation} accepts file paths only. '{argument}' is inline content and is not supported. Prepare every input file on disk before starting the command.");
        }
    }
}
