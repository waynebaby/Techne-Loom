using System.Reflection;

namespace Techne.Loom.Common.Documentation;

public sealed record DocumentationBundleInstallOptions
{
    public string? BaseDirectory { get; init; }

    public string? TemporaryDirectory { get; init; }
}

public sealed record DocumentationBundleResult(
    string Version,
    string DocsRoot,
    string GuidePath,
    bool IsPartial,
    IReadOnlyList<string> Warnings);

public sealed class DocumentationBundleInstallException : InvalidOperationException
{
    public DocumentationBundleInstallException(string message)
        : base(message)
    {
    }

    public DocumentationBundleInstallException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public static class DocumentationBundleInstaller
{
    public static Task<DocumentationBundleResult> InstallAsync(
        Assembly assembly,
        string guideRelativePath,
        DocumentationBundleInstallOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        cancellationToken.ThrowIfCancellationRequested();

        var version = ResolveVersion(assembly);
        var guidePath = NormalizeRelativePath(guideRelativePath);
        var assemblyDirectory = Path.GetDirectoryName(Path.GetFullPath(assembly.Location));
        var candidateRoots = new[]
        {
            string.IsNullOrWhiteSpace(options?.BaseDirectory)
                ? null
                : Path.Combine(Path.GetFullPath(options.BaseDirectory), "docs", "en"),
            assemblyDirectory is null ? null : Path.Combine(assemblyDirectory, "docs", "en"),
            Path.Combine(Path.GetFullPath(AppContext.BaseDirectory), "docs", "en"),
            ResolveProcessDirectDocsRoot(),
        }
        .OfType<string>()
        .Where(Directory.Exists)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        DocumentationBundleInstallException? lastFailure = null;
        foreach (var candidateRoot in candidateRoots)
        {
            try
            {
                return Task.FromResult(LocateDirectDocs(candidateRoot, guidePath, version));
            }
            catch (DocumentationBundleInstallException exception)
            {
                lastFailure = exception;
            }
        }

        throw lastFailure ?? new DocumentationBundleInstallException(
            $"Runtime package documentation was not found under '{Path.Combine(Path.GetFullPath(AppContext.BaseDirectory), "docs", "en")}'. " +
            "The executable must be launched from a complete package that includes tools/<rid>/docs/en.");
    }

    private static DocumentationBundleResult LocateDirectDocs(string docsRoot, string guidePath, string version)
    {
        EnsureNoReparsePoint(docsRoot);
        var fullGuidePath = ResolveDestinationPath(docsRoot, guidePath);
        EnsureNoReparsePoint(fullGuidePath);
        if (!File.Exists(fullGuidePath) || !IsCurrentGuide(fullGuidePath, version))
        {
            throw new DocumentationBundleInstallException(
                $"The version-matched guide '{guidePath}' could not be located or verified under package docs root '{docsRoot}'.");
        }

        return new DocumentationBundleResult(version, Path.GetFullPath(docsRoot), fullGuidePath, false, []);
    }

    private static string? ResolveProcessDirectDocsRoot()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return null;
        }

        var processDirectory = Path.GetDirectoryName(Path.GetFullPath(processPath));
        return string.IsNullOrWhiteSpace(processDirectory)
            ? null
            : Path.Combine(processDirectory, "docs", "en");
    }

    private static string ResolveVersion(Assembly assembly)
    {
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var version = informationalVersion?.Split('+', 2)[0].Trim();
        if (string.IsNullOrWhiteSpace(version))
        {
            version = assembly.GetName().Version?.ToString();
        }

        if (string.IsNullOrWhiteSpace(version)
            || version is "." or ".."
            || version.Contains('/', StringComparison.Ordinal)
            || version.Contains('\\', StringComparison.Ordinal)
            || version.Any(Path.GetInvalidFileNameChars().Contains))
        {
            throw new DocumentationBundleInstallException($"Assembly '{assembly.FullName}' does not expose a safe package version.");
        }

        return version;
    }

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Contains(':', StringComparison.Ordinal))
        {
            throw new DocumentationBundleInstallException($"Documentation path '{path}' is not a safe relative path.");
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            throw new DocumentationBundleInstallException($"Documentation path '{path}' is not a safe relative path.");
        }

        return string.Join('/', segments);
    }

    private static string ResolveDestinationPath(string docsRoot, string relativePath)
    {
        var fullRoot = Path.GetFullPath(docsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(docsRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new DocumentationBundleInstallException($"Documentation path '{relativePath}' escapes the docs root.");
        }

        return fullPath;
    }

    private static bool IsCurrentGuide(string path, string version)
    {
        try
        {
            var content = File.ReadAllText(path);
            return content.Contains($"Version: {version}", StringComparison.Ordinal)
                && content.Contains($"Build: published package {version}", StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void EnsureNoReparsePoint(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new DocumentationBundleInstallException($"Documentation path '{path}' is a reparse point and cannot be used.");
            }
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
