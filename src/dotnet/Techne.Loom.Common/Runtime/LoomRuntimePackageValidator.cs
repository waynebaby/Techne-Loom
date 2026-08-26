using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Techne.Loom.Common.Runtime;

public static class LoomRuntimePackageValidator
{
    private const string ManifestSchema = "techne-loom-runtime-v1";

    public static LoomRuntimePackageValidationResult Validate(
        ReadOnlyMemory<byte> packageBytes,
        LoomRuntimeProduct product,
        string version,
        string runtimeIdentifier,
        LoomRuntimePackageLimits? limits = null)
    {
        var normalizedVersion = LoomRuntimeCatalog.NormalizeVersion(version);
        LoomRuntimeCatalog.EnsureSupportedRuntimeIdentifier(runtimeIdentifier);
        var packageId = LoomRuntimeCatalog.GetPackageId(product, runtimeIdentifier);
        var expectedEntryPointName = LoomRuntimeCatalog.GetEntryFile(product, runtimeIdentifier);
        var packageLimits = limits ?? new LoomRuntimePackageLimits();

        if (packageBytes.Length > packageLimits.MaxArchiveBytes)
        {
            throw new LoomRuntimeIntegrityException($"Runtime package '{packageId}' exceeds the archive size limit of {packageLimits.MaxArchiveBytes} bytes.");
        }

        using var archive = OpenArchive(packageBytes, packageId);
        var entries = archive.Entries.ToArray();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        long totalUncompressedBytes = 0;

        foreach (var entry in entries)
        {
            var normalizedPath = NormalizeEntryPath(entry.FullName);
            if (!paths.Add(normalizedPath))
            {
                throw new LoomRuntimeIntegrityException($"Runtime package '{packageId}' contains a duplicate ZIP path '{normalizedPath}'.");
            }

            if (entry.Length > packageLimits.MaxEntryBytes)
            {
                throw new LoomRuntimeIntegrityException($"Runtime package '{packageId}' entry '{normalizedPath}' exceeds the entry size limit of {packageLimits.MaxEntryBytes} bytes.");
            }

            totalUncompressedBytes = checked(totalUncompressedBytes + entry.Length);
            if (totalUncompressedBytes > packageLimits.MaxTotalUncompressedBytes)
            {
                throw new LoomRuntimeIntegrityException($"Runtime package '{packageId}' exceeds the total uncompressed size limit of {packageLimits.MaxTotalUncompressedBytes} bytes.");
            }
        }

        var nuspecEntries = entries.Where(entry =>
            !entry.FullName.Contains('/', StringComparison.Ordinal) &&
            entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (nuspecEntries.Length == 0)
        {
            throw new LoomRuntimeIntegrityException($"Runtime package '{packageId}' does not contain a root nuspec.");
        }
        if (nuspecEntries.Length != 1)
        {
            throw new LoomRuntimeIntegrityException($"Runtime package '{packageId}' contains multiple root nuspec files.");
        }
        var nuspecEntry = nuspecEntries[0];

        ValidateNuspec(nuspecEntry, packageId, normalizedVersion, runtimeIdentifier);

        var manifestPath = $"tools/{runtimeIdentifier}/runtime.json";
        var manifestEntries = entries.Where(entry => string.Equals(entry.FullName, manifestPath, StringComparison.Ordinal)).ToArray();
        if (manifestEntries.Length == 0)
        {
            throw new LoomRuntimeIntegrityException($"Runtime package '{packageId}' is missing its fixed manifest '{manifestPath}'.");
        }
        if (manifestEntries.Length != 1)
        {
            throw new LoomRuntimeIntegrityException($"Runtime package '{packageId}' contains multiple fixed manifests at '{manifestPath}'.");
        }
        var manifestEntry = manifestEntries[0];

        if (manifestEntry.Length > packageLimits.MaxManifestBytes)
        {
            throw new LoomRuntimeIntegrityException($"Runtime package '{packageId}' manifest exceeds the size limit of {packageLimits.MaxManifestBytes} bytes.");
        }

        var expectedDocsRoot = $"tools/{runtimeIdentifier}/docs/en";
        var expectedGuidePath = $"guides/{LoomRuntimeCatalog.GetEntryPoint(product)}-guide.md";
        if (paths.Any(path => path.StartsWith(expectedDocsRoot + "/zh-cn/", StringComparison.OrdinalIgnoreCase)))
        {
            throw new LoomRuntimeIntegrityException($"Runtime package '{packageId}' contains a Chinese docs tree under its English docs root.");
        }
        ValidateManifest(manifestEntry, packageId, product, normalizedVersion, runtimeIdentifier, expectedEntryPointName, expectedDocsRoot, expectedGuidePath);

        if (!paths.Any(path => path.StartsWith(expectedDocsRoot + "/", StringComparison.Ordinal)))
        {
            throw new LoomRuntimeIntegrityException($"Runtime package '{packageId}' is missing its documentation tree '{expectedDocsRoot}/'.");
        }

        var guideArchivePath = $"{expectedDocsRoot}/{expectedGuidePath}";
        if (!paths.Contains(guideArchivePath))
        {
            throw new LoomRuntimeIntegrityException($"Runtime package '{packageId}' is missing its product guide '{guideArchivePath}'.");
        }

        var entryPointPath = $"tools/{runtimeIdentifier}/{expectedEntryPointName}";
        var entryPointEntries = entries.Where(entry => string.Equals(entry.FullName, entryPointPath, StringComparison.Ordinal)).ToArray();
        if (entryPointEntries.Length != 1 || entryPointEntries[0].Length == 0)
        {
            throw new LoomRuntimeIntegrityException($"Runtime package '{packageId}' must contain exactly one non-empty entry point at '{entryPointPath}'.");
        }

        foreach (var entry in entries)
        {
            var path = NormalizeEntryPath(entry.FullName);
            if (path.EndsWith("/", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(path, entryPointPath, StringComparison.Ordinal) || string.Equals(path, manifestPath, StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsAllowedMetadataPath(path, packageId, runtimeIdentifier))
            {
                throw new LoomRuntimeIntegrityException($"Runtime package '{packageId}' contains an unexpected file '{path}'.");
            }
        }

        return new LoomRuntimePackageValidationResult(
            packageId,
            normalizedVersion,
            runtimeIdentifier,
            expectedEntryPointName,
            manifestPath);
    }

    public static string NormalizeAndValidateSha512(ReadOnlySpan<byte> packageBytes, string sidecar)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sidecar);
        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(sidecar.Trim());
        }
        catch (FormatException)
        {
            throw new LoomRuntimeIntegrityException("The runtime package SHA-512 sidecar is not valid base64.");
        }

        if (expected.Length != SHA512.HashSizeInBytes)
        {
            throw new LoomRuntimeIntegrityException("The runtime package SHA-512 sidecar does not contain a SHA-512 digest.");
        }

        var actual = SHA512.HashData(packageBytes);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            throw new LoomRuntimeIntegrityException("The runtime package SHA-512 sidecar does not match the downloaded package bytes.");
        }

        return Convert.ToBase64String(expected);
    }

    public static byte[] ReadEntryBytes(ReadOnlyMemory<byte> packageBytes, string entryPath)
    {
        using var archive = OpenArchive(packageBytes, entryPath);
        var entry = archive.GetEntry(entryPath) ?? throw new LoomRuntimeIntegrityException($"Runtime package entry '{entryPath}' was not found.");
        using var entryStream = entry.Open();
        using var output = new MemoryStream();
        entryStream.CopyTo(output);
        return output.ToArray();
    }

    public static void ExtractDocumentation(ReadOnlyMemory<byte> packageBytes, string runtimeIdentifier, string destinationRoot)
    {
        LoomRuntimeCatalog.EnsureSupportedRuntimeIdentifier(runtimeIdentifier);
        var fullDestinationRoot = Path.GetFullPath(destinationRoot);
        Directory.CreateDirectory(fullDestinationRoot);
        EnsureNoReparsePoint(fullDestinationRoot);
        var docsPrefix = $"tools/{runtimeIdentifier}/docs/en/";
        var extractedFileCount = 0;
        using var archive = OpenArchive(packageBytes, "runtime documentation");
        foreach (var entry in archive.Entries)
        {
            var normalizedPath = NormalizeEntryPath(entry.FullName);
            if (!normalizedPath.StartsWith(docsPrefix, StringComparison.Ordinal) || entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                continue;
            }

            var relativePath = normalizedPath[docsPrefix.Length..];
            var destinationPath = ResolveExtractedPath(fullDestinationRoot, relativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
                EnsureNoReparsePoint(destinationDirectory);
            }

            using var input = entry.Open();
            using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            input.CopyTo(output);
            output.Flush(flushToDisk: true);
            extractedFileCount++;
        }

        if (extractedFileCount == 0)
        {
            throw new LoomRuntimeIntegrityException($"Runtime package does not contain documentation under '{docsPrefix}'.");
        }
    }

    public static void ValidateExtractedDocumentation(ReadOnlyMemory<byte> packageBytes, string runtimeIdentifier, string docsRoot)
    {
        LoomRuntimeCatalog.EnsureSupportedRuntimeIdentifier(runtimeIdentifier);
        var fullDocsRoot = Path.GetFullPath(docsRoot);
        if (!Directory.Exists(fullDocsRoot))
        {
            throw new LoomRuntimeIntegrityException($"Extracted runtime documentation root '{fullDocsRoot}' does not exist.");
        }

        EnsureNoReparsePoint(fullDocsRoot);
        var docsPrefix = $"tools/{runtimeIdentifier}/docs/en/";
        var expectedFiles = new HashSet<string>(StringComparer.Ordinal);
        using var archive = OpenArchive(packageBytes, "runtime documentation");
        foreach (var entry in archive.Entries)
        {
            var normalizedPath = NormalizeEntryPath(entry.FullName);
            if (!normalizedPath.StartsWith(docsPrefix, StringComparison.Ordinal) || entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                continue;
            }

            var relativePath = normalizedPath[docsPrefix.Length..];
            expectedFiles.Add(relativePath);
            var extractedPath = ResolveExtractedPath(fullDocsRoot, relativePath);
            EnsureNoReparsePoint(extractedPath);
            if (!File.Exists(extractedPath))
            {
                throw new LoomRuntimeIntegrityException($"Extracted runtime documentation is missing '{relativePath}'.");
            }

            using var expectedStream = entry.Open();
            using var actualStream = File.OpenRead(extractedPath);
            if (entry.Length != actualStream.Length ||
                !CryptographicOperations.FixedTimeEquals(SHA512.HashData(expectedStream), SHA512.HashData(actualStream)))
            {
                throw new LoomRuntimeIntegrityException($"Extracted runtime documentation does not match package content for '{relativePath}'.");
            }
        }

        if (expectedFiles.Count == 0)
        {
            throw new LoomRuntimeIntegrityException($"Runtime package does not contain documentation under '{docsPrefix}'.");
        }

        foreach (var extractedFile in Directory.EnumerateFiles(fullDocsRoot, "*", SearchOption.AllDirectories))
        {
            EnsureNoReparsePoint(extractedFile);
            var relativePath = NormalizeEntryPath(Path.GetRelativePath(fullDocsRoot, extractedFile).Replace(Path.DirectorySeparatorChar, '/'));
            if (!expectedFiles.Contains(relativePath))
            {
                throw new LoomRuntimeIntegrityException($"Extracted runtime documentation contains an unexpected file '{relativePath}'.");
            }
        }
    }

    private static string ResolveExtractedPath(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new LoomRuntimeIntegrityException($"Runtime documentation path '{relativePath}' escapes the extraction root.");
        }

        return fullPath;
    }

    private static void EnsureNoReparsePoint(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new LoomRuntimeIntegrityException($"Runtime documentation path '{path}' is a reparse point.");
            }
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private static void ValidateNuspec(ZipArchiveEntry entry, string expectedPackageId, string expectedVersion, string expectedRuntimeIdentifier)
    {
        XDocument document;
        try
        {
            using var stream = entry.Open();
            document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }
        catch (Exception exception) when (exception is InvalidOperationException or XmlException)
        {
            throw new LoomRuntimeIntegrityException($"Runtime package '{expectedPackageId}' contains an invalid nuspec.");
        }

        var metadata = document.Descendants().FirstOrDefault(element => string.Equals(element.Name.LocalName, "metadata", StringComparison.OrdinalIgnoreCase));
        var id = metadata?.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, "id", StringComparison.OrdinalIgnoreCase))?.Value;
        var version = metadata?.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, "version", StringComparison.OrdinalIgnoreCase))?.Value;
        var tags = metadata?.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, "tags", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

        string normalizedVersion;
        try
        {
            normalizedVersion = string.IsNullOrWhiteSpace(version) ? string.Empty : LoomRuntimeCatalog.NormalizeVersion(version);
        }
        catch (FormatException)
        {
            throw new LoomRuntimeIntegrityException($"Runtime package '{expectedPackageId}' nuspec contains an invalid version.");
        }

        if (!string.Equals(id, expectedPackageId, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(version) || normalizedVersion != expectedVersion)
        {
            throw new LoomRuntimeIntegrityException($"Runtime package nuspec identity/version does not match package '{expectedPackageId}' version '{expectedVersion}'.");
        }

        if (!tags.Split([' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains($"rid:{expectedRuntimeIdentifier}", StringComparer.OrdinalIgnoreCase))
        {
            throw new LoomRuntimeIntegrityException($"Runtime package '{expectedPackageId}' nuspec is missing the RID tag 'rid:{expectedRuntimeIdentifier}'.");
        }
    }

    private static void ValidateManifest(
        ZipArchiveEntry entry,
        string expectedPackageId,
        LoomRuntimeProduct product,
        string expectedVersion,
        string expectedRuntimeIdentifier,
        string expectedEntryPointName,
        string expectedDocsRoot,
        string expectedGuidePath)
    {
        try
        {
            using var stream = entry.Open();
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            var schema = GetRequiredString(root, "schema");
            var manifestProduct = GetRequiredString(root, "product");
            var packageId = GetRequiredString(root, "package_id");
            var version = GetRequiredString(root, "version");
            var runtimeIdentifier = GetRequiredString(root, "rid");
            var entryPoint = GetRequiredString(root, "entrypoint");
            var docsRoot = GetRequiredString(root, "docs_root");
            var guidePath = GetRequiredString(root, "guide_path");
            var singleFile = root.TryGetProperty("single_file", out var singleFileElement) && singleFileElement.ValueKind == JsonValueKind.True;

            var expectedProduct = LoomRuntimeCatalog.GetEntryPoint(product);
            if (!string.Equals(schema, ManifestSchema, StringComparison.Ordinal) ||
                !string.Equals(manifestProduct, expectedProduct, StringComparison.Ordinal) ||
                !string.Equals(packageId, expectedPackageId, StringComparison.Ordinal) ||
                LoomRuntimeCatalog.NormalizeVersion(version) != expectedVersion ||
                !string.Equals(runtimeIdentifier, expectedRuntimeIdentifier, StringComparison.Ordinal) ||
                !string.Equals(entryPoint, expectedEntryPointName, StringComparison.Ordinal) ||
                !string.Equals(docsRoot, expectedDocsRoot, StringComparison.Ordinal) ||
                !string.Equals(guidePath, expectedGuidePath, StringComparison.Ordinal) ||
                !singleFile)
            {
                throw new LoomRuntimeIntegrityException($"Runtime package '{expectedPackageId}' manifest does not match its requested product, version, RID, or entry point.");
            }
        }
        catch (LoomRuntimeIntegrityException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new LoomRuntimeIntegrityException($"Runtime package '{expectedPackageId}' contains an invalid runtime manifest.");
        }
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new KeyNotFoundException(propertyName);
        }

        return property.GetString()!;
    }

    private static bool IsAllowedMetadataPath(string path, string packageId, string runtimeIdentifier)
    {
        var docsPrefix = $"tools/{runtimeIdentifier}/docs/en/";
        if (path.StartsWith(docsPrefix, StringComparison.Ordinal) && path.Length > docsPrefix.Length)
        {
            return true;
        }

        if (string.Equals(path, $"{packageId}.nuspec", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "README.md", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "icon.png", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "[Content_Types].xml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "_rels/.rels", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.StartsWith("package/services/metadata/core-properties/", StringComparison.OrdinalIgnoreCase) &&
               path.EndsWith(".psmdcp", StringComparison.OrdinalIgnoreCase);
    }

    private static ZipArchive OpenArchive(ReadOnlyMemory<byte> packageBytes, string packageName)
    {
        var stream = new MemoryStream(packageBytes.ToArray(), writable: false);
        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch (InvalidDataException)
        {
            stream.Dispose();
            throw new LoomRuntimeIntegrityException($"Runtime package '{packageName}' is not a valid ZIP archive.");
        }
    }

    private static string NormalizeEntryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\0'))
        {
            throw new LoomRuntimeIntegrityException("Runtime package contains an invalid empty or NUL-containing ZIP path.");
        }

        if (path.Contains('\\'))
        {
            throw new LoomRuntimeIntegrityException($"Runtime package contains a non-canonical ZIP path using a backslash: '{path}'.");
        }

        var normalized = path.EndsWith("/", StringComparison.Ordinal) ? path[..^1] : path;
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.StartsWith("/", StringComparison.Ordinal) ||
            normalized.Contains("//", StringComparison.Ordinal) ||
            normalized.Contains(':', StringComparison.Ordinal))
        {
            throw new LoomRuntimeIntegrityException($"Runtime package contains a non-canonical or absolute ZIP path '{path}'.");
        }

        var segments = normalized.Split('/', StringSplitOptions.None);
        if (segments.Any(segment => string.IsNullOrEmpty(segment) || segment is "." or ".."))
        {
            throw new LoomRuntimeIntegrityException($"Runtime package contains a path traversal or non-canonical ZIP path '{path}'.");
        }

        return normalized;
    }
}
