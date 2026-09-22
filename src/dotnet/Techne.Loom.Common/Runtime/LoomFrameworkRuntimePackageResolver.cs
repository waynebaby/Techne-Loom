using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
namespace Techne.Loom.Common.Runtime;
internal sealed class LoomFrameworkRuntimePackageResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly HttpClient _httpClient;
    private readonly LoomRuntimePackageLimits _limits;
    public LoomFrameworkRuntimePackageResolver(HttpClient httpClient, LoomRuntimePackageLimits limits)
    {
        _httpClient = httpClient;
        _limits = limits;
    }
    public async Task<LoomFrameworkRuntimePackageBundle> ResolveAsync(
        LoomRuntimeProduct product,
        string version,
        string channel,
        string runtimeIdentifier,
        string cacheRoot,
        string? nuGetPackageCacheRoot,
        TimeSpan lockTimeout,
        CancellationToken cancellationToken)
    {
        var normalizedVersion = LoomRuntimeCatalog.NormalizeVersion(version);
        var bundleDirectory = Path.Combine(cacheRoot, GetProductName(product), normalizedVersion, "dotnet-cli");
        var lockPath = Path.Combine(cacheRoot, ".locks", $"{GetProductName(product)}.{normalizedVersion}.dotnet-cli.lock");
        await using var cacheLock = await AcquireCacheLockAsync(lockPath, lockTimeout, cancellationToken).ConfigureAwait(false);
        var cached = TryReadCachedBundle(bundleDirectory, product, normalizedVersion);
        if (cached is not null)
        {
            return cached;
        }
        var metadata = await ResolveMetadataClosureAsync(
            product,
            normalizedVersion,
            nuGetPackageCacheRoot,
            cancellationToken).ConfigureAwait(false);
        var artifacts = new List<FrameworkPackageArtifact>(metadata.Count);
        foreach (var package in metadata)
        {
            artifacts.Add(await AcquirePackageAsync(
                package,
                normalizedVersion,
                channel,
                nuGetPackageCacheRoot,
                cancellationToken).ConfigureAwait(false));
        }
        return await PublishBundleAsync(
            bundleDirectory,
            product,
            normalizedVersion,
            runtimeIdentifier,
            artifacts,
            cancellationToken).ConfigureAwait(false);
    }
    private async Task<IReadOnlyList<FrameworkPackageMetadata>> ResolveMetadataClosureAsync(
        LoomRuntimeProduct product,
        string version,
        string? nuGetPackageCacheRoot,
        CancellationToken cancellationToken)
    {
        var root = new FrameworkPackageCoordinate(LoomRuntimeCatalog.GetProductPackageId(product), version);
        var pending = new Queue<FrameworkPackageCoordinate>([root]);
        var resolved = new Dictionary<string, FrameworkPackageMetadata>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        while (pending.Count > 0)
        {
            var coordinate = pending.Dequeue();
            if (resolved.TryGetValue(coordinate.Id, out var existing))
            {
                if (!string.Equals(existing.Version, coordinate.Version, StringComparison.Ordinal))
                {
                    throw new LoomRuntimeAcquisitionException($"Framework dependency '{coordinate.Id}' resolves to conflicting exact versions '{existing.Version}' and '{coordinate.Version}'.");
                }
                continue;
            }
            var metadata = await ReadMetadataAsync(coordinate, nuGetPackageCacheRoot, cancellationToken).ConfigureAwait(false);
            resolved.Add(coordinate.Id, metadata);
            order.Add(coordinate.Id);
            foreach (var dependency in metadata.Dependencies)
            {
                pending.Enqueue(new FrameworkPackageCoordinate(dependency.Id, dependency.Version));
            }
        }
        return order.Select(id => resolved[id]).ToArray();
    }
    private async Task<FrameworkPackageMetadata> ReadMetadataAsync(
        FrameworkPackageCoordinate coordinate,
        string? nuGetPackageCacheRoot,
        CancellationToken cancellationToken)
    {
        var localPath = FindLocalPackagePath(coordinate.Id, coordinate.Version, nuGetPackageCacheRoot);
        if (localPath is not null)
        {
            try
            {
                var localBytes = await File.ReadAllBytesAsync(localPath, cancellationToken).ConfigureAwait(false);
                return ValidateAndReadNuspec(localBytes, coordinate.Id, coordinate.Version);
            }
            catch (LoomRuntimeIntegrityException)
            {
            }
            catch (IOException)
            {
            }
        }
        var registrationUrl = LoomRuntimeCatalog.GetNuGetRegistrationUrl(coordinate.Id, coordinate.Version);
        string json;
        try
        {
            using var response = await _httpClient.GetAsync(registrationUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new LoomRuntimeAcquisitionException($"NuGet metadata for '{coordinate.Id}/{coordinate.Version}' returned {(int)response.StatusCode}.");
            }
            var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            json = DecodeRegistrationJson(responseBytes, response.Content.Headers.ContentEncoding);

        }
        catch (HttpRequestException exception)
        {
            throw new LoomRuntimeAcquisitionException($"NuGet metadata for '{coordinate.Id}/{coordinate.Version}' could not be read.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LoomRuntimeAcquisitionException($"NuGet metadata for '{coordinate.Id}/{coordinate.Version}' timed out.", exception);
        }
        return await ReadRegistrationMetadataAsync(json, coordinate, cancellationToken).ConfigureAwait(false);
    }
    private async Task<FrameworkPackageArtifact> AcquirePackageAsync(
        FrameworkPackageMetadata metadata,
        string rootVersion,
        string channel,
        string? nuGetPackageCacheRoot,
        CancellationToken cancellationToken)
    {
        var localPath = FindLocalPackagePath(metadata.Id, metadata.Version, nuGetPackageCacheRoot);
        if (localPath is not null)
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(localPath, cancellationToken).ConfigureAwait(false);
                var hashPath = localPath + ".sha512";
                var hashText = File.Exists(hashPath)
                    ? await File.ReadAllTextAsync(hashPath, cancellationToken).ConfigureAwait(false)
                    : null;
                var packageHash = hashText is null
                    ? Convert.ToBase64String(SHA512.HashData(bytes))
                    : LoomRuntimePackageValidator.NormalizeAndValidateSha512(bytes, hashText);
                ValidateAndReadNuspec(bytes, metadata.Id, metadata.Version);
                return new FrameworkPackageArtifact(metadata, bytes, packageHash, LoomRuntimeCatalog.GetNuGetPackageUrl(metadata.Id, metadata.Version), LoomRuntimeCatalog.GetNuGetHashUrl(metadata.Id, metadata.Version));
            }
            catch (LoomRuntimeIntegrityException)
            {
            }
            catch (IOException)
            {
            }
        }
        var sources = new[]
        {
            new FrameworkPackageSource(LoomRuntimeCatalog.GetNuGetPackageUrl(metadata.Id, metadata.Version), LoomRuntimeCatalog.GetNuGetHashUrl(metadata.Id, metadata.Version)),
            new FrameworkPackageSource(LoomRuntimeCatalog.GetGitHubPackageUrl(metadata.Id, metadata.Version, channel), LoomRuntimeCatalog.GetGitHubPackageUrl(metadata.Id, metadata.Version, channel) + ".sha512"),
        };
        foreach (var source in sources)
        {
            var bytes = await TryDownloadBytesAsync(source.PackageUrl, cancellationToken).ConfigureAwait(false);
            if (bytes is null)
            {
                continue;
            }
            var hashText = await TryDownloadTextAsync(source.HashUrl, cancellationToken).ConfigureAwait(false);
            if (hashText is null)
            {
                continue;
            }
            var packageHash = LoomRuntimePackageValidator.NormalizeAndValidateSha512(bytes, hashText);
            ValidateAndReadNuspec(bytes, metadata.Id, metadata.Version);
            return new FrameworkPackageArtifact(metadata, bytes, packageHash, source.PackageUrl, source.HashUrl);
        }
        throw new LoomRuntimeAcquisitionException($"Unable to acquire exact framework package '{metadata.Id}' version '{metadata.Version}' for root runtime version '{rootVersion}'.");
    }
    private async Task<LoomFrameworkRuntimePackageBundle> PublishBundleAsync(
        string bundleDirectory,
        LoomRuntimeProduct product,
        string version,
        string runtimeIdentifier,
        IReadOnlyList<FrameworkPackageArtifact> artifacts,
        CancellationToken cancellationToken)
    {
        var temporaryDirectory = bundleDirectory + $".tmp-{Guid.NewGuid():N}";
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var packageRoot = Path.Combine(temporaryDirectory, "packages");
            Directory.CreateDirectory(packageRoot);
            var selectedAssets = new Dictionary<string, FrameworkAsset>(StringComparer.OrdinalIgnoreCase);
            FrameworkPackageArtifact? productArtifact = null;
            foreach (var artifact in artifacts)
            {
                if (string.Equals(artifact.Metadata.Id, LoomRuntimeCatalog.GetProductPackageId(product), StringComparison.OrdinalIgnoreCase))
                {
                    productArtifact = artifact;
                }
                WritePackageCache(packageRoot, artifact);
                CollectFrameworkAssets(artifact.Metadata.Id, artifact.Bytes, selectedAssets);
            }
            if (productArtifact is null)
            {
                throw new LoomRuntimeIntegrityException($"Framework package closure does not contain product package '{LoomRuntimeCatalog.GetProductPackageId(product)}'.");
            }
            ValidateRawProductPackageShape(productArtifact, product);
            foreach (var asset in selectedAssets.Values)
            {
                var artifact = artifacts.Single(item => string.Equals(item.Metadata.Id, asset.PackageId, StringComparison.OrdinalIgnoreCase));
                var bytes = ReadEntryBytes(artifact.Bytes, asset.EntryPath);
                await File.WriteAllBytesAsync(Path.Combine(temporaryDirectory, asset.FileName), bytes, cancellationToken).ConfigureAwait(false);
            }
            var entryPoint = LoomRuntimeCatalog.GetEntryPoint(product) + ".dll";
            var launchFile = Path.Combine(temporaryDirectory, entryPoint);
            if (!File.Exists(launchFile))
            {
                throw new LoomRuntimeIntegrityException($"Framework package closure does not contain '{entryPoint}'.");
            }
            using (var productArchive = OpenArchive(productArtifact.Bytes, productArtifact.Metadata.Id))
            {
                var runtimeConfigEntry = productArchive.Entries
                    .Where(entry => !entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    .Select(entry => (Entry: entry, Path: NormalizeEntryPath(entry.FullName)))
                    .Where(item => item.Path.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(item => GetTargetFrameworkRank(item.Path.Split('/').Length > 1 ? item.Path.Split('/')[1] : string.Empty))
                    .Select(item => item.Entry)
                    .FirstOrDefault();
                if (runtimeConfigEntry is null)
                {
                    throw new LoomRuntimeIntegrityException($"Framework product package '{productArtifact.Metadata.Id}' does not contain a runtimeconfig file.");
                }
                await using var input = runtimeConfigEntry.Open();
                await using var output = new FileStream(Path.Combine(temporaryDirectory, LoomRuntimeCatalog.GetEntryPoint(product) + ".runtimeconfig.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                foreach (var entry in productArchive.Entries)
                {
                    var path = NormalizeEntryPath(entry.FullName);
                    const string docsPrefix = "docs/en/";
                    if (!path.StartsWith(docsPrefix, StringComparison.Ordinal) || entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    var relativePath = path[docsPrefix.Length..];
                    var destination = ResolvePathWithinRoot(Path.Combine(temporaryDirectory, "docs", "en"), relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    await using var docsInput = entry.Open();
                    await using var docsOutput = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    await docsInput.CopyToAsync(docsOutput, cancellationToken).ConfigureAwait(false);
                    await docsOutput.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            var depsFile = Path.Combine(temporaryDirectory, LoomRuntimeCatalog.GetEntryPoint(product) + ".deps.json");
            var runtimeConfigFile = Path.Combine(temporaryDirectory, LoomRuntimeCatalog.GetEntryPoint(product) + ".runtimeconfig.json");
            var depsJson = CreateDepsJson(product, version, artifacts, selectedAssets);
            await File.WriteAllTextAsync(depsFile, depsJson + Environment.NewLine, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            ValidateGeneratedDepsFile(
                depsFile,
                temporaryDirectory,
                product,
                version,
                artifacts.ToDictionary(artifact => artifact.Metadata.Id, artifact => artifact.Metadata.Version, StringComparer.OrdinalIgnoreCase),
                selectedAssets.Keys.ToArray());
            var lockFile = Path.Combine(temporaryDirectory, "framework.lock.json");
            var lockPayload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["schema"] = "techne-loom-framework-runtime-v1",
                ["product"] = LoomRuntimeCatalog.GetEntryPoint(product),
                ["version"] = version,
                ["runtime_identifier"] = runtimeIdentifier,
                ["packages"] = artifacts.Select(artifact => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = artifact.Metadata.Id,
                    ["version"] = artifact.Metadata.Version,
                    ["hash"] = artifact.PackageHash,
                    ["package_url"] = artifact.PackageUrl,
                    ["hash_url"] = artifact.HashUrl,
                }).ToArray(),
            };
            await File.WriteAllTextAsync(lockFile, JsonSerializer.Serialize(lockPayload, JsonOptions) + Environment.NewLine, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            var displacedDirectory = bundleDirectory + $".stale-{Guid.NewGuid():N}";
            if (Directory.Exists(bundleDirectory))
            {
                Directory.Move(bundleDirectory, displacedDirectory);
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(bundleDirectory)!);
                Directory.Move(temporaryDirectory, bundleDirectory);
            }
            catch
            {
                if (Directory.Exists(displacedDirectory) && !Directory.Exists(bundleDirectory))
                {
                    Directory.Move(displacedDirectory, bundleDirectory);
                }
                throw;
            }
            finally
            {
                if (Directory.Exists(displacedDirectory))
                {
                    Directory.Delete(displacedDirectory, recursive: true);
                }
            }
            return CreateBundle(bundleDirectory, product, artifacts);
        }
        catch
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
            throw;
        }
    }
    private LoomFrameworkRuntimePackageBundle? TryReadCachedBundle(
        string bundleDirectory,
        LoomRuntimeProduct product,
        string version)
    {
        var lockPath = Path.Combine(bundleDirectory, "framework.lock.json");
        var entryPoint = LoomRuntimeCatalog.GetEntryPoint(product);
        var depsFile = Path.Combine(bundleDirectory, entryPoint + ".deps.json");
        var runtimeConfigFile = Path.Combine(bundleDirectory, entryPoint + ".runtimeconfig.json");
        var launchFile = Path.Combine(bundleDirectory, entryPoint + ".dll");
        if (!File.Exists(lockPath) || !File.Exists(depsFile) || !File.Exists(runtimeConfigFile) || !File.Exists(launchFile))
        {
            return null;
        }
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(lockPath));
            var root = document.RootElement;
            if (!string.Equals(GetString(root, "schema"), "techne-loom-framework-runtime-v1", StringComparison.Ordinal)
                || !string.Equals(GetString(root, "product"), entryPoint, StringComparison.Ordinal)
                || !string.Equals(LoomRuntimeCatalog.NormalizeVersion(GetString(root, "version")), version, StringComparison.Ordinal)
                || !root.TryGetProperty("packages", out var packages)
                || packages.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            var packageIds = new List<string>();
            var packageVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var urls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hashUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var selectedAssets = new Dictionary<string, FrameworkAsset>(StringComparer.OrdinalIgnoreCase);
            foreach (var package in packages.EnumerateArray())
            {
                var id = GetString(package, "id");
                var packageVersion = LoomRuntimeCatalog.NormalizeVersion(GetString(package, "version"));
                var hash = GetString(package, "hash");
                if (packageIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                {
                    return null;
                }

                packageIds.Add(id);
                packageVersions[id] = packageVersion;
                hashes[id] = hash;
                urls[id] = GetString(package, "package_url");
                hashUrls[id] = GetString(package, "hash_url");
                var packagePath = Path.Combine(bundleDirectory, "packages", id.ToLowerInvariant(), packageVersion, $"{id.ToLowerInvariant()}.{packageVersion}.nupkg");
                if (!File.Exists(packagePath))
                {
                    return null;
                }
                var bytes = File.ReadAllBytes(packagePath);
                if (!CryptographicOperations.FixedTimeEquals(SHA512.HashData(bytes), Convert.FromBase64String(hash)))
                {
                    return null;
                }

                ValidateAndReadNuspec(bytes, id, packageVersion);
                CollectFrameworkAssets(id, bytes, selectedAssets);
            }
            ValidateGeneratedDepsFile(depsFile, bundleDirectory, product, version, packageVersions, selectedAssets.Keys.ToArray());
            var docsRoot = Path.Combine(bundleDirectory, "docs", "en");
            var guidePath = Path.Combine(docsRoot, "guides", entryPoint + "-guide.md");
            if (!File.Exists(guidePath))
            {
                return null;
            }
            return new LoomFrameworkRuntimePackageBundle(bundleDirectory, launchFile, depsFile, runtimeConfigFile, OrderPackageIds(product, packageIds), hashes, urls, hashUrls);
        }
        catch (Exception exception) when (exception is LoomRuntimeIntegrityException or IOException or JsonException or FormatException or KeyNotFoundException)
        {
            return null;
        }
    }
    private static LoomFrameworkRuntimePackageBundle CreateBundle(
        string bundleDirectory,
        LoomRuntimeProduct product,
        IReadOnlyList<FrameworkPackageArtifact> artifacts)
    {
        var entryPoint = artifacts[0].Metadata.Id.Contains("AgentOrchestrator", StringComparison.OrdinalIgnoreCase) ? "ao" : "so";
        var packageIds = OrderPackageIds(product, artifacts.Select(artifact => artifact.Metadata.Id));
        var hashes = artifacts.ToDictionary(artifact => artifact.Metadata.Id, artifact => artifact.PackageHash, StringComparer.OrdinalIgnoreCase);
        var urls = artifacts.ToDictionary(artifact => artifact.Metadata.Id, artifact => artifact.PackageUrl, StringComparer.OrdinalIgnoreCase);
        var hashUrls = artifacts.ToDictionary(artifact => artifact.Metadata.Id, artifact => artifact.HashUrl, StringComparer.OrdinalIgnoreCase);
        return new LoomFrameworkRuntimePackageBundle(
            bundleDirectory,
            Path.Combine(bundleDirectory, entryPoint + ".dll"),
            Path.Combine(bundleDirectory, entryPoint + ".deps.json"),
            Path.Combine(bundleDirectory, entryPoint + ".runtimeconfig.json"),
            packageIds,
            hashes,
            urls,
            hashUrls);
    }
    private static IReadOnlyList<string> OrderPackageIds(LoomRuntimeProduct product, IEnumerable<string> packageIds)
    {
        var distinct = packageIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var ordered = new List<string>();
        foreach (var required in new[]
        {
            LoomRuntimeCatalog.GetProductPackageId(product),
            "Techne.Loom.Common",
            "Techne.Loom.Abstractions",
        })
        {
            var match = distinct.FirstOrDefault(id => string.Equals(id, required, StringComparison.OrdinalIgnoreCase));
            if (match is not null) ordered.Add(match);
        }

        ordered.AddRange(distinct.Where(id => !ordered.Contains(id, StringComparer.OrdinalIgnoreCase)));
        return ordered;
    }

    private static void CollectFrameworkAssets(
        string packageId,
        byte[] bytes,
        IDictionary<string, FrameworkAsset> selectedAssets)
    {
        using var archive = OpenArchive(bytes, packageId);
        foreach (var entry in archive.Entries)
        {
            var path = NormalizeEntryPath(entry.FullName);
            if (!IsLibraryAssembly(path))
            {
                continue;
            }

            var targetFramework = path.Split('/')[1];
            var fileName = Path.GetFileName(path);
            if (fileName.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var asset = new FrameworkAsset(packageId, path, fileName, GetTargetFrameworkRank(targetFramework));
            if (!selectedAssets.TryGetValue(fileName, out var existing) || asset.Rank > existing.Rank)
            {
                selectedAssets[fileName] = asset;
            }
        }
    }

    private static void ValidateRawProductPackageShape(FrameworkPackageArtifact productArtifact, LoomRuntimeProduct product)
    {
        using var archive = OpenArchive(productArtifact.Bytes, productArtifact.Metadata.Id);
        var entryPoint = LoomRuntimeCatalog.GetEntryPoint(product);
        var requiredPaths = new[]
        {
            $"lib/net9.0/{entryPoint}.dll",
            $"lib/net9.0/{entryPoint}.runtimeconfig.json",
            $"docs/en/guides/{entryPoint}-guide.md",
        };
        var paths = archive.Entries
            .Where(entry => !entry.FullName.EndsWith("/", StringComparison.Ordinal))
            .Select(entry => NormalizeEntryPath(entry.FullName))
            .ToHashSet(StringComparer.Ordinal);
        var missingPaths = requiredPaths.Where(path => !paths.Contains(path)).ToArray();
        if (missingPaths.Length > 0)
        {
            throw new LoomRuntimeIntegrityException(
                $"Raw framework product package '{productArtifact.Metadata.Id}/{productArtifact.Metadata.Version}' is missing required input files: {string.Join(", ", missingPaths)}. The raw package is not a runnable framework bundle; the resolver must stage and generate '{entryPoint}.deps.json'.");
        }
    }

    private static void ValidateGeneratedDepsFile(
        string depsFile,
        string bundleDirectory,
        LoomRuntimeProduct product,
        string expectedVersion,
        IReadOnlyDictionary<string, string> packageVersions,
        IReadOnlyCollection<string> expectedRuntimeAssets)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(depsFile));
            var root = document.RootElement;
            if (!root.TryGetProperty("runtimeTarget", out var runtimeTarget)
                || runtimeTarget.ValueKind != JsonValueKind.Object
                || !string.Equals(GetString(runtimeTarget, "name"), ".NETCoreApp,Version=v9.0", StringComparison.Ordinal))
            {
                throw new LoomRuntimeIntegrityException($"Generated framework dependency manifest '{depsFile}' must target .NETCoreApp,Version=v9.0.");
            }

            if (!root.TryGetProperty("targets", out var targets) || targets.ValueKind != JsonValueKind.Object
                || !targets.TryGetProperty(".NETCoreApp,Version=v9.0", out var target)
                || target.ValueKind != JsonValueKind.Object)
            {
                throw new LoomRuntimeIntegrityException($"Generated framework dependency manifest '{depsFile}' does not contain the .NET 9 target map.");
            }

            if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Object)
            {
                throw new LoomRuntimeIntegrityException($"Generated framework dependency manifest '{depsFile}' does not contain a valid libraries map.");
            }

            var expectedKeys = packageVersions.Select(pair => GetLibraryKey(product, pair.Key, pair.Value)).ToHashSet(StringComparer.Ordinal);
            var targetKeys = target.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
            var libraryKeys = libraries.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
            var missing = expectedKeys.Except(targetKeys.Union(libraryKeys)).OrderBy(key => key, StringComparer.Ordinal).ToArray();
            var unexpected = targetKeys.Union(libraryKeys).Except(expectedKeys).OrderBy(key => key, StringComparer.Ordinal).ToArray();
            if (missing.Length > 0 || unexpected.Length > 0 || !targetKeys.SetEquals(libraryKeys))
            {
                throw new LoomRuntimeIntegrityException(
                    $"Generated framework dependency manifest '{depsFile}' does not match the exact package closure for version '{expectedVersion}'. Missing: [{string.Join(", ", missing)}]. Unexpected or inconsistent: [{string.Join(", ", unexpected)}].");
            }

            var expectedRuntimeAssetNames = expectedRuntimeAssets.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var actualRuntimeAssetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var library in target.EnumerateObject())
            {
                if (!library.Value.TryGetProperty("runtime", out var runtime) || runtime.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var asset in runtime.EnumerateObject())
                {
                    actualRuntimeAssetNames.Add(asset.Name);
                    if (string.IsNullOrWhiteSpace(asset.Name)
                        || asset.Name.Contains('/', StringComparison.Ordinal)
                        || asset.Name.Contains('\\', StringComparison.Ordinal)
                        || asset.Name.Contains(':', StringComparison.Ordinal)
                        || !string.Equals(Path.GetFileName(asset.Name), asset.Name, StringComparison.Ordinal))
                    {
                        throw new LoomRuntimeIntegrityException(
                            $"Generated framework dependency manifest '{depsFile}' contains a runtime asset path '{asset.Name}' that is not a file in the flattened bundle root.");
                    }

                    var assetPath = Path.Combine(bundleDirectory, asset.Name);
                    if (!File.Exists(assetPath))
                    {
                        throw new LoomRuntimeIntegrityException(
                            $"Generated framework dependency manifest '{depsFile}' references runtime asset '{asset.Name}', but the flattened bundle file '{assetPath}' is missing.");
                    }
                }
            }

            var missingRuntimeAssets = expectedRuntimeAssetNames.Except(actualRuntimeAssetNames, StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            var unexpectedRuntimeAssets = actualRuntimeAssetNames.Except(expectedRuntimeAssetNames, StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            if (missingRuntimeAssets.Length > 0 || unexpectedRuntimeAssets.Length > 0)
            {
                throw new LoomRuntimeIntegrityException(
                    $"Generated framework dependency manifest '{depsFile}' does not contain the exact flattened runtime asset set. Missing: [{string.Join(", ", missingRuntimeAssets)}]. Unexpected: [{string.Join(", ", unexpectedRuntimeAssets)}].");
            }
        }
        catch (LoomRuntimeIntegrityException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidOperationException or UnauthorizedAccessException or KeyNotFoundException)
        {
            throw new LoomRuntimeIntegrityException($"Generated framework dependency manifest '{depsFile}' is unreadable or invalid.", exception);
        }
    }

    private static string CreateDepsJson(
        LoomRuntimeProduct product,
        string version,
        IReadOnlyList<FrameworkPackageArtifact> artifacts,
        IReadOnlyDictionary<string, FrameworkAsset> selectedAssets)
    {
        var target = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var artifact in artifacts)
        {
            var package = new Dictionary<string, object?>(StringComparer.Ordinal);
            var dependencies = artifact.Metadata.Dependencies
                .ToDictionary(
                    dependency => GetLibraryKey(product, dependency.Id, dependency.Version),
                    dependency => (object?)dependency.Version,
                    StringComparer.Ordinal);
            if (dependencies.Count > 0)
            {
                package["dependencies"] = dependencies;
            }
            var runtime = selectedAssets.Values
                .Where(asset => string.Equals(asset.PackageId, artifact.Metadata.Id, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(asset => asset.FileName, _ => (object?)new Dictionary<string, object?>(), StringComparer.Ordinal);
            if (runtime.Count > 0)
            {
                package["runtime"] = runtime;
            }
            target[GetLibraryKey(product, artifact.Metadata.Id, artifact.Metadata.Version)] = package;
        }
        var libraries = artifacts.ToDictionary(
            artifact => GetLibraryKey(product, artifact.Metadata.Id, artifact.Metadata.Version),
            artifact => (object?)new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = "package",
                ["serviceable"] = true,
                ["sha512"] = "sha512-" + artifact.PackageHash,
                ["path"] = artifact.Metadata.Id.ToLowerInvariant() + "/" + artifact.Metadata.Version.ToLowerInvariant(),
                ["hashPath"] = artifact.Metadata.Id.ToLowerInvariant() + "." + artifact.Metadata.Version.ToLowerInvariant() + ".nupkg.sha512",
            },
            StringComparer.Ordinal);
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["runtimeTarget"] = new Dictionary<string, object?> { ["name"] = ".NETCoreApp,Version=v9.0", ["signature"] = "" },
            ["compilationOptions"] = new Dictionary<string, object?>(),
            ["targets"] = new Dictionary<string, object?> { [".NETCoreApp,Version=v9.0"] = target },
            ["libraries"] = libraries,
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }
    private static string GetLibraryKey(LoomRuntimeProduct product, string packageId, string version)
        => string.Equals(packageId, LoomRuntimeCatalog.GetProductPackageId(product), StringComparison.OrdinalIgnoreCase)
            ? LoomRuntimeCatalog.GetEntryPoint(product) + "/" + version
            : packageId + "/" + version;
    private static void WritePackageCache(string packageRoot, FrameworkPackageArtifact artifact)
    {
        var directory = Path.Combine(packageRoot, artifact.Metadata.Id.ToLowerInvariant(), artifact.Metadata.Version.ToLowerInvariant());
        Directory.CreateDirectory(directory);
        var packagePath = Path.Combine(directory, artifact.Metadata.Id.ToLowerInvariant() + "." + artifact.Metadata.Version.ToLowerInvariant() + ".nupkg");
        File.WriteAllBytes(packagePath, artifact.Bytes);
        File.WriteAllText(packagePath + ".sha512", artifact.PackageHash, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, "package.url"), artifact.PackageUrl, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, "package.hash.url"), artifact.HashUrl, new UTF8Encoding(false));
    }
    private void ValidateArchiveEntries(ZipArchive archive, string packageId)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        long totalBytes = 0;
        foreach (var entry in archive.Entries)
        {
            var path = NormalizeEntryPath(entry.FullName);
            if (!paths.Add(path))
            {
                throw new LoomRuntimeIntegrityException($"NuGet package '{packageId}' contains a duplicate ZIP path '{path}'.");
            }

            if (entry.Length > _limits.MaxEntryBytes)
            {
                throw new LoomRuntimeIntegrityException($"NuGet package '{packageId}' entry '{path}' exceeds the entry size limit of {_limits.MaxEntryBytes} bytes.");
            }

            totalBytes = checked(totalBytes + entry.Length);
            if (totalBytes > _limits.MaxTotalUncompressedBytes)
            {
                throw new LoomRuntimeIntegrityException($"NuGet package '{packageId}' exceeds the total uncompressed size limit of {_limits.MaxTotalUncompressedBytes} bytes.");
            }
        }
    }

    private FrameworkPackageMetadata ValidateAndReadNuspec(byte[] bytes, string expectedId, string expectedVersion)
    {
        using var archive = OpenArchive(bytes, expectedId);
        ValidateArchiveEntries(archive, expectedId);
        var nuspecs = archive.Entries.Where(entry => !entry.FullName.Contains('/', StringComparison.Ordinal) && entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (nuspecs.Length != 1)
        {
            throw new LoomRuntimeIntegrityException($"NuGet package '{expectedId}' must contain exactly one root nuspec.");
        }
        XDocument document;
        try
        {
            using var stream = nuspecs[0].Open();
            document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }
        catch (Exception exception) when (exception is XmlException or InvalidOperationException)
        {
            throw new LoomRuntimeIntegrityException($"NuGet package '{expectedId}' contains an invalid nuspec.", exception);
        }
        var metadata = document.Descendants().FirstOrDefault(element => string.Equals(element.Name.LocalName, "metadata", StringComparison.OrdinalIgnoreCase));
        var actualId = metadata?.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, "id", StringComparison.OrdinalIgnoreCase))?.Value;
        var actualVersion = metadata?.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, "version", StringComparison.OrdinalIgnoreCase))?.Value;
        if (!string.Equals(actualId, expectedId, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(actualVersion)
            || !string.Equals(LoomRuntimeCatalog.NormalizeVersion(actualVersion), LoomRuntimeCatalog.NormalizeVersion(expectedVersion), StringComparison.Ordinal))
        {
            throw new LoomRuntimeIntegrityException($"NuGet package identity does not match '{expectedId}/{expectedVersion}'.");
        }
        var dependenciesElement = metadata?.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, "dependencies", StringComparison.OrdinalIgnoreCase));
        var dependencies = ParseXmlDependencies(dependenciesElement);
        return new FrameworkPackageMetadata(expectedId, LoomRuntimeCatalog.NormalizeVersion(expectedVersion), dependencies);
    }
    private static IReadOnlyList<FrameworkPackageDependency> ParseXmlDependencies(XElement? dependenciesElement)
    {
        if (dependenciesElement is null)
        {
            return [];
        }

        var groups = dependenciesElement.Elements().Where(element => string.Equals(element.Name.LocalName, "group", StringComparison.OrdinalIgnoreCase)).ToArray();
        var dependencyElements = groups.Length == 0
            ? dependenciesElement.Elements().Where(element => string.Equals(element.Name.LocalName, "dependency", StringComparison.OrdinalIgnoreCase))
            : groups.Where(group => IsCompatibleFramework(group.Attribute("targetFramework")?.Value))
                .OrderByDescending(group => GetDependencyFrameworkRank(group.Attribute("targetFramework")?.Value))
                .Take(1)
                .SelectMany(group => group.Elements().Where(element => string.Equals(element.Name.LocalName, "dependency", StringComparison.OrdinalIgnoreCase)));
        return dependencyElements.Select(element => new FrameworkPackageDependency(
            element.Attribute("id")?.Value ?? throw new LoomRuntimeAcquisitionException("NuGet dependency is missing its id."),
            ParseExactDependencyVersion(element.Attribute("version")?.Value))).ToArray();
    }
    private async Task<FrameworkPackageMetadata> ReadRegistrationMetadataAsync(string json, FrameworkPackageCoordinate coordinate, CancellationToken cancellationToken)

    {

        try

        {

            using var document = JsonDocument.Parse(json);

            var root = document.RootElement;

            var catalogEntry = root.TryGetProperty("catalogEntry", out var entry) ? entry : root;

            if (catalogEntry.ValueKind == JsonValueKind.String)
            {
                if (string.IsNullOrWhiteSpace(catalogEntry.GetString()))
                {
                    throw new LoomRuntimeAcquisitionException($"NuGet metadata for '{coordinate.Id}/{coordinate.Version}' has an empty catalog entry URL.");
                }

                using var response = await _httpClient.GetAsync(LoomRuntimeCatalog.GetNuGetRegistrationIndexUrl(coordinate.Id), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new LoomRuntimeAcquisitionException($"NuGet registration index for '{coordinate.Id}/{coordinate.Version}' returned {(int)response.StatusCode}.");
                }

                var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                var registrationJson = DecodeRegistrationJson(responseBytes, response.Content.Headers.ContentEncoding);
                using var registrationDocument = JsonDocument.Parse(registrationJson);
                if (!registrationDocument.RootElement.TryGetProperty("items", out var registrationItems) || registrationItems.ValueKind != JsonValueKind.Array)
                {
                    throw new LoomRuntimeAcquisitionException($"NuGet registration index for '{coordinate.Id}/{coordinate.Version}' has no items array.");
                }

                var matchingEntry = await FindCatalogEntryAsync(registrationItems, coordinate.Version, new HashSet<string>(StringComparer.Ordinal), cancellationToken).ConfigureAwait(false);
                if (matchingEntry is null)
                {
                    throw new LoomRuntimeAcquisitionException($"NuGet registration index does not contain exact version '{coordinate.Id}/{coordinate.Version}'.");
                }

                catalogEntry = matchingEntry.Value;
            }
            var actualId = GetString(catalogEntry, "id");

            var actualVersion = LoomRuntimeCatalog.NormalizeVersion(GetString(catalogEntry, "version"));

            if (!string.Equals(actualId, coordinate.Id, StringComparison.OrdinalIgnoreCase) || !string.Equals(actualVersion, coordinate.Version, StringComparison.Ordinal))

            {

                throw new LoomRuntimeAcquisitionException($"NuGet metadata identity does not match '{coordinate.Id}/{coordinate.Version}'.");

            }



            var dependencies = new List<FrameworkPackageDependency>();
            if (catalogEntry.TryGetProperty("dependencyGroups", out var groups) && groups.ValueKind == JsonValueKind.Array)
            {
                var group = groups.EnumerateArray()
                    .Where(group => IsCompatibleFramework(GetNullableString(group, "targetFramework")))
                    .OrderByDescending(group => GetDependencyFrameworkRank(GetNullableString(group, "targetFramework")))
                    .FirstOrDefault();
                if (group.ValueKind == JsonValueKind.Object
                    && group.TryGetProperty("dependencies", out var groupDependencies)
                    && groupDependencies.ValueKind == JsonValueKind.Array)
                {
                    foreach (var dependency in groupDependencies.EnumerateArray())
                    {
                        dependencies.Add(new FrameworkPackageDependency(
                            GetString(dependency, "id"),
                            ParseExactDependencyVersion(GetNullableString(dependency, "range"))));
                    }
                }
            }

            return new FrameworkPackageMetadata(coordinate.Id, coordinate.Version, dependencies.Distinct().ToArray());

        }

        catch (JsonException exception)

        {

            throw new LoomRuntimeAcquisitionException($"NuGet metadata for '{coordinate.Id}/{coordinate.Version}' is not valid JSON.", exception);

        }

    }

    private async Task<JsonElement?> FindCatalogEntryAsync(JsonElement items, string expectedVersion, ISet<string> visitedUrls, CancellationToken cancellationToken)
    {
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("catalogEntry", out var catalogEntry)
                && catalogEntry.ValueKind == JsonValueKind.Object
                && string.Equals(LoomRuntimeCatalog.NormalizeVersion(GetString(catalogEntry, "version")), expectedVersion, StringComparison.Ordinal))
            {
                return catalogEntry.Clone();
            }

            if (item.TryGetProperty("items", out var nestedItems) && nestedItems.ValueKind == JsonValueKind.Array)
            {
                var nestedMatch = await FindCatalogEntryAsync(nestedItems, expectedVersion, visitedUrls, cancellationToken).ConfigureAwait(false);
                if (nestedMatch is not null)
                {
                    return nestedMatch;
                }
            }

            if (item.TryGetProperty("@id", out var pageId)
                && pageId.ValueKind == JsonValueKind.String
                && pageId.GetString() is { Length: > 0 } pageUrl
                && pageUrl.Contains("/page/", StringComparison.OrdinalIgnoreCase)
                && visitedUrls.Add(pageUrl))
            {
                using var response = await _httpClient.GetAsync(pageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new LoomRuntimeAcquisitionException($"NuGet registration page '{pageUrl}' returned {(int)response.StatusCode}.");
                }

                var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                var pageJson = DecodeRegistrationJson(responseBytes, response.Content.Headers.ContentEncoding);
                using var pageDocument = JsonDocument.Parse(pageJson);
                if (!pageDocument.RootElement.TryGetProperty("items", out var pageItems) || pageItems.ValueKind != JsonValueKind.Array)
                {
                    throw new LoomRuntimeAcquisitionException($"NuGet registration page '{pageUrl}' has no items array.");
                }

                var pageMatch = await FindCatalogEntryAsync(pageItems, expectedVersion, visitedUrls, cancellationToken).ConfigureAwait(false);
                if (pageMatch is not null)
                {
                    return pageMatch;
                }
            }
        }

        return null;
    }

    private static int GetDependencyFrameworkRank(string? targetFramework)
    {
        if (string.IsNullOrWhiteSpace(targetFramework)) return 100;
        if (targetFramework.Contains("net9.0", StringComparison.OrdinalIgnoreCase) || targetFramework.Contains(".NETCoreApp,Version=v9.0", StringComparison.OrdinalIgnoreCase)) return 500;
        if (targetFramework.Contains("net8.0", StringComparison.OrdinalIgnoreCase)) return 400;
        if (targetFramework.Contains("net7.0", StringComparison.OrdinalIgnoreCase)) return 300;
        if (targetFramework.Contains("net6.0", StringComparison.OrdinalIgnoreCase)) return 250;
        if (targetFramework.Contains("netstandard2.0", StringComparison.OrdinalIgnoreCase)) return 200;
        return 0;
    }
    private static bool IsCompatibleFramework(string? targetFramework)
        => string.IsNullOrWhiteSpace(targetFramework)
            || targetFramework.Contains("net9.0", StringComparison.OrdinalIgnoreCase)
            || targetFramework.Contains(".NETCoreApp,Version=v9.0", StringComparison.OrdinalIgnoreCase)
            || targetFramework.Contains("net8.0", StringComparison.OrdinalIgnoreCase)
            || targetFramework.Contains("netstandard2.0", StringComparison.OrdinalIgnoreCase);
    private static string ParseExactDependencyVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new LoomRuntimeAcquisitionException("Framework dependency metadata must declare an exact version range.");
        }
        var trimmed = value.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
        {
            var range = trimmed[1..^1].Split(',', StringSplitOptions.TrimEntries);
            if (range.Length == 1)
            {
                trimmed = range[0];
            }
            else if (range.Length == 2 && string.Equals(LoomRuntimeCatalog.NormalizeVersion(range[0]), LoomRuntimeCatalog.NormalizeVersion(range[1]), StringComparison.Ordinal))
            {
                trimmed = range[0];
            }
        }
        try
        {
            return LoomRuntimeCatalog.NormalizeVersion(trimmed);
        }
        catch (FormatException exception)
        {
            throw new LoomRuntimeAcquisitionException($"Framework dependency version '{value}' is not an exact supported version.", exception);
        }
    }
    private static string DecodeRegistrationJson(byte[] bytes, IEnumerable<string> contentEncoding)
    {
        if (contentEncoding.Contains("gzip", StringComparer.OrdinalIgnoreCase) || (bytes.Length >= 2 && bytes[0] == 0x1f && bytes[1] == 0x8b))
        {
            using var compressed = new MemoryStream(bytes, writable: false);
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static string? FindLocalPackagePath(string packageId, string version, string? requestedRoot)
    {
        var root = requestedRoot;
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        }
        if (string.IsNullOrWhiteSpace(root))
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(profile))
            {
                return null;
            }
            root = Path.Combine(profile, ".nuget", "packages");
        }
        var directory = Path.Combine(root, packageId.ToLowerInvariant(), LoomRuntimeCatalog.NormalizeVersion(version).ToLowerInvariant());
        var candidates = new[]
        {
            Path.Combine(directory, packageId.ToLowerInvariant() + "." + LoomRuntimeCatalog.NormalizeVersion(version).ToLowerInvariant() + ".nupkg"),
            Path.Combine(directory, packageId + "." + LoomRuntimeCatalog.NormalizeVersion(version) + ".nupkg"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
    private async Task<byte[]?> TryDownloadBytesAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > _limits.MaxArchiveBytes)
            {
                throw new LoomRuntimeIntegrityException($"Framework package download '{url}' exceeds the archive size limit of {_limits.MaxArchiveBytes} bytes.");
            }
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            long totalBytes = 0;
            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                totalBytes = checked(totalBytes + read);
                if (totalBytes > _limits.MaxArchiveBytes)
                {
                    throw new LoomRuntimeIntegrityException($"Framework package download '{url}' exceeds the archive size limit of {_limits.MaxArchiveBytes} bytes.");
                }
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            return output.ToArray();
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
    private async Task<string?> TryDownloadTextAsync(string url, CancellationToken cancellationToken)
    {
        var bytes = await TryDownloadBytesAsync(url, cancellationToken).ConfigureAwait(false);
        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }
    private static ZipArchive OpenArchive(byte[] bytes, string packageName)
    {
        try
        {
            return new ZipArchive(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read, leaveOpen: false);
        }
        catch (InvalidDataException exception)
        {
            throw new LoomRuntimeIntegrityException($"NuGet package '{packageName}' is not a valid ZIP archive.", exception);
        }
    }
    private static byte[] ReadEntryBytes(byte[] bytes, string entryPath)
    {
        using var archive = OpenArchive(bytes, entryPath);
        var entry = archive.GetEntry(entryPath) ?? throw new LoomRuntimeIntegrityException($"NuGet package entry '{entryPath}' was not found.");
        using var input = entry.Open();
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }
    private static bool IsLibraryAssembly(string path)
    {
        var segments = path.Split('/', StringSplitOptions.None);
        return segments.Length >= 3
            && string.Equals(segments[0], "lib", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
    }
    private static int GetTargetFrameworkRank(string targetFramework)
    {
        if (string.Equals(targetFramework, "net9.0", StringComparison.OrdinalIgnoreCase)) return 500;
        if (string.Equals(targetFramework, "net8.0", StringComparison.OrdinalIgnoreCase)) return 400;
        if (string.Equals(targetFramework, "net7.0", StringComparison.OrdinalIgnoreCase)) return 300;
        if (string.Equals(targetFramework, "net6.0", StringComparison.OrdinalIgnoreCase)) return 250;
        if (string.Equals(targetFramework, "netstandard2.0", StringComparison.OrdinalIgnoreCase)) return 200;
        if (targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase)) return 100;
        return 0;
    }
    private static string NormalizeEntryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\0') || path.Contains('\\'))
        {
            throw new LoomRuntimeIntegrityException($"NuGet package contains an invalid ZIP path '{path}'.");
        }
        var normalized = path.EndsWith("/", StringComparison.Ordinal) ? path[..^1] : path;
        if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Contains("//", StringComparison.Ordinal) || normalized.Contains(':', StringComparison.Ordinal))
        {
            throw new LoomRuntimeIntegrityException($"NuGet package contains a non-canonical ZIP path '{path}'.");
        }
        if (normalized.Split('/').Any(segment => string.IsNullOrEmpty(segment) || segment is "." or ".."))
        {
            throw new LoomRuntimeIntegrityException($"NuGet package contains a traversal ZIP path '{path}'.");
        }
        return normalized;
    }
    private static string ResolvePathWithinRoot(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new LoomRuntimeIntegrityException($"NuGet package path '{relativePath}' escapes the bundle root.");
        }
        return fullPath;
    }
    private static string GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new KeyNotFoundException(propertyName);
        }
        return property.GetString()!;
    }
    private static string? GetNullableString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static string GetProductName(LoomRuntimeProduct product)
        => product switch
        {
            LoomRuntimeProduct.AgentOrchestrator => "ao",
            LoomRuntimeProduct.SkillOrchestrator => "so",
            _ => throw new ArgumentOutOfRangeException(nameof(product), product, "Unsupported Loom runtime product."),
        };
    private static async Task<IAsyncDisposable> AcquireCacheLockAsync(string lockPath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Framework cache lock timeout must be positive.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            try
            {
                return new FrameworkCacheLock(await Task.Run(() => new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, useAsync: true), cancellationToken).ConfigureAwait(false));
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                throw new LoomRuntimeAcquisitionException($"Timed out acquiring framework runtime cache lock '{lockPath}' after {timeout}.", exception);
            }
        }
    }
    private sealed record FrameworkPackageCoordinate(string Id, string Version);
    private sealed record FrameworkPackageDependency(string Id, string Version);
    private sealed record FrameworkPackageMetadata(string Id, string Version, IReadOnlyList<FrameworkPackageDependency> Dependencies);
    private sealed record FrameworkPackageArtifact(FrameworkPackageMetadata Metadata, byte[] Bytes, string PackageHash, string PackageUrl, string HashUrl);
    private sealed record FrameworkPackageSource(string PackageUrl, string HashUrl);
    private sealed record FrameworkAsset(string PackageId, string EntryPath, string FileName, int Rank);
    private sealed class FrameworkCacheLock : IAsyncDisposable
    {
        private readonly FileStream _stream;
        public FrameworkCacheLock(FileStream stream)
        {
            _stream = stream;
        }
        public ValueTask DisposeAsync()
        {
            _stream.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
internal sealed record LoomFrameworkRuntimePackageBundle(
    string BundleDirectory,
    string LaunchFile,
    string DepsFile,
    string RuntimeConfigFile,
    IReadOnlyList<string> PackageIds,
    IReadOnlyDictionary<string, string> PackageHashes,
    IReadOnlyDictionary<string, string> PackageUrls,
    IReadOnlyDictionary<string, string> PackageHashUrls);