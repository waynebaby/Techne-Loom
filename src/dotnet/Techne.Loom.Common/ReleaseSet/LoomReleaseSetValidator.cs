using System.Net;
using System.Net.Http;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Techne.Loom.Common.Runtime;

namespace Techne.Loom.Common.ReleaseSet;

public sealed class LoomNuGetPackageMetadataSource : ILoomReleaseSetPackageMetadataSource
{
    private readonly HttpClient _httpClient;

    public LoomNuGetPackageMetadataSource(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> GetLatestVersionAsync(string packageId, string channel, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        var normalizedPackageId = packageId.ToLowerInvariant();
        var url = $"https://api.nuget.org/v3-flatcontainer/{normalizedPackageId}/index.json";
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"NuGet metadata request for '{packageId}' returned {(int)response.StatusCode} ({response.ReasonPhrase}).", null, response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("versions", out var versions) || versions.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"NuGet metadata for '{packageId}' did not contain a versions array.");
        }

        var candidates = versions.EnumerateArray()
            .Where(static version => version.ValueKind == JsonValueKind.String)
            .Select(static version => version.GetString())
            .Where(static version => !string.IsNullOrWhiteSpace(version))
            .Select(static version => LoomRuntimeCatalog.NormalizeVersion(version!))
            .Where(version => IsChannelVersion(version, channel))
            .Select(static version => new { Version = version, Key = ParseVersion(version) })
            .OrderBy(item => item.Key.Major)
            .ThenBy(item => item.Key.Minor)
            .ThenBy(item => item.Key.Patch)
            .ThenBy(item => item.Key.Prerelease, StringComparer.Ordinal)
            .Select(static item => item.Version)
            .ToArray();

        return candidates.LastOrDefault()
            ?? throw new InvalidDataException($"NuGet metadata for '{packageId}' did not contain a published {channel} version.");
    }

    private static bool IsChannelVersion(string version, string channel)
        => channel switch
        {
            "released" => !version.Contains('-', StringComparison.Ordinal),
            "beta" => version.EndsWith("-beta", StringComparison.OrdinalIgnoreCase),
            _ => throw new ArgumentException($"Unsupported release channel '{channel}'.", nameof(channel)),
        };

    private static (int Major, int Minor, int Patch, string Prerelease) ParseVersion(string version)
    {
        var parts = version.Split('-', 2, StringSplitOptions.None);
        var numbers = parts[0].Split('.', StringSplitOptions.None);
        return (
            int.Parse(numbers[0], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(numbers[1], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(numbers[2], System.Globalization.CultureInfo.InvariantCulture),
            parts.Length == 2 ? parts[1] : string.Empty);
    }
}

public static class LoomReleaseSetValidator
{
    private const string ManifestSchemaVersion = "loom-release-set.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };
    private static readonly Regex VersionLiteralPattern = new(
        @"(?<![0-9A-Za-z])(?<version>[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?)(?![0-9A-Za-z])",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ExactVersionPattern = new(
        @"^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GuideVersionPattern = new(
        @"(?m)^Version:\s*(?<version>\S+)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GuideBuildPattern = new(
        @"(?m)^Build:\s*published package\s+(?<version>\S+)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly HashSet<string> SupportedExcludedClassifications = new(StringComparer.Ordinal)
    {
        "historical",
        "audit",
        "test",
        "source-debug",
        "synthetic",
    };
    private static readonly string[] ExpectedCorePackageIds =
    [
        "Techne.Loom.Abstractions",
        "Techne.Loom.Common",
        "Techne.Loom.AgentOrchestrator",
        "Techne.Loom.SkillOrchestrator",
    ];

    public static IReadOnlyList<string> GetExpectedPackageIds()
    {
        var packageIds = new List<string>(ExpectedCorePackageIds);
        foreach (var product in new[] { LoomRuntimeProduct.AgentOrchestrator, LoomRuntimeProduct.SkillOrchestrator })
        {
            packageIds.AddRange(LoomRuntimeCatalog.SupportedRuntimeIdentifiers.Select(rid => LoomRuntimeCatalog.GetPackageId(product, rid)));
        }

        return packageIds;
    }

    public static async Task<LoomReleaseSetValidationReport> ValidateAsync(
        LoomReleaseSetValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<LoomReleaseSetValidationIssue>();
        var latestVersions = new Dictionary<string, string>(StringComparer.Ordinal);
        var surfaceVersions = new List<(string Path, string Version)>();
        var packageIndexVersions = new Dictionary<string, List<(string Path, string Version)>>(StringComparer.Ordinal);
        var root = ResolveRoot(request.RepositoryRoot, issues);
        var manifestPath = ResolvePath(root, request.ManifestPath, "manifest", issues);
        LoomReleaseSetManifest? manifest = null;

        if (!File.Exists(manifestPath))
        {
            AddIssue(issues, "missing-manifest", request.ManifestPath, "The release-set manifest does not exist.");
        }
        else
        {
            try
            {
                await using var stream = File.OpenRead(manifestPath);
                manifest = await JsonSerializer.DeserializeAsync<LoomReleaseSetManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
                if (manifest is null)
                {
                    AddIssue(issues, "malformed-manifest", request.ManifestPath, "The release-set manifest is empty.");
                }
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                AddIssue(issues, "malformed-manifest", request.ManifestPath, $"The release-set manifest could not be read as valid JSON: {exception.Message}");
            }
        }

        if (manifest is null)
        {
            return CreateReport(request, null, latestVersions, issues);
        }

        ValidateManifestShape(manifest, request, root, issues);
        var packageIds = BuildPackageIds(manifest, issues);
        var candidateVersion = ValidateCandidateVersion(request, manifest, issues);
        var validatesPublishedPackages = request.AuthorityMode == LoomReleaseSetAuthorityMode.CheckIn;
        var validatesSurfaces = request.Phase != LoomReleaseSetValidationPhase.PostPublishPackageClosure
            && request.Phase != LoomReleaseSetValidationPhase.PrePublishPackageClosure;
        var validatesDocumentCopyEvidence = request.Phase != LoomReleaseSetValidationPhase.PrePublish
            && request.Phase != LoomReleaseSetValidationPhase.PrePublishPackageClosure;
        var validatesPackageArtifacts = request.AuthorityMode == LoomReleaseSetAuthorityMode.Release
            && (request.Phase == LoomReleaseSetValidationPhase.PrePublishPackageClosure
                || request.Phase == LoomReleaseSetValidationPhase.PostPublish
                || request.Phase == LoomReleaseSetValidationPhase.PostPublishPackageClosure);
        var strictSurfaceVersion = request.AuthorityMode == LoomReleaseSetAuthorityMode.CheckIn || request.Phase == LoomReleaseSetValidationPhase.PostPublish;
        var expectedVersion = candidateVersion;

        if (validatesPublishedPackages)
        {
            if (request.PackageMetadataSource is null)
            {
                AddIssue(issues, "missing-package-metadata-source", "version_authority", "NuGet metadata is required for this validation mode, but no metadata source was provided.");
            }
            else
            {
                foreach (var packageId in packageIds)
                {
                    try
                    {
                        var version = await request.PackageMetadataSource.GetLatestVersionAsync(packageId, request.Channel, cancellationToken).ConfigureAwait(false);
                        latestVersions[packageId] = LoomRuntimeCatalog.NormalizeVersion(version);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        AddIssue(issues, "nuget-metadata-unavailable", packageId, $"The latest {request.Channel} package version could not be verified: {exception.Message}");
                    }
                }

                var distinctLatestVersions = latestVersions.Values.Distinct(StringComparer.Ordinal).ToArray();
                if (latestVersions.Count == packageIds.Count && distinctLatestVersions.Length != 1)
                {
                    AddIssue(issues, "mixed-published-versions", "nuget", $"The 20 package ids do not resolve to one exact {request.Channel} version: {string.Join(", ", distinctLatestVersions)}.");
                }
                else if (distinctLatestVersions.Length == 1)
                {
                    var latestVersion = distinctLatestVersions[0];
                    if (!IsManifestChannelVersion(manifest, request.Channel, latestVersion))
                    {
                        AddIssue(issues, "channel-version-mismatch", "nuget", $"NuGet returned version '{latestVersion}', which does not belong to channel '{request.Channel}'.");
                    }
                    else if (expectedVersion is null)
                    {
                        expectedVersion = latestVersion;
                    }
                    else if (!string.Equals(expectedVersion, latestVersion, StringComparison.Ordinal))
                    {
                        AddIssue(issues, "candidate-version-mismatch", "nuget", $"Candidate version '{expectedVersion}' does not match the published {request.Channel} version '{latestVersion}'.");
                    }
                }
            }
        }

        if (validatesSurfaces)
        {
        ValidatePackageIndexes(manifest, request, root, expectedVersion, strictSurfaceVersion, packageIndexVersions, issues);
        ValidateSkills(manifest, request, root, expectedVersion, strictSurfaceVersion, validatesDocumentCopyEvidence, surfaceVersions, issues);
        ValidateGuides(manifest, request, root, expectedVersion, strictSurfaceVersion, surfaceVersions, issues);
        }
        ValidateCi(manifest, root, issues);
        ValidateExcludedArtifacts(manifest, root, issues);
        if (validatesPackageArtifacts)
        {
            ValidatePackageArtifacts(manifest, request, root, packageIds, expectedVersion, issues);
        }

        if (validatesSurfaces)
        {
        if (!strictSurfaceVersion)
        {
            var distinctSurfaceVersions = surfaceVersions.Select(item => item.Version).Distinct(StringComparer.Ordinal).ToArray();
            if (distinctSurfaceVersions.Length > 1)
            {
                AddIssue(issues, "surface-version-drift", "surfaces", $"The active skill, lock, manifest, and guide surfaces disagree: {string.Join(", ", distinctSurfaceVersions)}.");
            }
        }

        foreach (var (channel, values) in packageIndexVersions)
        {
            var distinct = values.Select(item => item.Version).Distinct(StringComparer.Ordinal).ToArray();
            if (distinct.Length > 1)
            {
                AddIssue(issues, "package-index-version-drift", channel, $"The active {channel} package indexes disagree: {string.Join(", ", distinct)}.");
            }
        }

        }
        return CreateReport(request, expectedVersion, latestVersions, issues);
    }

    private static LoomReleaseSetValidationReport CreateReport(
        LoomReleaseSetValidationRequest request,
        string? expectedVersion,
        IReadOnlyDictionary<string, string> latestVersions,
        IReadOnlyList<LoomReleaseSetValidationIssue> issues)
        => new(
            issues.Count == 0,
            request.AuthorityMode.ToString(),
            request.Phase.ToString(),
            request.Channel,
            expectedVersion,
            new Dictionary<string, string>(latestVersions, StringComparer.Ordinal),
            issues.ToArray());

    private static string ResolveRoot(string root, List<LoomReleaseSetValidationIssue> issues)
    {
        try
        {
            return Path.GetFullPath(root);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            AddIssue(issues, "invalid-repository-root", "repository", exception.Message);
            return Directory.GetCurrentDirectory();
        }
    }

    private static string ResolvePath(string root, string? relativePath, string label, List<LoomReleaseSetValidationIssue> issues)
    {
        var fullRoot = Path.GetFullPath(root);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            AddIssue(issues, "missing-path", label, "A path is required.");
            return fullRoot;
        }

        var normalized = relativePath.Replace('\\', '/');
        if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal))
        {
            AddIssue(issues, "unsafe-path", relativePath, "Release-set paths must be relative and must not escape the repository root.");
            return fullRoot;
        }

        if (Path.IsPathRooted(relativePath))
        {
            AddIssue(issues, "unsafe-path", relativePath, "Release-set paths must be relative and must not escape the repository root.");
            return fullRoot;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            AddIssue(issues, "invalid-path", relativePath, $"The release-set path could not be resolved: {exception.Message}");
            return fullRoot;
        }

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var rootWithSeparator = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!string.Equals(fullPath, fullRoot, comparison) && !fullPath.StartsWith(rootWithSeparator, comparison))
        {
            AddIssue(issues, "unsafe-path", relativePath, "Release-set paths must resolve inside the repository root.");
            return fullRoot;
        }

        return fullPath;
    }

    private static void ValidateManifestShape(
        LoomReleaseSetManifest manifest,
        LoomReleaseSetValidationRequest request,
        string root,
        List<LoomReleaseSetValidationIssue> issues)
    {
        if (!string.Equals(manifest.SchemaVersion, ManifestSchemaVersion, StringComparison.Ordinal))
        {
            AddIssue(issues, "manifest-schema", "schema_version", $"Expected '{ManifestSchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.ReleaseSetId))
        {
            AddIssue(issues, "manifest-release-set-id", "release_set_id", "The release-set id is required.");
        }

        if (manifest.VersionAuthority?.CheckIn is null ||
            !string.Equals(manifest.VersionAuthority.CheckIn.Source, "nuget-flat-container-index", StringComparison.Ordinal) ||
            manifest.VersionAuthority.CheckIn.RequiresAllPackagesSameExactVersion != true)
        {
            AddIssue(issues, "check-in-authority", "version_authority.check_in", "Check-in authority must be the NuGet flat-container index and require one exact version across all packages.");
        }

        if (manifest.VersionAuthority?.Release is null ||
            !string.Equals(manifest.VersionAuthority.Release.Source, "shared-version-job-output", StringComparison.Ordinal) ||
            manifest.VersionAuthority.Release.RequiresAllPackagesSameExactVersion != true)
        {
            AddIssue(issues, "release-authority", "version_authority.release", "Release authority must be the shared version-job output and require one exact version across all packages.");
        }

        if (manifest.Channels is null || !manifest.Channels.ContainsKey("released") || !manifest.Channels.ContainsKey("beta"))
        {
            AddIssue(issues, "channel-rules", "channels", "Both released and beta channel rules are required.");
        }
        else
        {
            foreach (var channelName in new[] { "released", "beta" })
            {
                var rule = manifest.Channels[channelName];
                if (rule is null || string.IsNullOrWhiteSpace(rule.VersionPattern))
                {
                    AddIssue(issues, "channel-version-pattern", $"channels.{channelName}.version_pattern", "Each channel must declare a non-empty exact-version pattern.");
                }
                else
                {
                    try
                    {
                        var sampleVersion = channelName == "beta" ? "0.3.258-beta" : "0.3.270";
                        if (!Regex.IsMatch(sampleVersion, rule.VersionPattern, RegexOptions.CultureInvariant))
                        {
                            AddIssue(issues, "channel-version-pattern", $"channels.{channelName}.version_pattern", $"The channel pattern does not accept its canonical {channelName} version shape.");
                        }
                    }
                    catch (ArgumentException exception)
                    {
                        AddIssue(issues, "channel-version-pattern", $"channels.{channelName}.version_pattern", $"The channel version pattern is invalid: {exception.Message}");
                    }
                }

                if (rule?.PackageIndexPaths is null || rule.PackageIndexPaths.Count == 0)
                {
                    AddIssue(issues, $"channel-package-index-scope", $"channels.{channelName}.package_index_paths", "Each channel must declare at least one package index path.");
                }
            }

            if (!string.Equals(request.Channel, "released", StringComparison.Ordinal) && !string.Equals(request.Channel, "beta", StringComparison.Ordinal))
            {
                AddIssue(issues, "unsupported-channel", "channel", $"Unsupported release channel '{request.Channel}'.");
            }
        }

        var core = manifest.Packages?.Core;
        if (core is null || core.Count != ExpectedCorePackageIds.Length ||
            !core.OrderBy(static value => value, StringComparer.Ordinal).SequenceEqual(ExpectedCorePackageIds.OrderBy(static value => value, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            AddIssue(issues, "core-package-scope", "packages.core", "The release set must contain exactly the four core package ids.");
        }

        var runtime = manifest.Packages?.Runtime;
        if (runtime?.Products is null ||
            !runtime.Products.OrderBy(static value => value, StringComparer.Ordinal).SequenceEqual(new[] { "ao", "so" }, StringComparer.Ordinal))
        {
            AddIssue(issues, "runtime-product-scope", "packages.runtime.products", "The runtime package scope must contain exactly ao and so.");
        }

        if (runtime?.RuntimeIdentifiers is null ||
            !runtime.RuntimeIdentifiers.OrderBy(static value => value, StringComparer.Ordinal).SequenceEqual(LoomRuntimeCatalog.SupportedRuntimeIdentifiers.OrderBy(static value => value, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            AddIssue(issues, "runtime-rid-scope", "packages.runtime.rids", "The runtime package scope must contain exactly the supported eight RIDs.");
        }

        if (manifest.Skills is null || manifest.Skills.Count != 2 || manifest.Skills.Select(static skill => skill.Product).ToHashSet(StringComparer.Ordinal).SetEquals(new[] { "ao", "so" }) is false)
        {
            AddIssue(issues, "skill-scope", "skills", "The release set must declare one AO skill surface and one SO skill surface.");
        }

        if (manifest.Surfaces is null)
        {
            AddIssue(issues, "surface-scope", "surfaces", "Active release-set surfaces are required.");
        }

        if (manifest.Ci is null || manifest.Ci.WorkflowPaths is null || manifest.Ci.WorkflowPaths.Count == 0)
        {
            AddIssue(issues, "ci-scope", "ci", "CI workflow paths and the shared version contract are required.");
        }

        foreach (var skill in manifest.Skills ?? [])
        {
            ValidateRelativeManifestPath(skill.Root, root, $"skills[{skill.Product}].root", issues);
            ValidateRelativeManifestPath(skill.VersionBlock, root, $"skills[{skill.Product}].version_block", issues);
            ValidateRelativeManifestPath(skill.PackageLock, root, $"skills[{skill.Product}].package_lock", issues);
            ValidateRelativeManifestPath(skill.DocumentCopyManifest, root, $"skills[{skill.Product}].document_copy_manifest", issues);
        }

        foreach (var surface in manifest.Surfaces?.PackageIndexes ?? [])
        {
            ValidateRelativeManifestPath(surface.Path, root, $"surfaces.package_indexes[{surface.Channel}]", issues);
            if (!string.Equals(surface.Channel, "released", StringComparison.Ordinal) && !string.Equals(surface.Channel, "beta", StringComparison.Ordinal))
            {
                AddIssue(issues, "surface-channel", surface.Path ?? "package-index", "Package index surfaces must declare released or beta.");
            }
        }

        foreach (var surface in manifest.Surfaces?.GuideMetadata ?? [])
        {
            ValidateRelativeManifestPath(surface.Glob, root, $"surfaces.guide_metadata[{surface.Product}]", issues);
            if (!string.Equals(surface.Product, "ao", StringComparison.Ordinal) && !string.Equals(surface.Product, "so", StringComparison.Ordinal))
            {
                AddIssue(issues, "guide-product", surface.Glob ?? "guide", "Guide metadata surfaces must declare ao or so.");
            }
        }

        foreach (var path in manifest.Surfaces?.WorkflowPaths ?? [])
        {
            ValidateRelativeManifestPath(path, root, "surfaces.workflow_paths", issues);
        }

        foreach (var path in manifest.Ci?.WorkflowPaths ?? [])
        {
            ValidateRelativeManifestPath(path, root, "ci.workflow_paths", issues);
        }

        foreach (var channelName in new[] { "released", "beta" })
        {
            var declaredPaths = manifest.Channels?.TryGetValue(channelName, out var channelRule) == true
                ? (channelRule.PackageIndexPaths ?? []).OrderBy(static path => path, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            var surfacePaths = (manifest.Surfaces?.PackageIndexes ?? [])
                .Where(surface => string.Equals(surface.Channel, channelName, StringComparison.Ordinal))
                .Select(surface => surface.Path ?? string.Empty)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray();
            if (!declaredPaths.SequenceEqual(surfacePaths, StringComparer.Ordinal))
            {
                AddIssue(issues, $"channel-package-index-mismatch", $"channels.{channelName}.package_index_paths", "Channel package index paths must match the active package index surfaces.");
            }
        }
    }

    private static List<string> BuildPackageIds(LoomReleaseSetManifest manifest, List<LoomReleaseSetValidationIssue> issues)
    {
        var packageIds = new List<string>();
        packageIds.AddRange(manifest.Packages?.Core ?? []);
        foreach (var productName in manifest.Packages?.Runtime?.Products ?? [])
        {
            if (!TryGetProduct(productName, out var product))
            {
                AddIssue(issues, "runtime-product", "packages.runtime.products", $"Unsupported runtime product '{productName}'.");
                continue;
            }

            foreach (var rid in manifest.Packages?.Runtime?.RuntimeIdentifiers ?? [])
            {
                try
                {
                    packageIds.Add(LoomRuntimeCatalog.GetPackageId(product, rid));
                }
                catch (PlatformNotSupportedException exception)
                {
                    AddIssue(issues, "runtime-rid", rid, exception.Message);
                }
            }
        }

        var duplicatePackageIds = packageIds.GroupBy(static packageId => packageId, StringComparer.Ordinal).Where(static group => group.Count() > 1).Select(static group => group.Key).ToArray();
        if (duplicatePackageIds.Length > 0)
        {
            AddIssue(issues, "duplicate-package-scope", "packages", $"The release-set package scope contains duplicates: {string.Join(", ", duplicatePackageIds)}.");
        }

        var expected = GetExpectedPackageIds().OrderBy(static value => value, StringComparer.Ordinal).ToArray();
        if (!packageIds.OrderBy(static value => value, StringComparer.Ordinal).SequenceEqual(expected, StringComparer.Ordinal))
        {
            AddIssue(issues, "package-scope", "packages", $"The release set must enumerate exactly 20 package ids: 4 core packages and 16 AO/SO runtime packages.");
        }

        return packageIds.Distinct(StringComparer.Ordinal).ToList();
    }

    private static string? ValidateCandidateVersion(LoomReleaseSetValidationRequest request, LoomReleaseSetManifest manifest, List<LoomReleaseSetValidationIssue> issues)
    {
        if (request.AuthorityMode != LoomReleaseSetAuthorityMode.Release && string.IsNullOrWhiteSpace(request.CandidateVersion))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.CandidateVersion))
        {
            AddIssue(issues, "missing-candidate-version", "candidate_version", "Release validation requires the exact version emitted by the shared version job.");
            return null;
        }

        string normalized;
        try
        {
            normalized = LoomRuntimeCatalog.NormalizeVersion(request.CandidateVersion);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            AddIssue(issues, "invalid-candidate-version", "candidate_version", exception.Message);
            return null;
        }

        if (!ExactVersionPattern.IsMatch(normalized) || !IsManifestChannelVersion(manifest, request.Channel, normalized))
        {
            AddIssue(issues, "candidate-channel-version", "candidate_version", $"Candidate version '{normalized}' does not match channel '{request.Channel}'.");
        }

        return normalized;
    }

    private static void ValidatePackageArtifacts(
        LoomReleaseSetManifest manifest,
        LoomReleaseSetValidationRequest request,
        string root,
        IReadOnlyList<string> packageIds,
        string? expectedVersion,
        List<LoomReleaseSetValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(request.PackageRoot))
        {
            AddIssue(issues, "missing-package-root", "package_root", "Local package closure requires the package artifact directory.");
            return;
        }

        if (string.IsNullOrWhiteSpace(expectedVersion))
        {
            AddIssue(issues, "missing-candidate-version", "candidate_version", "Local package closure requires the exact version emitted by the shared version job.");
            return;
        }

        var packageRoot = ResolvePath(root, request.PackageRoot, "package-root", issues);
        if (!Directory.Exists(packageRoot))
        {
            AddIssue(issues, "missing-package-root", request.PackageRoot, "The local package artifact directory does not exist.");
            return;
        }

        var expectedFiles = packageIds
            .Select(packageId => packageId + "." + expectedVersion + ".nupkg")
            .ToHashSet(StringComparer.Ordinal);
        var actualFiles = Directory.GetFiles(packageRoot, "*.nupkg", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expectedFile in expectedFiles.OrderBy(static value => value, StringComparer.Ordinal))
        {
            if (!actualFiles.Contains(expectedFile))
            {
                AddIssue(issues, "missing-package-artifact", Path.Combine(request.PackageRoot, expectedFile), "The local package artifact is missing from the release set.");
            }
        }

        foreach (var actualFile in actualFiles.OrderBy(static value => value, StringComparer.Ordinal))
        {
            if (!expectedFiles.Contains(actualFile))
            {
                AddIssue(issues, "unexpected-package-artifact", Path.Combine(request.PackageRoot, actualFile), "The local package artifact is outside the exact release-set package and version closure.");
            }
        }

        foreach (var packageId in packageIds)
        {
            var expectedFile = packageId + "." + expectedVersion + ".nupkg";
            var packagePath = Path.Combine(packageRoot, expectedFile);
            if (File.Exists(packagePath))
            {
                ValidatePackageArtifact(packagePath, packageId, expectedVersion, request.PackageRoot, packageIds.ToHashSet(StringComparer.OrdinalIgnoreCase), issues);
            }
        }
    }

    private static void ValidatePackageArtifact(
        string packagePath,
        string expectedPackageId,
        string expectedVersion,
        string packageRoot,
        IReadOnlySet<string> releaseSetPackageIds,
        List<LoomReleaseSetValidationIssue> issues)
    {
        var displayPath = Path.Combine(packageRoot, Path.GetFileName(packagePath));
        try
        {
            var packageBytes = File.ReadAllBytes(packagePath);
            using var archive = ZipFile.OpenRead(packagePath);
            var nuspec = archive.Entries.FirstOrDefault(entry =>
                entry.FullName.IndexOf('/', StringComparison.Ordinal) < 0
                && entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
            if (nuspec is null)
            {
                AddIssue(issues, "invalid-package-artifact", displayPath, "The package does not contain a root nuspec file.");
                return;
            }

            using var nuspecStream = nuspec.Open();
            var document = XDocument.Load(nuspecStream);
            var metadata = document.Descendants().FirstOrDefault(element => string.Equals(element.Name.LocalName, "metadata", StringComparison.Ordinal));
            var actualPackageId = metadata?.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, "id", StringComparison.Ordinal))?.Value.Trim();
            var actualVersion = metadata?.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, "version", StringComparison.Ordinal))?.Value.Trim();
            if (!string.Equals(actualPackageId, expectedPackageId, StringComparison.Ordinal) || !string.Equals(actualVersion, expectedVersion, StringComparison.Ordinal))
            {
                AddIssue(issues, "package-artifact-identity", displayPath, $"The package nuspec identity is '{actualPackageId ?? "<missing>"}' at version '{actualVersion ?? "<missing>"}', expected '{expectedPackageId}' at version '{expectedVersion}'.");
            }

            var dependencyElements = metadata?.Descendants().Where(element => string.Equals(element.Name.LocalName, "dependency", StringComparison.Ordinal))
                ?? Enumerable.Empty<XElement>();
            foreach (var dependency in dependencyElements)
            {
                var dependencyId = dependency.Attribute("id")?.Value.Trim();
                if (string.IsNullOrWhiteSpace(dependencyId) || !releaseSetPackageIds.Contains(dependencyId))
                {
                    continue;
                }

                var dependencyVersion = dependency.Attribute("version")?.Value.Trim();
                string? normalizedDependencyVersion = null;
                if (!string.IsNullOrWhiteSpace(dependencyVersion) && ExactVersionPattern.IsMatch(dependencyVersion))
                {
                    try
                    {
                        normalizedDependencyVersion = LoomRuntimeCatalog.NormalizeVersion(dependencyVersion);
                    }
                    catch (Exception exception) when (exception is ArgumentException or FormatException)
                    {
                        normalizedDependencyVersion = null;
                    }
                }

                if (!string.Equals(normalizedDependencyVersion, expectedVersion, StringComparison.Ordinal))
                {
                    AddIssue(issues, "package-artifact-internal-dependency-version", $"{displayPath}::dependency:{dependencyId}", $"Internal Loom dependency '{dependencyId}' uses '{dependencyVersion ?? "<missing>"}', expected exact version '{expectedVersion}'.");
                }
            }

            if (TryGetRuntimePackageIdentity(expectedPackageId, out var runtimeProduct, out var runtimeIdentifier))
            {
                try
                {
                    LoomRuntimePackageValidator.Validate(packageBytes, runtimeProduct, expectedVersion, runtimeIdentifier);
                    ValidateRuntimeGuideVersions(archive, displayPath, expectedVersion, runtimeProduct, runtimeIdentifier, issues);
                }
                catch (LoomRuntimeIntegrityException exception)
                {
                    AddIssue(issues, "runtime-package-integrity", displayPath, exception.Message);
                }
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or XmlException or InvalidOperationException)
        {
            AddIssue(issues, "invalid-package-artifact", displayPath, $"The local package artifact is not a readable NuGet archive: {exception.Message}");
        }
    }

    private static void ValidateRuntimeGuideVersions(
        ZipArchive archive,
        string displayPath,
        string expectedVersion,
        LoomRuntimeProduct product,
        string runtimeIdentifier,
        List<LoomReleaseSetValidationIssue> issues)
    {
        var entryPoint = LoomRuntimeCatalog.GetEntryPoint(product);
        var guidePrefix = $"tools/{runtimeIdentifier}/docs/en/guides/{entryPoint}-guide";
        var guideEntries = archive.Entries
            .Where(entry => entry.FullName.StartsWith(guidePrefix, StringComparison.Ordinal) && entry.FullName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (guideEntries.Length == 0)
        {
            AddIssue(issues, "runtime-package-guide-version", displayPath, $"Runtime package contains no guide pages under '{guidePrefix}'.");
            return;
        }

        foreach (var guideEntry in guideEntries)
        {
            using var reader = new StreamReader(guideEntry.Open());
            var text = reader.ReadToEnd();
            var versionMatch = GuideVersionPattern.Match(text);
            var buildMatch = GuideBuildPattern.Match(text);
            var matches = false;
            if (versionMatch.Success && buildMatch.Success)
            {
                try
                {
                    matches = string.Equals(LoomRuntimeCatalog.NormalizeVersion(versionMatch.Groups["version"].Value), expectedVersion, StringComparison.Ordinal) &&
                        string.Equals(LoomRuntimeCatalog.NormalizeVersion(buildMatch.Groups["version"].Value), expectedVersion, StringComparison.Ordinal);
                }
                catch (Exception exception) when (exception is ArgumentException or FormatException)
                {
                    matches = false;
                }
            }

            if (!matches)
            {
                AddIssue(issues, "runtime-package-guide-version", $"{displayPath}::{guideEntry.FullName}", $"Runtime guide metadata must declare Version and Build '{expectedVersion}'.");
            }
        }
    }

    private static bool TryGetRuntimePackageIdentity(string packageId, out LoomRuntimeProduct product, out string runtimeIdentifier)
    {
        foreach (var candidateProduct in new[] { LoomRuntimeProduct.AgentOrchestrator, LoomRuntimeProduct.SkillOrchestrator })
        {
            var prefix = LoomRuntimeCatalog.GetProductPackageId(candidateProduct) + ".Runtime.";
            if (!packageId.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var candidateRid = packageId[prefix.Length..];
            if (LoomRuntimeCatalog.SupportedRuntimeIdentifiers.Contains(candidateRid, StringComparer.Ordinal) &&
                string.Equals(LoomRuntimeCatalog.GetPackageId(candidateProduct, candidateRid), packageId, StringComparison.Ordinal))
            {
                product = candidateProduct;
                runtimeIdentifier = candidateRid;
                return true;
            }
        }

        product = default;
        runtimeIdentifier = string.Empty;
        return false;
    }

    private static void ValidatePackageIndexes(
        LoomReleaseSetManifest manifest,
        LoomReleaseSetValidationRequest request,
        string root,
        string? expectedVersion,
        bool strictSurfaceVersion,
        Dictionary<string, List<(string Path, string Version)>> packageIndexVersions,
        List<LoomReleaseSetValidationIssue> issues)
    {
        foreach (var surface in manifest.Surfaces?.PackageIndexes ?? [])
        {
            var path = ResolvePath(root, surface.Path, "package-index", issues);
            if (!File.Exists(path))
            {
                AddIssue(issues, "missing-package-index", surface.Path ?? "package-index", "The active package index does not exist.");
                continue;
            }

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                AddIssue(issues, "unreadable-package-index", surface.Path ?? "package-index", exception.Message);
                continue;
            }

            var literals = ExtractActivePackageIndexVersionLiterals(text).Distinct(StringComparer.Ordinal).ToArray();
            if (literals.Length == 0)
            {
                AddIssue(issues, "package-index-version-missing", surface.Path ?? "package-index", "The active package index has no exact version literal.");
                continue;
            }

            var normalizedVersions = new List<string>();
            foreach (var literal in literals)
            {
                try
                {
                    var normalized = LoomRuntimeCatalog.NormalizeVersion(literal);
                    normalizedVersions.Add(normalized);
                    if (!IsManifestChannelVersion(manifest, surface.Channel ?? string.Empty, normalized))
                    {
                        AddIssue(issues, "package-index-channel-version", surface.Path ?? "package-index", $"Version '{literal}' does not belong to channel '{surface.Channel}'.");
                    }
                }
                catch (Exception exception) when (exception is ArgumentException or FormatException)
                {
                    AddIssue(issues, "package-index-invalid-version", surface.Path ?? "package-index", exception.Message);
                }
            }

            var distinct = normalizedVersions.Distinct(StringComparer.Ordinal).ToArray();
            if (distinct.Length != 1)
            {
                AddIssue(issues, "package-index-version-drift", surface.Path ?? "package-index", $"The package index contains more than one active exact version: {string.Join(", ", distinct)}.");
                continue;
            }

            var channel = surface.Channel ?? string.Empty;
            if (!packageIndexVersions.TryGetValue(channel, out var values))
            {
                values = [];
                packageIndexVersions[channel] = values;
            }

            values.Add((surface.Path ?? "package-index", distinct[0]));
            if (strictSurfaceVersion && string.Equals(channel, request.Channel, StringComparison.Ordinal) && expectedVersion is not null && !string.Equals(distinct[0], expectedVersion, StringComparison.Ordinal))
            {
                AddIssue(issues, "stale-package-index", surface.Path ?? "package-index", $"The active {channel} package index uses '{distinct[0]}' instead of '{expectedVersion}'.");
            }
        }
    }

    private static void ValidateSkills(
        LoomReleaseSetManifest manifest,
        LoomReleaseSetValidationRequest request,
        string root,
        string? expectedVersion,
        bool strictSurfaceVersion,
        bool validatesDocumentCopyEvidence,
        List<(string Path, string Version)> surfaceVersions,
        List<LoomReleaseSetValidationIssue> issues)
    {
        foreach (var skill in manifest.Skills ?? [])
        {
            var product = skill.Product ?? string.Empty;
            var versionBlockPath = ResolvePath(root, skill.VersionBlock, "skill-version-block", issues);
            if (!File.Exists(versionBlockPath))
            {
                AddIssue(issues, "missing-skill-version-block", skill.VersionBlock ?? product, "The active skill version block does not exist.");
            }
            else
            {
                var text = File.ReadAllText(versionBlockPath);
                var pattern = new Regex($@"(?m)^- Current published (?:AO|SO) package runtime version:\s*`(?<version>[^`]+)`\.", RegexOptions.CultureInvariant);
                var matches = pattern.Matches(text);
                if (matches.Count != 1)
                {
                    AddIssue(issues, "skill-version-block-shape", skill.VersionBlock ?? product, "The active skill must contain exactly one current published package version marker.");
                }
                else
                {
                    ValidateSurfaceVersion(matches[0].Groups["version"].Value, skill.VersionBlock ?? product, request, manifest, expectedVersion, strictSurfaceVersion, surfaceVersions, issues);
                }
            }

            ValidatePackageLock(skill, request, manifest, root, expectedVersion, strictSurfaceVersion, surfaceVersions, issues);
            ValidateDocumentCopyManifest(skill, request, manifest, root, expectedVersion, strictSurfaceVersion, validatesDocumentCopyEvidence, surfaceVersions, issues);
        }
    }

    private static void ValidatePackageLock(
        LoomReleaseSetSkillSurface skill,
        LoomReleaseSetValidationRequest request,
        LoomReleaseSetManifest manifest,
        string root,
        string? expectedVersion,
        bool strictSurfaceVersion,
        List<(string Path, string Version)> surfaceVersions,
        List<LoomReleaseSetValidationIssue> issues)
    {
        var path = ResolvePath(root, skill.PackageLock, "package-lock", issues);
        if (!File.Exists(path))
        {
            AddIssue(issues, "missing-package-lock", skill.PackageLock ?? skill.Product ?? "skill", "The active package lock does not exist.");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var rootElement = document.RootElement;
            var version = GetRequiredString(rootElement, "resolved_version");
            ValidateSurfaceVersion(version, skill.PackageLock ?? skill.Product ?? "package-lock", request, manifest, expectedVersion, strictSurfaceVersion, surfaceVersions, issues);
            if (!rootElement.TryGetProperty("runtime_restore", out var restore) || restore.ValueKind != JsonValueKind.Object)
            {
                AddIssue(issues, "package-lock-restore-policy", skill.PackageLock ?? skill.Product ?? "package-lock", "The package lock must declare runtime_restore policy.");
            }
            else
            {
                if (!restore.TryGetProperty("never_float_to_latest", out var neverFloat) || neverFloat.ValueKind != JsonValueKind.True)
                {
                    AddIssue(issues, "floating-package-lock", skill.PackageLock ?? skill.Product ?? "package-lock", "The package lock must explicitly reject latest or floating package resolution.");
                }

                foreach (var floatingValue in FindFloatingValues(restore))
                {
                    AddIssue(issues, "floating-package-lock", skill.PackageLock ?? skill.Product ?? "package-lock", $"The package lock contains a floating version value '{floatingValue}'.");
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            AddIssue(issues, "malformed-package-lock", skill.PackageLock ?? skill.Product ?? "package-lock", $"The package lock is invalid: {exception.Message}");
        }
    }

    private static void ValidateDocumentCopyManifest(
        LoomReleaseSetSkillSurface skill,
        LoomReleaseSetValidationRequest request,
        LoomReleaseSetManifest manifest,
        string root,
        string? expectedVersion,
        bool strictSurfaceVersion,
        bool validatesDocumentCopyEvidence,
        List<(string Path, string Version)> surfaceVersions,
        List<LoomReleaseSetValidationIssue> issues)
    {
        var path = ResolvePath(root, skill.DocumentCopyManifest, "document-copy-manifest", issues);
        if (!File.Exists(path))
        {
            AddIssue(issues, "missing-document-copy-manifest", skill.DocumentCopyManifest ?? skill.Product ?? "skill", "The active document-copy manifest does not exist.");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var rootElement = document.RootElement;
            var targetProduct = GetRequiredString(rootElement, "target_bound_product");
            var targetChannel = GetRequiredString(rootElement, "target_bound_channel");
            var targetVersion = GetRequiredString(rootElement, "target_bound_version");
            if (strictSurfaceVersion && !string.Equals(targetChannel, request.Channel, StringComparison.Ordinal))
            {
                AddIssue(issues, "document-copy-channel", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"The active document-copy manifest uses channel '{targetChannel}' instead of selected channel '{request.Channel}'.");
            }
            if (!string.Equals(targetProduct, skill.Product, StringComparison.Ordinal))
            {
                AddIssue(issues, "document-copy-product", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"The document-copy manifest belongs to '{targetProduct}', not '{skill.Product}'.");
            }
            if (!IsSupportedChannel(targetChannel))
            {
                AddIssue(issues, "document-copy-channel", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"Unsupported document-copy channel '{targetChannel}'.");
            }
            ValidateSurfaceVersion(targetVersion, skill.DocumentCopyManifest ?? skill.Product ?? "manifest", request, manifest, expectedVersion, strictSurfaceVersion, surfaceVersions, issues);

            if (!rootElement.TryGetProperty("documents", out var documents) || documents.ValueKind != JsonValueKind.Array || documents.GetArrayLength() == 0)
            {
                AddIssue(issues, "document-copy-documents", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", "The document-copy manifest must contain at least one document entry.");
                return;
            }

            foreach (var entry in documents.EnumerateArray())
            {
                var targetPath = GetRequiredString(entry, "target_path");
                var sourcePath = GetRequiredString(entry, "source_path");
                var sourceProduct = GetRequiredString(entry, "source_product");
                var sourcePackageId = GetRequiredString(entry, "source_package_id");
                var sourcePackageRid = GetRequiredString(entry, "source_package_rid");
                var sourcePackagePath = GetRequiredString(entry, "source_package_path");
                var sourceChannel = GetRequiredString(entry, "source_channel");
                var sourceVersion = GetRequiredString(entry, "source_version");
                var sourceHash = GetRequiredString(entry, "source_sha256");
                var artifactOrigin = GetRequiredString(entry, "artifact_origin");
                if (!string.Equals(sourceProduct, skill.Product, StringComparison.Ordinal) || !string.Equals(sourceChannel, targetChannel, StringComparison.Ordinal))
                {
                    AddIssue(issues, "document-copy-provenance", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"Document '{targetPath}' has provenance that does not match its target product and channel.");
                }
                if (TryGetProduct(sourceProduct, out var sourceRuntimeProduct))
                {
                    try
                    {
                        var expectedPackageId = LoomRuntimeCatalog.GetPackageId(sourceRuntimeProduct, sourcePackageRid);
                        if (!string.Equals(sourcePackageId, expectedPackageId, StringComparison.Ordinal))
                        {
                            AddIssue(issues, "document-copy-package-id", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"Document '{targetPath}' names runtime package '{sourcePackageId}' instead of '{expectedPackageId}'.");
                        }

                        var normalizedSourcePath = sourcePath.Replace('\\', '/');
                        var sourceFileName = Path.GetFileName(normalizedSourcePath) ?? string.Empty;
                        var expectedPackagePath = $"tools/{sourcePackageRid}/docs/en/guides/{sourceFileName}";
                        if (!string.Equals(sourcePackagePath.Replace('\\', '/'), expectedPackagePath, StringComparison.Ordinal))
                        {
                            AddIssue(issues, "document-copy-package-path", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"Document '{targetPath}' names package path '{sourcePackagePath}' instead of '{expectedPackagePath}'.");
                        }
                    }
                    catch (PlatformNotSupportedException exception)
                    {
                        AddIssue(issues, "document-copy-rid", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", exception.Message);
                    }
                }
                else
                {
                    AddIssue(issues, "document-copy-product", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"Document '{targetPath}' has unsupported source product '{sourceProduct}'.");
                }
                if (!string.Equals(LoomRuntimeCatalog.NormalizeVersion(sourceVersion), LoomRuntimeCatalog.NormalizeVersion(targetVersion), StringComparison.Ordinal))
                {
                    AddIssue(issues, "document-copy-version", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"Document '{targetPath}' has source version '{sourceVersion}' instead of target version '{targetVersion}'.");
                }
                if (!string.Equals(artifactOrigin, "verified-copy", StringComparison.Ordinal))
                {
                    AddIssue(issues, "document-copy-origin", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"Document '{targetPath}' is not marked as a verified copy.");
                }

                var sourceFullPath = ResolvePath(root, sourcePath, "document-copy-source", issues);
                var targetRoot = ResolvePath(root, skill.Root, "skill-root", issues);
                var targetFullPath = ResolvePath(targetRoot, targetPath, "document-copy-target", issues);
                if (!File.Exists(sourceFullPath) || !File.Exists(targetFullPath))
                {
                    AddIssue(issues, "document-copy-file", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"Document '{targetPath}' must have both readable source and target files.");
                    continue;
                }

                if (!validatesDocumentCopyEvidence)
                {
                    continue;
                }

                var contentAuthority = entry.TryGetProperty("content_authority", out var contentAuthorityProperty)
                    && contentAuthorityProperty.ValueKind == JsonValueKind.String
                    ? contentAuthorityProperty.GetString()
                    : "checked-in-source";
                if (contentAuthority is not "checked-in-source" and not "published-package")
                {
                    AddIssue(issues, "document-copy-authority", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"Document '{targetPath}' has unsupported content_authority '{contentAuthority ?? "<missing>"}'.");
                    continue;
                }

                if (!Regex.IsMatch(sourceHash, "^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant))
                {
                    AddIssue(issues, "document-copy-hash", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"Document '{targetPath}' has an invalid SHA-256 source hash.");
                }
                else if (string.Equals(contentAuthority, "published-package", StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(request.PackageRoot))
                    {
                        AddIssue(issues, "document-copy-package-root", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"Document '{targetPath}' uses published-package authority but no package root was supplied for verification.");
                        continue;
                    }

                    var sourcePackageSha512 = entry.TryGetProperty("source_package_sha512", out var sourcePackageSha512Property)
                        && sourcePackageSha512Property.ValueKind == JsonValueKind.String
                        ? sourcePackageSha512Property.GetString()
                        : null;
                    if (string.IsNullOrWhiteSpace(sourcePackageSha512))
                    {
                        AddIssue(issues, "document-copy-package-hash", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"Document '{targetPath}' uses published-package authority but does not declare source_package_sha512.");
                        continue;
                    }

                    ValidatePublishedPackageDocumentCopy(
                        root,
                        request.PackageRoot,
                        sourcePackageId,
                        sourceVersion,
                        sourcePackagePath,
                        targetPath,
                        sourceHash,
                        sourcePackageSha512,
                        targetFullPath,
                        skill.DocumentCopyManifest ?? skill.Product ?? "manifest",
                        issues);
                }
                else
                {
                    var sourceContent = NormalizeDocumentText(File.ReadAllText(sourceFullPath)).TrimEnd();
                    var actualHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sourceContent))).ToLowerInvariant();
                    if (!string.Equals(actualHash, sourceHash, StringComparison.OrdinalIgnoreCase))
                    {
                        AddIssue(issues, "document-copy-hash", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"Document '{targetPath}' source hash does not match the checked-in source file.");
                    }

                    var targetContent = NormalizeDocumentText(File.ReadAllText(targetFullPath));
                    if (string.IsNullOrWhiteSpace(sourceContent) || targetContent.IndexOf(sourceContent, StringComparison.Ordinal) < 0)
                    {
                        AddIssue(issues, "document-copy-content", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"Document '{targetPath}' does not contain the complete source document '{sourcePath}'.");
                    }
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            AddIssue(issues, "malformed-document-copy-manifest", skill.DocumentCopyManifest ?? skill.Product ?? "manifest", $"The document-copy manifest is invalid: {exception.Message}");
        }
    }

    private static void ValidatePublishedPackageDocumentCopy(
        string root,
        string packageRoot,
        string packageId,
        string packageVersion,
        string packageEntryPath,
        string targetPath,
        string sourceHash,
        string packageSha512,
        string targetFullPath,
        string manifestPath,
        List<LoomReleaseSetValidationIssue> issues)
    {
        var resolvedPackageRoot = ResolvePath(root, packageRoot, "document-copy-package-root", issues);
        var packagePath = Path.Combine(resolvedPackageRoot, $"{packageId}.{packageVersion}.nupkg");
        if (!File.Exists(packagePath))
        {
            AddIssue(issues, "document-copy-package-artifact", manifestPath, $"Document '{targetPath}' requires exact package artifact '{Path.Combine(packageRoot, Path.GetFileName(packagePath))}'.");
            return;
        }

        try
        {
            var packageBytes = File.ReadAllBytes(packagePath);
            byte[] expectedPackageHash;
            try
            {
                expectedPackageHash = Convert.FromBase64String(packageSha512);
            }
            catch (FormatException)
            {
                AddIssue(issues, "document-copy-package-hash", manifestPath, $"Document '{targetPath}' has an invalid base64 source_package_sha512.");
                return;
            }

            if (expectedPackageHash.Length != SHA512.HashSizeInBytes
                || !CryptographicOperations.FixedTimeEquals(expectedPackageHash, SHA512.HashData(packageBytes)))
            {
                AddIssue(issues, "document-copy-package-hash", manifestPath, $"Document '{targetPath}' source_package_sha512 does not match the exact nupkg artifact.");
                return;
            }

            using var archive = ZipFile.OpenRead(packagePath);
            var sourceEntry = archive.GetEntry(packageEntryPath);
            if (sourceEntry is null)
            {
                AddIssue(issues, "document-copy-package-entry", manifestPath, $"Document '{targetPath}' source package entry '{packageEntryPath}' was not found in the exact nupkg artifact.");
                return;
            }

            using var reader = new StreamReader(sourceEntry.Open(), System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var packageContent = NormalizeDocumentText(reader.ReadToEnd()).TrimEnd();
            var actualSourceHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(packageContent))).ToLowerInvariant();
            if (!string.Equals(actualSourceHash, sourceHash, StringComparison.OrdinalIgnoreCase))
            {
                AddIssue(issues, "document-copy-package-source-hash", manifestPath, $"Document '{targetPath}' source_sha256 does not match package entry '{packageEntryPath}'.");
                return;
            }

            var targetContent = NormalizeDocumentText(File.ReadAllText(targetFullPath));
            if (string.IsNullOrWhiteSpace(packageContent) || targetContent.IndexOf(packageContent, StringComparison.Ordinal) < 0)
            {
                AddIssue(issues, "document-copy-content", manifestPath, $"Document '{targetPath}' does not contain the complete package guide entry '{packageEntryPath}'.");
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or FormatException)
        {
            AddIssue(issues, "document-copy-package-artifact", manifestPath, $"Document '{targetPath}' exact nupkg verification failed: {exception.Message}");
        }
    }
    private static void ValidateGuides(
        LoomReleaseSetManifest manifest,
        LoomReleaseSetValidationRequest request,
        string root,
        string? expectedVersion,
        bool strictSurfaceVersion,
        List<(string Path, string Version)> surfaceVersions,
        List<LoomReleaseSetValidationIssue> issues)
    {
        foreach (var surface in manifest.Surfaces?.GuideMetadata ?? [])
        {
            var glob = surface.Glob ?? string.Empty;
            var normalizedGlob = glob.Replace('\\', '/');
            var directoryPart = Path.GetDirectoryName(normalizedGlob)?.Replace('/', Path.DirectorySeparatorChar) ?? string.Empty;
            var filePattern = Path.GetFileName(normalizedGlob);
            var directory = ResolvePath(root, directoryPart, "guide-directory", issues);
            if (!Directory.Exists(directory))
            {
                AddIssue(issues, "missing-guide-directory", glob, "The active guide metadata directory does not exist.");
                continue;
            }

            var files = Directory.GetFiles(directory, filePattern, SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                AddIssue(issues, "missing-guide-files", glob, "The active guide metadata glob matched no files.");
                continue;
            }

            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                var versionMatch = GuideVersionPattern.Match(text);
                var buildMatch = GuideBuildPattern.Match(text);
                var displayPath = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
                if (!versionMatch.Success || !buildMatch.Success)
                {
                    AddIssue(issues, "guide-metadata-shape", displayPath, "Each active English guide page must declare Version and Build metadata.");
                    continue;
                }

                var version = versionMatch.Groups["version"].Value;
                var buildVersion = buildMatch.Groups["version"].Value;
                try
                {
                    if (!string.Equals(LoomRuntimeCatalog.NormalizeVersion(version), LoomRuntimeCatalog.NormalizeVersion(buildVersion), StringComparison.Ordinal))
                    {
                        AddIssue(issues, "guide-metadata-mismatch", displayPath, $"Version '{version}' and Build version '{buildVersion}' disagree.");
                    }
                }
                catch (Exception exception) when (exception is ArgumentException or FormatException)
                {
                    AddIssue(issues, "guide-invalid-version", displayPath, $"Guide version metadata is invalid: {exception.Message}");
                    continue;
                }
                ValidateSurfaceVersion(version, displayPath, request, manifest, expectedVersion, strictSurfaceVersion, surfaceVersions, issues);
            }
        }
    }

    private static void ValidateCi(LoomReleaseSetManifest manifest, string root, List<LoomReleaseSetValidationIssue> issues)
    {
        var ci = manifest.Ci;
        if (ci is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ci.SharedVersionJobId) || string.IsNullOrWhiteSpace(ci.PackageVersionOutput))
        {
            AddIssue(issues, "ci-version-contract", "ci", "CI must declare a shared version job id and package_version output expression.");
        }

        foreach (var workflowRelativePath in ci.WorkflowPaths ?? [])
        {
            var path = ResolvePath(root, workflowRelativePath, "ci-workflow", issues);
            if (!File.Exists(path))
            {
                AddIssue(issues, "missing-ci-workflow", workflowRelativePath, "The release workflow does not exist.");
                continue;
            }

            var text = File.ReadAllText(path);
            var executeCount = CountOccurrences(text, "gittools/actions/gitversion/execute@");
            if (executeCount != 1)
            {
                AddIssue(issues, "duplicate-version-authority", workflowRelativePath, $"The workflow must execute GitVersion exactly once in the shared version job, but found {executeCount} executions.");
            }
            if (!text.Contains("needs.version.outputs.package_version", StringComparison.Ordinal))
            {
                AddIssue(issues, "missing-shared-package-version", workflowRelativePath, "Runtime packaging and publishing must consume needs.version.outputs.package_version.");
            }
            if (!string.IsNullOrWhiteSpace(ci.SharedVersionJobId) && !Regex.IsMatch(text, $@"(?m)^\s{{2}}{Regex.Escape(ci.SharedVersionJobId)}:\s*$", RegexOptions.CultureInvariant))
            {
                AddIssue(issues, "missing-shared-version-job", workflowRelativePath, $"The workflow must declare the shared '{ci.SharedVersionJobId}' job.");
            }

            foreach (var line in text.Split(["\r\n", "\n"], StringSplitOptions.None))
            {
                var isPackageCommand = line.Contains("dotnet add package", StringComparison.OrdinalIgnoreCase) ||
                                       line.Contains("dotnet restore", StringComparison.OrdinalIgnoreCase) ||
                                       line.Contains("dotnet pack", StringComparison.OrdinalIgnoreCase);
                if (!isPackageCommand)
                {
                    continue;
                }

                if (Regex.IsMatch(line, @"--version\s+(?:latest|\*|\[)|(?:Version|PackageVersion)=latest|--prerelease\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    AddIssue(issues, "floating-automation-dependency", workflowRelativePath, $"Automation package command uses a floating version: {line.Trim()}");
                }
            }

            foreach (var forbiddenPattern in ci.ForbiddenAutomationPatterns ?? [])
            {
                if (!string.IsNullOrWhiteSpace(forbiddenPattern) && text.Contains(forbiddenPattern, StringComparison.OrdinalIgnoreCase))
                {
                    AddIssue(issues, "forbidden-automation-pattern", workflowRelativePath, $"The workflow contains forbidden automation text '{forbiddenPattern}'.");
                }
            }
        }
    }

    private static void ValidateExcludedArtifacts(LoomReleaseSetManifest manifest, string root, List<LoomReleaseSetValidationIssue> issues)
    {
        foreach (var artifact in manifest.ExcludedArtifacts ?? [])
        {
            if (!SupportedExcludedClassifications.Contains(artifact.Classification ?? string.Empty))
            {
                AddIssue(issues, "unclassified-excluded-artifact", artifact.Path ?? "excluded_artifact", "Historical, audit, test, source-debug, or synthetic artifacts must declare a supported classification.");
            }
            if (string.IsNullOrWhiteSpace(artifact.Reason))
            {
                AddIssue(issues, "excluded-artifact-reason", artifact.Path ?? "excluded_artifact", "Excluded artifacts must explain why they are outside the active release set.");
            }

            var path = ResolvePath(root, artifact.Path, "excluded-artifact", issues);
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                AddIssue(issues, "missing-excluded-artifact", artifact.Path ?? "excluded_artifact", "The classified artifact does not exist.");
            }
        }
    }

    private static void ValidateSurfaceVersion(
        string version,
        string path,
        LoomReleaseSetValidationRequest request,
        LoomReleaseSetManifest manifest,
        string? expectedVersion,
        bool strictSurfaceVersion,
        List<(string Path, string Version)> surfaceVersions,
        List<LoomReleaseSetValidationIssue> issues)
    {
        string normalized;
        try
        {
            normalized = LoomRuntimeCatalog.NormalizeVersion(version);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            AddIssue(issues, "invalid-surface-version", path, exception.Message);
            return;
        }

        if (!IsManifestChannelVersion(manifest, request.Channel, normalized) && strictSurfaceVersion)
        {
            AddIssue(issues, "surface-channel-version", path, $"Version '{normalized}' does not belong to selected channel '{request.Channel}'.");
        }
        if (strictSurfaceVersion && expectedVersion is not null && !string.Equals(normalized, expectedVersion, StringComparison.Ordinal))
        {
            AddIssue(issues, "stale-surface-version", path, $"Active surface uses '{normalized}' instead of '{expectedVersion}'.");
        }

        surfaceVersions.Add((path, normalized));
    }

    private static IEnumerable<string> ExtractActivePackageIndexVersionLiterals(string text)
    {
        foreach (var line in text.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("- Bad:", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("Bad:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (Match match in VersionLiteralPattern.Matches(line))
            {
                yield return match.Groups["version"].Value;
            }
        }
    }

    private static string NormalizeDocumentText(string content)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            lines[index] = lines[index].TrimEnd();
        }

        return string.Join("\n", lines);
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new KeyNotFoundException(propertyName);
        }

        return property.GetString()!;
    }

    private static IEnumerable<string> FindFloatingValues(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString() ?? string.Empty;
            if (value.Equals("latest", StringComparison.OrdinalIgnoreCase) || value.IndexOf('*') >= 0 || value.StartsWith("[", StringComparison.Ordinal))
            {
                yield return value;
            }
            yield break;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                foreach (var value in FindFloatingValues(property.Value))
                {
                    yield return value;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                foreach (var value in FindFloatingValues(child))
                {
                    yield return value;
                }
            }
        }
    }

    private static void ValidateRelativeManifestPath(string? path, string root, string label, List<LoomReleaseSetValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            AddIssue(issues, "missing-manifest-path", label, "A relative path is required.");
            return;
        }

        _ = ResolvePath(root, path, label, issues);
    }

    private static bool TryGetProduct(string? value, out LoomRuntimeProduct product)
    {
        product = value switch
        {
            "ao" => LoomRuntimeProduct.AgentOrchestrator,
            "so" => LoomRuntimeProduct.SkillOrchestrator,
            _ => default,
        };
        return value is "ao" or "so";
    }

    private static bool IsSupportedChannel(string channel)
        => channel is "released" or "beta";

    private static bool IsManifestChannelVersion(LoomReleaseSetManifest manifest, string channel, string version)
    {
        if (!IsChannelVersion(version, channel) || manifest.Channels is null || !manifest.Channels.TryGetValue(channel, out var rule) || string.IsNullOrWhiteSpace(rule?.VersionPattern))
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(version, rule.VersionPattern, RegexOptions.CultureInvariant);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsChannelVersion(string version, string channel)
        => channel switch
        {
            "released" => !version.Contains('-', StringComparison.Ordinal),
            "beta" => version.EndsWith("-beta", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };

    private static int CountOccurrences(string value, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static void AddIssue(List<LoomReleaseSetValidationIssue> issues, string code, string path, string message)
        => issues.Add(new LoomReleaseSetValidationIssue(code, path, message));
}
