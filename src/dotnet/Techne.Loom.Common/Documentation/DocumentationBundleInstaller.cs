using System.Reflection;
using System.IO.Compression;

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
    public const string ResourceNameSuffix = "Techne.Loom.DocsBundle.zip";

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
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(ResourceNameSuffix, StringComparison.Ordinal));

        if (resourceName is null)
        {
            throw new DocumentationBundleInstallException(
                $"Embedded documentation bundle '{ResourceNameSuffix}' was not found in assembly '{assembly.FullName}'.");
        }

        var baseDirectory = Path.GetFullPath(options?.BaseDirectory ?? AppContext.BaseDirectory);
        var temporaryDirectory = Path.GetFullPath(options?.TemporaryDirectory ?? Path.GetTempPath());
        var primaryDocsRoot = Path.Combine(baseDirectory, "docs");
        var fallbackDocsRoot = Path.Combine(temporaryDirectory, "docs");
        var primaryRoot = Path.Combine(primaryDocsRoot, version);
        var fallbackRoot = Path.Combine(fallbackDocsRoot, version);
        EnsureSafeDirectoryChain(baseDirectory, primaryDocsRoot);
        var warnings = new List<string>();
        DocumentationBundleInstallAttempt attempt;
        var actualRoot = primaryRoot;
        try
        {
            attempt = InstallToRoot(assembly, resourceName, primaryRoot, guidePath, version, cancellationToken);
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            warnings.Add($"The binary docs directory '{primaryRoot}' was not writable; documentation was installed under '{fallbackRoot}'.");
            EnsureSafeDirectoryChain(temporaryDirectory, fallbackDocsRoot);
            attempt = InstallToRoot(assembly, resourceName, fallbackRoot, guidePath, version, cancellationToken);
            actualRoot = fallbackRoot;
        }

        if (actualRoot == primaryRoot && attempt.RequiresFallback)
        {
            warnings.Add($"The binary docs directory '{primaryRoot}' was not writable; documentation was installed under '{fallbackRoot}'.");
            EnsureSafeDirectoryChain(temporaryDirectory, fallbackDocsRoot);
            attempt = InstallToRoot(assembly, resourceName, fallbackRoot, guidePath, version, cancellationToken);
            actualRoot = fallbackRoot;
        }


        warnings.AddRange(attempt.Warnings);
        if (!attempt.GuideAvailable)
        {
            throw new DocumentationBundleInstallException(
                $"The version-matched guide '{guidePath}' could not be installed or verified under '{actualRoot}'.");
        }

        var result = new DocumentationBundleResult(
            version,
            actualRoot,
            Path.Combine(actualRoot, guidePath.Replace('/', Path.DirectorySeparatorChar)),
            warnings.Count > 0,
            warnings);
        return Task.FromResult(result);
    }

    private static DocumentationBundleInstallAttempt InstallToRoot(
        Assembly assembly,
        string resourceName,
        string targetRoot,
        string guidePath,
        string version,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(targetRoot);
        EnsureNoReparsePoint(targetRoot);
        using var installLock = AcquireInstallLock(targetRoot, cancellationToken);
        var pathComparer = GetPathComparer();
        var expectedFiles = new HashSet<string>(pathComparer);
        var warnings = new List<string>();
        var requiresFallback = false;
        var guideEntryFound = false;
        var guideAvailable = false;
        var writeFailures = false;

        using var resourceStream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new DocumentationBundleInstallException($"Unable to open embedded documentation bundle '{resourceName}'.");
        using var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read, leaveOpen: false);

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var relativePath = NormalizeRelativePath(entry.FullName);
            var destinationPath = ResolveDestinationPath(targetRoot, relativePath);
            expectedFiles.Add(relativePath);
            var isGuide = pathComparer.Equals(relativePath, guidePath);
            guideEntryFound |= isGuide;
            try
            {
                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    EnsureSafeDirectoryChain(targetRoot, destinationDirectory);
                    Directory.CreateDirectory(destinationDirectory);
                    EnsureSafeDirectoryChain(targetRoot, destinationDirectory);
                }

                EnsureNoReparsePoint(destinationPath);
                using (var input = entry.Open())
                using (var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    input.CopyTo(output);
                    output.Flush(flushToDisk: true);
                }

                if (isGuide)
                {
                    guideAvailable = IsCurrentGuide(destinationPath, entry.Length, version);
                }
            }
            catch (Exception ex) when (IsPerFileFailure(ex))
            {
                writeFailures = true;
                requiresFallback |= IsStorageFailure(ex);
                if (isGuide)
                {
                    guideAvailable = IsCurrentGuide(destinationPath, entry.Length, version);
                }

                warnings.Add($"Unable to install documentation file '{relativePath}': {ex.Message}");
            }
        }

        if (!guideEntryFound)
        {
            throw new DocumentationBundleInstallException($"Embedded documentation bundle does not contain the required guide '{guidePath}'.");
        }

        if (!guideAvailable)
        {
            return new DocumentationBundleInstallAttempt(requiresFallback, false, warnings);
        }

        if (!writeFailures)
        {
            CleanupStaleFiles(targetRoot, expectedFiles, warnings);
        }

        return new DocumentationBundleInstallAttempt(requiresFallback, true, warnings);
    }

    private static void CleanupStaleFiles(
        string targetRoot,
        IReadOnlySet<string> expectedFiles,
        ICollection<string> warnings)
    {
        var directories = EnumerateOwnedDirectories(targetRoot, warnings);
        foreach (var file in EnumerateOwnedFiles(targetRoot, directories, warnings))
        {
            var relativePath = NormalizeRelativePath(Path.GetRelativePath(targetRoot, file));
            if (expectedFiles.Contains(relativePath))
            {
                continue;
            }

            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (IsPerFileFailure(ex))
            {
                warnings.Add($"Unable to remove stale documentation file '{relativePath}': {ex.Message}");
            }
        }

        foreach (var directory in directories.OrderByDescending(path => path.Length))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch (Exception ex) when (IsPerFileFailure(ex))
            {
                warnings.Add($"Unable to remove stale documentation directory '{Path.GetRelativePath(targetRoot, directory)}': {ex.Message}");
            }
        }
    }

    private static List<string> EnumerateOwnedDirectories(string targetRoot, ICollection<string> warnings)
    {
        EnsureNoReparsePoint(targetRoot);
        var directories = new List<string>();
        var pending = new Stack<string>();
        pending.Push(targetRoot);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            string[] children;
            try
            {
                children = Directory.GetDirectories(current);
            }
            catch (Exception ex) when (IsPerFileFailure(ex))
            {
                warnings.Add($"Unable to inspect documentation directory '{Path.GetRelativePath(targetRoot, current)}': {ex.Message}");
                continue;
            }

            foreach (var child in children)
            {
                if (IsReparsePoint(child))
                {
                    warnings.Add($"Skipped reparse-point documentation directory '{Path.GetRelativePath(targetRoot, child)}'.");
                    continue;
                }

                directories.Add(child);
                pending.Push(child);
            }
        }

        return directories;
    }

    private static IEnumerable<string> EnumerateOwnedFiles(
        string targetRoot,
        IReadOnlyList<string> directories,
        ICollection<string> warnings)
    {
        foreach (var directory in directories.Append(targetRoot))
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(directory);
            }
            catch (Exception ex) when (IsPerFileFailure(ex))
            {
                warnings.Add($"Unable to inspect documentation files under '{Path.GetRelativePath(targetRoot, directory)}': {ex.Message}");
                continue;
            }

            foreach (var file in files)
            {
                if (IsReparsePoint(file))
                {
                    warnings.Add($"Skipped reparse-point documentation file '{Path.GetRelativePath(targetRoot, file)}'.");
                    continue;
                }

                yield return file;
            }
        }
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

    private static string ResolveDestinationPath(string targetRoot, string relativePath)
    {
        var fullRoot = Path.GetFullPath(targetRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(targetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new DocumentationBundleInstallException($"Documentation path '{relativePath}' escapes the docs root.");
        }

        return fullPath;
    }

    private static bool IsCurrentGuide(string path, long expectedLength, string version)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != expectedLength)
        {
            return false;
        }

        try
        {
            var content = File.ReadAllText(path);
            var hasVersion = content.Contains($"Version: {version}", StringComparison.Ordinal);
            var hasBuild = content.Contains($"Build: published package {version}", StringComparison.Ordinal);
            return hasVersion && hasBuild;
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

    private static StringComparer GetPathComparer()
        => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static void EnsureSafeDirectoryChain(string targetRoot, string directory)
    {
        var fullRoot = Path.GetFullPath(targetRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = Path.GetFullPath(directory);
        while (current.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            EnsureNoReparsePoint(current);
            if (string.Equals(current, fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            current = Directory.GetParent(current)?.FullName
                ?? throw new DocumentationBundleInstallException($"Unable to validate documentation directory '{directory}'.");
        }

        throw new DocumentationBundleInstallException($"Documentation directory '{directory}' escapes the docs root.");
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

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static FileStream AcquireInstallLock(string targetRoot, CancellationToken cancellationToken)
    {
        var lockPath = targetRoot + ".install.lock";
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(50);
            }
        }
    }

    private static bool IsPerFileFailure(Exception exception)
        => exception is IOException or UnauthorizedAccessException or NotSupportedException;

    private static bool IsStorageFailure(Exception exception)
        => exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException;

    private sealed record DocumentationBundleInstallAttempt(
        bool RequiresFallback,
        bool GuideAvailable,
        IReadOnlyList<string> Warnings);
}
