using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Techne.Loom.Common.Runtime;

public sealed class LoomRuntimeResolver
{
    private static readonly Regex NetCoreApp9Pattern = new(@"^Microsoft\.NETCore\.App\s+9\.", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private readonly HttpClient _httpClient;
    private readonly ILoomRuntimeProcessRunner _processRunner;
    private readonly LoomRuntimePackageLimits _packageLimits;

    public LoomRuntimeResolver(
        HttpClient? httpClient = null,
        ILoomRuntimeProcessRunner? processRunner = null,
        LoomRuntimePackageLimits? packageLimits = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _processRunner = processRunner ?? new DefaultLoomRuntimeProcessRunner();
        _packageLimits = packageLimits ?? new LoomRuntimePackageLimits();
    }

    public async Task<LoomLaunchDescriptor> ResolveAsync(
        LoomRuntimeResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var version = LoomRuntimeCatalog.NormalizeVersion(request.Version);
        var channel = ValidateChannel(request.Channel);
        var runtimeIdentifier = request.RuntimeIdentifier ?? LoomRuntimeCatalog.DetectCurrentRuntimeIdentifier();
        LoomRuntimeCatalog.EnsureSupportedRuntimeIdentifier(runtimeIdentifier);

        var explicitFramework = !string.IsNullOrWhiteSpace(request.FrameworkBundleDirectory);
        if (request.ForceSelfContained && explicitFramework)
        {
            throw new ArgumentException("A resolution request cannot select both an explicit framework bundle directory and forced self-contained mode.", nameof(request));
        }

        return explicitFramework
            ? await ResolveFrameworkStrictAsync(request, version, channel, runtimeIdentifier, cancellationToken).ConfigureAwait(false)
            : await ResolveSelfContainedAsync(request, version, runtimeIdentifier, channel, cancellationToken).ConfigureAwait(false);
    }

    private async Task<LoomLaunchDescriptor> ResolveFrameworkStrictAsync(
        LoomRuntimeResolutionRequest request,
        string version,
        string channel,
        string runtimeIdentifier,
        CancellationToken cancellationToken)
    {
        var productName = LoomRuntimeCatalog.GetProductPackageId(request.Product);
        LoomProcessResult hostProbe;
        try
        {
            hostProbe = await _processRunner.RunAsync(
                "dotnet",
                ["--list-runtimes"],
                workingDirectory: null,
                request.GuideTimeout,
                environmentVariables: null,
                cancellationToken).ConfigureAwait(false);
        }
        catch (LoomRuntimeHostStartupException exception)
        {
            throw new LoomRuntimeHostStartupException($".NET CLI runtime for '{productName}' could not verify the .NET host: {exception.Message}", exception);
        }

        if (!hostProbe.Started || hostProbe.ExitCode != 0 || !HasNetCoreApp9(hostProbe.StandardOutput))
        {
            throw new LoomRuntimeHostStartupException(
                $".NET CLI runtime for '{productName}' requires a usable Microsoft.NETCore.App 9.x host. The explicit .NET CLI mode fails closed and never falls back to self-contained mode.");
        }

        var bundleDirectory = Path.GetFullPath(request.FrameworkBundleDirectory!);
        var entryPoint = LoomRuntimeCatalog.GetEntryPoint(request.Product);
        var launchFile = Path.Combine(bundleDirectory, entryPoint + ".dll");
        var depsFile = Path.Combine(bundleDirectory, entryPoint + ".deps.json");
        var runtimeConfigFile = Path.Combine(bundleDirectory, entryPoint + ".runtimeconfig.json");
        var missingFiles = new List<string>();
        if (!File.Exists(launchFile))
        {
            missingFiles.Add(launchFile);
        }

        if (!File.Exists(depsFile))
        {
            missingFiles.Add(depsFile);
        }

        if (!File.Exists(runtimeConfigFile))
        {
            missingFiles.Add(runtimeConfigFile);
        }

        if (missingFiles.Count > 0)
        {
            throw new LoomRuntimeIntegrityException(
                $"Legacy runtime bundle '{bundleDirectory}' is incomplete; required startup files are missing: {string.Join(", ", missingFiles)}.");
        }

        ValidateFrameworkBundle(bundleDirectory, request.Product, version, entryPoint, depsFile, runtimeConfigFile);

        var prefixArguments = new List<string>
        {
            "exec",
            "--depsfile",
            depsFile,
            "--runtimeconfig",
            runtimeConfigFile,
        };

        var guide = await RunGuideAsync(
            "dotnet",
            [.. prefixArguments, launchFile],
            bundleDirectory,
            version,
            request.GuideTimeout,
            environmentVariables: null,
            cancellationToken,
            expectedGuideRelativePath: GetExpectedGuideRelativePath(request.Product)).ConfigureAwait(false);
        var cacheRoot = ResolveCacheRoot(request.CacheRoot);
        return new LoomLaunchDescriptor(
            LoomRuntimeMode.FrameworkDependent,
            request.Product,
            version,
            channel,
            runtimeIdentifier,
            LoomRuntimeCatalog.GetProductPackageId(request.Product),
            [LoomRuntimeCatalog.GetProductPackageId(request.Product), "Techne.Loom.Common", "Techne.Loom.Abstractions"],
            null,
            null,
            cacheRoot,
            bundleDirectory,
            launchFile,
            prefixArguments,
            "framework-dependent-net9-host",
            guide.GuidePath,
            guide.DocsRoot,
            guide.GuideHash,
            null,
            LoomPreparationDiagnostics.CreatePreparationId(
                LoomRuntimeMode.FrameworkDependent,
                request.Product,
                version,
                runtimeIdentifier,
                bundleDirectory,
                null));
    }

    private async Task<LoomLaunchDescriptor> ResolveSelfContainedAsync(
        LoomRuntimeResolutionRequest request,
        string version,
        string runtimeIdentifier,
        string channel,
        CancellationToken cancellationToken)
    {
        var packageId = LoomRuntimeCatalog.GetPackageId(request.Product, runtimeIdentifier);
        var cacheRoot = ResolveCacheRoot(request.CacheRoot);
        var cacheEntry = Path.Combine(
            cacheRoot,
            GetProductCacheName(request.Product),
            version,
            runtimeIdentifier);
        var lockFile = Path.Combine(
            cacheRoot,
            ".locks",
            $"{GetProductCacheName(request.Product)}.{version}.{runtimeIdentifier}.lock");

        await using var cacheLock = await AcquireCacheLockAsync(lockFile, request.LockTimeout, cancellationToken).ConfigureAwait(false);
        var cachedPackage = await TryReadCachedPackageAsync(cacheEntry, request, version, runtimeIdentifier, cancellationToken).ConfigureAwait(false);
        if (cachedPackage is not null)
        {
            try
            {
                var extractionBaseDirectory = GetExtractionBaseDirectory(cacheRoot, request.Product, version, runtimeIdentifier, cachedPackage.PackageHash);
                var guide = await RunSelfContainedGuideAsync(
                    cachedPackage.LaunchFile,
                    request.Product,
                    version,
                    request.GuideTimeout,
                    LoomPreparationDiagnostics.CreateSelfContainedLaunchEnvironment(extractionBaseDirectory),
                    cancellationToken).ConfigureAwait(false);
                return CreateSelfContainedDescriptor(request, version, channel, runtimeIdentifier, cacheRoot, cachedPackage.PackageUrl, cachedPackage.PackageHash, cachedPackage.PackageHashUrl, cachedPackage.LaunchFile, guide);
            }
            catch (LoomRuntimeHostStartupException)
            {
                DeleteCacheEntry(cacheEntry);
            }
            catch (LoomRuntimeGuideValidationException)
            {
                DeleteCacheEntry(cacheEntry);
            }
        }

        var localPackage = await TryReadNuGetCachedPackageAsync(
            request,
            version,
            runtimeIdentifier,
            cancellationToken).ConfigureAwait(false);
        var downloadedPackage = localPackage
            ?? await DownloadPackageAsync(request, version, runtimeIdentifier, channel, cancellationToken).ConfigureAwait(false);
        var publishedPackage = await PublishCacheEntryAsync(cacheEntry, cacheRoot, downloadedPackage, request, version, runtimeIdentifier, cancellationToken).ConfigureAwait(false);
        try
        {
            var extractionBaseDirectory = GetExtractionBaseDirectory(cacheRoot, request.Product, version, runtimeIdentifier, publishedPackage.PackageHash);
            var guide = await RunSelfContainedGuideAsync(
                publishedPackage.LaunchFile,
                request.Product,
                version,
                request.GuideTimeout,
                LoomPreparationDiagnostics.CreateSelfContainedLaunchEnvironment(extractionBaseDirectory),
                cancellationToken).ConfigureAwait(false);
            return CreateSelfContainedDescriptor(request, version, channel, runtimeIdentifier, cacheRoot, publishedPackage.PackageUrl, publishedPackage.PackageHash, publishedPackage.PackageHashUrl, publishedPackage.LaunchFile, guide);
        }
        catch (LoomRuntimeGuideValidationException exception)
        {
            DeleteCacheEntry(cacheEntry);
            throw new LoomRuntimeAcquisitionException($"Self-contained runtime package '{packageId}' did not pass the fresh --guide validation gate.", exception);
        }
        catch (LoomRuntimeHostStartupException exception)
        {
            DeleteCacheEntry(cacheEntry);
            throw new LoomRuntimeAcquisitionException($"Self-contained runtime package '{packageId}' was acquired but could not pass the fresh --guide startup gate.", exception);
        }
    }

    private async Task<CachedPackage?> TryReadCachedPackageAsync(
        string cacheEntry,
        LoomRuntimeResolutionRequest request,
        string version,
        string runtimeIdentifier,
        CancellationToken cancellationToken)
    {
        var packageId = LoomRuntimeCatalog.GetPackageId(request.Product, runtimeIdentifier);
        var packagePath = Path.Combine(cacheEntry, "package.nupkg");
        var hashPath = packagePath + ".sha512";
        if (!File.Exists(packagePath) || !File.Exists(hashPath))
        {
            return null;
        }

        try
        {
            if (new FileInfo(packagePath).Length > _packageLimits.MaxArchiveBytes)
            {
                return null;
            }

            var packageBytes = await File.ReadAllBytesAsync(packagePath, cancellationToken).ConfigureAwait(false);
            var hashText = await File.ReadAllTextAsync(hashPath, cancellationToken).ConfigureAwait(false);
            var packageHash = LoomRuntimePackageValidator.NormalizeAndValidateSha512(packageBytes, hashText);
            var validation = LoomRuntimePackageValidator.Validate(packageBytes, request.Product, version, runtimeIdentifier, _packageLimits);
            var launchFile = Path.Combine(cacheEntry, validation.EntryPointName);
            if (!File.Exists(launchFile) || new FileInfo(launchFile).Length == 0)
            {
                return null;
            }

            var expectedEntryBytes = LoomRuntimePackageValidator.ReadEntryBytes(packageBytes, $"tools/{runtimeIdentifier}/{validation.EntryPointName}");
            var cachedEntryBytes = await File.ReadAllBytesAsync(launchFile, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(SHA512.HashData(expectedEntryBytes), SHA512.HashData(cachedEntryBytes)))
            {
                return null;
            }

            var manifestPath = Path.Combine(cacheEntry, "runtime.json");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            var expectedManifestBytes = LoomRuntimePackageValidator.ReadEntryBytes(packageBytes, validation.ManifestPath);
            var cachedManifestBytes = await File.ReadAllBytesAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(SHA512.HashData(expectedManifestBytes), SHA512.HashData(cachedManifestBytes)))
            {
                return null;
            }

            LoomRuntimePackageValidator.ValidateExtractedDocumentation(
                packageBytes,
                runtimeIdentifier,
                Path.Combine(cacheEntry, "docs", "en"));

            var packageUrlPath = Path.Combine(cacheEntry, "package.url");
            if (!File.Exists(packageUrlPath))
            {
                return null;
            }

            var packageUrl = await File.ReadAllTextAsync(packageUrlPath, cancellationToken).ConfigureAwait(false);
            var packageHashUrlPath = Path.Combine(cacheEntry, "package.hash.url");
            if (!File.Exists(packageHashUrlPath))
            {
                return null;
            }

            var packageHashUrl = await File.ReadAllTextAsync(packageHashUrlPath, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(packageUrl) || string.IsNullOrWhiteSpace(packageHashUrl))
            {
                return null;
            }
            if (!IsExpectedPackageUrl(packageUrl, packageId, version, request.Channel) ||
                !IsExpectedPackageHashUrl(packageHashUrl, packageId, version, request.Channel))
            {
                return null;
            }
            return new CachedPackage(launchFile, packageUrl, packageHash, packageHashUrl);
        }
        catch (LoomRuntimeIntegrityException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task<DownloadedPackage?> TryReadNuGetCachedPackageAsync(
        LoomRuntimeResolutionRequest request,
        string version,
        string runtimeIdentifier,
        CancellationToken cancellationToken)
    {
        var packageId = LoomRuntimeCatalog.GetPackageId(request.Product, runtimeIdentifier);
        var packageCacheRoot = request.NuGetPackageCacheRoot
            ?? Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (string.IsNullOrWhiteSpace(packageCacheRoot))
        {
            packageCacheRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget",
                "packages");
        }
        var packageDirectory = Path.Combine(packageCacheRoot, packageId.ToLowerInvariant(), version.ToLowerInvariant());
        var packagePath = new[]
        {
            Path.Combine(packageDirectory, $"{packageId}.{version}.nupkg"),
            Path.Combine(packageDirectory, $"{packageId.ToLowerInvariant()}.{version.ToLowerInvariant()}.nupkg"),
        }.FirstOrDefault(File.Exists);
        if (packagePath is null)
        {
            return null;
        }

        try
        {
            var packageBytes = await File.ReadAllBytesAsync(packagePath, cancellationToken).ConfigureAwait(false);
            var validation = LoomRuntimePackageValidator.Validate(packageBytes, request.Product, version, runtimeIdentifier, _packageLimits);
            var packageHash = Convert.ToBase64String(SHA512.HashData(packageBytes));
            return new DownloadedPackage(
                packageBytes,
                packageHash,
                LoomRuntimeCatalog.GetNuGetPackageUrl(packageId, version),
                LoomRuntimeCatalog.GetNuGetHashUrl(packageId, version),
                validation);
        }
        catch (LoomRuntimeIntegrityException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task<DownloadedPackage> DownloadPackageAsync(
        LoomRuntimeResolutionRequest request,
        string version,
        string runtimeIdentifier,
        string channel,
        CancellationToken cancellationToken)
    {
        var packageId = LoomRuntimeCatalog.GetPackageId(request.Product, runtimeIdentifier);
        var failures = new List<string>();
        var sources = new[]
        {
            new PackageSource(
                "NuGet.org",
                LoomRuntimeCatalog.GetNuGetPackageUrl(packageId, version),
                LoomRuntimeCatalog.GetNuGetHashUrl(packageId, version)),
            new PackageSource(
                "GitHub exact",
                LoomRuntimeCatalog.GetGitHubPackageUrl(packageId, version, channel),
                LoomRuntimeCatalog.GetGitHubPackageUrl(packageId, version, channel) + ".sha512"),
        };

        foreach (var source in sources)
        {
            var packageBytes = await TryDownloadBytesAsync(source.PackageUrl, cancellationToken).ConfigureAwait(false);
            if (packageBytes is null)
            {
                failures.Add($"{source.Name}: package unavailable");
                continue;
            }

            var hashText = await TryDownloadTextAsync(source.HashUrl, cancellationToken).ConfigureAwait(false);
            if (hashText is null)
            {
                failures.Add($"{source.Name}: SHA-512 sidecar unavailable");
                continue;
            }

            var packageHash = LoomRuntimePackageValidator.NormalizeAndValidateSha512(packageBytes, hashText);
            var validation = LoomRuntimePackageValidator.Validate(packageBytes, request.Product, version, runtimeIdentifier, _packageLimits);
            return new DownloadedPackage(packageBytes, packageHash, source.PackageUrl, source.HashUrl, validation);
        }

        throw new LoomRuntimeAcquisitionException($"Unable to acquire exact self-contained runtime package '{packageId}' version '{version}' for '{runtimeIdentifier}'. {string.Join("; ", failures)}");
    }

    private async Task<PublishedPackage> PublishCacheEntryAsync(
        string cacheEntry,
        string cacheRoot,
        DownloadedPackage package,
        LoomRuntimeResolutionRequest request,
        string version,
        string runtimeIdentifier,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(cacheRoot);
        var temporaryEntry = cacheEntry + $".tmp-{Guid.NewGuid():N}";
        Directory.CreateDirectory(temporaryEntry);
        try
        {
            var packagePath = Path.Combine(temporaryEntry, "package.nupkg");
            await File.WriteAllBytesAsync(packagePath, package.PackageBytes, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(packagePath + ".sha512", package.PackageHash, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(temporaryEntry, "package.url"), package.PackageUrl, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(temporaryEntry, "package.hash.url"), package.PackageHashUrl, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);

            var entryBytes = LoomRuntimePackageValidator.ReadEntryBytes(package.PackageBytes, $"tools/{runtimeIdentifier}/{package.Validation.EntryPointName}");
            await File.WriteAllBytesAsync(Path.Combine(temporaryEntry, package.Validation.EntryPointName), entryBytes, cancellationToken).ConfigureAwait(false);
            try
            {
                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    File.SetUnixFileMode(Path.Combine(temporaryEntry, package.Validation.EntryPointName), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
            }
            catch (PlatformNotSupportedException)
            {
            }

            var manifestBytes = LoomRuntimePackageValidator.ReadEntryBytes(package.PackageBytes, package.Validation.ManifestPath);
            await File.WriteAllBytesAsync(Path.Combine(temporaryEntry, "runtime.json"), manifestBytes, cancellationToken).ConfigureAwait(false);
            LoomRuntimePackageValidator.ExtractDocumentation(
                package.PackageBytes,
                runtimeIdentifier,
                Path.Combine(temporaryEntry, "docs", "en"));

            var displacedEntry = cacheEntry + $".stale-{Guid.NewGuid():N}";
            if (Directory.Exists(cacheEntry))
            {
                Directory.Move(cacheEntry, displacedEntry);
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cacheEntry)!);
                Directory.Move(temporaryEntry, cacheEntry);
            }
            catch
            {
                if (Directory.Exists(displacedEntry) && !Directory.Exists(cacheEntry))
                {
                    Directory.Move(displacedEntry, cacheEntry);
                }

                throw;
            }
            finally
            {
                if (Directory.Exists(displacedEntry))
                {
                    DeleteCacheEntry(displacedEntry);
                }
            }

            return new PublishedPackage(Path.Combine(cacheEntry, package.Validation.EntryPointName), package.PackageUrl, package.PackageHash, package.PackageHashUrl);
        }
        catch
        {
            DeleteCacheEntry(temporaryEntry);
            throw;
        }
    }

    private async Task<LoomGuideResult> RunSelfContainedGuideAsync(
        string launchFile,
        LoomRuntimeProduct product,
        string version,
        TimeSpan timeout,
        IDictionary<string, string>? environmentVariables,
        CancellationToken cancellationToken)
    {
        return await RunGuideAsync(
            launchFile,
            ["--guide"],
            Path.GetDirectoryName(launchFile),
            version,
            timeout,
            environmentVariables,
            cancellationToken,
            expectedGuideRelativePath: GetExpectedGuideRelativePath(product)).ConfigureAwait(false);
    }

    private async Task<LoomGuideResult> RunGuideAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        string expectedVersion,
        TimeSpan timeout,
        IDictionary<string, string>? environmentVariables,
        CancellationToken cancellationToken,
        string? expectedGuideRelativePath = null)
    {
        var process = await _processRunner.RunAsync(fileName, arguments, workingDirectory, timeout, environmentVariables, cancellationToken).ConfigureAwait(false);
        if (!process.Started)
        {
            throw new LoomRuntimeHostStartupException($"Runtime process '{fileName}' could not start.");
        }

        if (process.ExitCode != 0)
        {
            if (LooksLikeStructuredCliError(process.StandardOutput, process.StandardError))
            {
                throw new LoomRuntimeCommandException($"Runtime '--guide' failed after CLI startup with exit code {process.ExitCode}: {FirstNonEmpty(process.StandardError, process.StandardOutput)}");
            }

            throw new LoomRuntimeHostStartupException($"Runtime process '{fileName}' exited before producing a usable --guide result with exit code {process.ExitCode}: {FirstNonEmpty(process.StandardError, process.StandardOutput)}");
        }

        try
        {
            using var document = JsonDocument.Parse(process.StandardOutput);
            var root = document.RootElement;
            var version = GetGuideString(root, "version");
            var docsRoot = GetGuideString(root, "docs_root");
            var guidePath = GetGuideString(root, "guide_path");
            if (!string.Equals(LoomRuntimeCatalog.NormalizeVersion(version), expectedVersion, StringComparison.Ordinal) ||
                !Path.IsPathFullyQualified(guidePath) ||
                !Path.IsPathFullyQualified(docsRoot) ||
                !Directory.Exists(docsRoot) ||
                !File.Exists(guidePath))
            {
                throw new LoomRuntimeGuideValidationException($"Runtime '--guide' returned paths or a version that does not match exact version '{expectedVersion}'.");
            }

            if (!IsPathWithinRoot(docsRoot, guidePath))
            {
                throw new LoomRuntimeGuideValidationException("Runtime '--guide' returned a guide_path outside docs_root.");
            }

            var guideRelativePath = Path.GetRelativePath(docsRoot, guidePath).Replace(Path.DirectorySeparatorChar, '/');
            if (expectedGuideRelativePath is not null && !string.Equals(guideRelativePath, expectedGuideRelativePath, StringComparison.Ordinal))
            {
                throw new LoomRuntimeGuideValidationException($"Runtime '--guide' returned guide_path '{guideRelativePath}', expected '{expectedGuideRelativePath}'.");
            }

            var guideHash = Convert.ToBase64String(SHA512.HashData(await File.ReadAllBytesAsync(guidePath, cancellationToken).ConfigureAwait(false)));
            return new LoomGuideResult(version, docsRoot, guidePath, guideHash);
        }
        catch (LoomRuntimeCommandException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new LoomRuntimeGuideValidationException($"Runtime '--guide' returned a guide path that could not be read: {exception.Message}", exception);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or FormatException or ArgumentException)
        {
            throw new LoomRuntimeGuideValidationException($"Runtime '--guide' did not return the required JSON result: {exception.Message}", exception);
        }
    }

    private static bool IsPathWithinRoot(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetExpectedGuideRelativePath(LoomRuntimeProduct product)
        => $"guides/{LoomRuntimeCatalog.GetEntryPoint(product)}-guide.md";

    private static string GetRequiredJsonString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new KeyNotFoundException(propertyName);
        }

        return property.GetString()!;
    }

    private static LoomLaunchDescriptor CreateSelfContainedDescriptor(
        LoomRuntimeResolutionRequest request,
        string version,
        string channel,
        string runtimeIdentifier,
        string cacheRoot,
        string packageUrl,
        string packageHash,
        string packageHashUrl,
        string launchFile,
        LoomGuideResult guide)
    {
        var packageId = LoomRuntimeCatalog.GetPackageId(request.Product, runtimeIdentifier);
        var runtimeRoot = Path.GetDirectoryName(launchFile)!;
        return new LoomLaunchDescriptor(
            LoomRuntimeMode.SelfContained,
            request.Product,
            version,
            channel,
            runtimeIdentifier,
            packageId,
            [packageId],
            packageUrl,
            packageHash,
            cacheRoot,
            runtimeRoot,
            launchFile,
            [],
            "self-contained-single-file-package",
            guide.GuidePath,
            guide.DocsRoot,
            guide.GuideHash,
            GetExtractionBaseDirectory(cacheRoot, request.Product, version, runtimeIdentifier, packageHash),
            LoomPreparationDiagnostics.CreatePreparationId(
                LoomRuntimeMode.SelfContained,
                request.Product,
                version,
                runtimeIdentifier,
                runtimeRoot,
                packageHash),
            packageHashUrl);
    }

    private static string GetExtractionBaseDirectory(string cacheRoot, LoomRuntimeProduct product, string version, string runtimeIdentifier, string packageHash)
        => Path.Combine(
            cacheRoot,
            ".extraction",
            GetProductCacheName(product),
            version,
            runtimeIdentifier,
            packageHash.Substring(0, 16));

    private static void ValidateFrameworkBundle(
        string bundleDirectory,
        LoomRuntimeProduct product,
        string expectedVersion,
        string entryPoint,
        string depsFile,
        string runtimeConfigFile)
    {
        ValidateFrameworkRuntimeConfig(bundleDirectory, runtimeConfigFile);

        JsonDocument document;
        try
        {
            using var stream = File.OpenRead(depsFile);
            document = JsonDocument.Parse(stream);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new LoomRuntimeIntegrityException($"Legacy runtime bundle '{bundleDirectory}' contains an unreadable or invalid dependency manifest '{depsFile}'.", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (!root.TryGetProperty("targets", out var targets) || targets.ValueKind != JsonValueKind.Object)
            {
                throw new LoomRuntimeIntegrityException($"Legacy runtime bundle '{bundleDirectory}' dependency manifest does not contain a valid targets map.");
            }

            var targetProperties = targets.EnumerateObject().ToArray();
            if (targetProperties.Length != 1 || !string.Equals(targetProperties[0].Name, ".NETCoreApp,Version=v9.0", StringComparison.Ordinal))
            {
                throw new LoomRuntimeIntegrityException($"Legacy runtime bundle '{bundleDirectory}' dependency manifest must contain exactly one '.NETCoreApp,Version=v9.0' target.");
            }

            if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Object)
            {
                throw new LoomRuntimeIntegrityException($"Legacy runtime bundle '{bundleDirectory}' dependency manifest does not contain a valid libraries map.");
            }

            var target = targetProperties[0].Value;
            if (target.ValueKind != JsonValueKind.Object)
            {
                throw new LoomRuntimeIntegrityException($"Legacy runtime bundle '{bundleDirectory}' .NET 9 target is not an object.");
            }

            var expectedLibraryKeys = new[]
            {
                $"{entryPoint}/{expectedVersion}",
                $"Techne.Loom.Common/{expectedVersion}",
                $"Techne.Loom.Abstractions/{expectedVersion}",
                "Microsoft.CodeAnalysis.Common/4.12.0",
                "Microsoft.CodeAnalysis.CSharp/4.12.0",
            };
            var missingLibraries = expectedLibraryKeys
                .Where(key => !target.TryGetProperty(key, out _) || !libraries.TryGetProperty(key, out _))
                .ToArray();
            if (missingLibraries.Length > 0)
            {
                throw new LoomRuntimeIntegrityException(
                    $".NET CLI runtime bundle '{bundleDirectory}' does not contain the exact .NET runtime closure; missing target or library metadata: {string.Join(", ", missingLibraries)}.");
            }

            var requiredAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var library in target.EnumerateObject())
            {
                if (library.Value.ValueKind != JsonValueKind.Object || !libraries.TryGetProperty(library.Name, out var libraryMetadata) || libraryMetadata.ValueKind != JsonValueKind.Object)
                {
                    throw new LoomRuntimeIntegrityException($"Legacy runtime bundle '{bundleDirectory}' contains invalid dependency metadata for '{library.Name}'.");
                }

                AddDependencyAssets(library.Value, library.Name, "runtime", requiredAssets);
                AddDependencyAssets(library.Value, library.Name, "resources", requiredAssets);
                AddDependencyAssets(library.Value, library.Name, "native", requiredAssets);
                AddDependencyAssets(library.Value, library.Name, "runtimeTargets", requiredAssets);
            }

            var missingAssets = requiredAssets
                .Where(asset => !FrameworkAssetExists(bundleDirectory, asset))
                .OrderBy(asset => asset, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (missingAssets.Length > 0)
            {
                throw new LoomRuntimeIntegrityException(
                    $"Legacy runtime bundle '{bundleDirectory}' is missing dependency closure assets: {string.Join(", ", missingAssets)}.");
            }
        }
    }

    private static void ValidateFrameworkRuntimeConfig(string bundleDirectory, string runtimeConfigFile)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(runtimeConfigFile));
            var root = document.RootElement;
            if (!root.TryGetProperty("runtimeOptions", out var runtimeOptions) || runtimeOptions.ValueKind != JsonValueKind.Object)
            {
                throw new LoomRuntimeIntegrityException($"Legacy runtime bundle '{bundleDirectory}' runtimeconfig does not contain runtimeOptions.");
            }

            var tfm = GetRequiredJsonString(runtimeOptions, "tfm");
            if (!string.Equals(tfm, "net9.0", StringComparison.Ordinal))
            {
                throw new LoomRuntimeIntegrityException($"Legacy runtime bundle '{bundleDirectory}' runtimeconfig must target net9.0, but declares '{tfm}'.");
            }

            if (!runtimeOptions.TryGetProperty("framework", out var framework) || framework.ValueKind != JsonValueKind.Object)
            {
                throw new LoomRuntimeIntegrityException($"Legacy runtime bundle '{bundleDirectory}' runtimeconfig does not contain a single framework object.");
            }

            var frameworkName = GetRequiredJsonString(framework, "name");
            var frameworkVersion = GetRequiredJsonString(framework, "version");
            if (!string.Equals(frameworkName, "Microsoft.NETCore.App", StringComparison.Ordinal) ||
                !Version.TryParse(frameworkVersion, out var parsedVersion) || parsedVersion.Major != 9)
            {
                throw new LoomRuntimeIntegrityException($"Legacy runtime bundle '{bundleDirectory}' runtimeconfig must select Microsoft.NETCore.App 9.x.");
            }
        }
        catch (LoomRuntimeIntegrityException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or KeyNotFoundException or FormatException)
        {
            throw new LoomRuntimeIntegrityException($"Legacy runtime bundle '{bundleDirectory}' contains an invalid runtimeconfig '{runtimeConfigFile}'.", exception);
        }
    }

    private static void AddDependencyAssets(JsonElement library, string libraryName, string propertyName, ISet<string> assets)
    {
        if (!library.TryGetProperty(propertyName, out var property))
        {
            return;
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            throw new LoomRuntimeIntegrityException($"Legacy runtime dependency '{libraryName}' has a non-object '{propertyName}' asset map.");
        }

        foreach (var asset in property.EnumerateObject())
        {
            if (asset.Value.ValueKind != JsonValueKind.Object)
            {
                throw new LoomRuntimeIntegrityException($"Legacy runtime dependency '{libraryName}' has invalid metadata for asset '{asset.Name}'.");
            }

            foreach (var candidate in GetFrameworkAssetCandidates(asset.Name))
            {
                assets.Add(candidate);
            }
        }
    }

    private static IEnumerable<string> GetFrameworkAssetCandidates(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath) || assetPath.Contains('\\') || assetPath.Contains(':') || assetPath.StartsWith("/", StringComparison.Ordinal))
        {
            throw new LoomRuntimeIntegrityException($"Legacy runtime dependency contains a non-canonical or absolute asset path '{assetPath}'.");
        }

        var segments = assetPath.Split('/', StringSplitOptions.None);
        if (segments.Any(segment => string.IsNullOrEmpty(segment) || segment is "." or ".."))
        {
            throw new LoomRuntimeIntegrityException($"Legacy runtime dependency contains a traversal or non-canonical asset path '{assetPath}'.");
        }

        var candidates = new List<string>();
        if (segments.Length == 1)
        {
            candidates.Add(assetPath);
        }
        else if (string.Equals(segments[0], "lib", StringComparison.Ordinal) && segments.Length >= 3)
        {
            candidates.Add(assetPath);
            candidates.Add(string.Join('/', segments.Skip(2)));
        }
        else if (string.Equals(segments[0], "runtimes", StringComparison.Ordinal) && segments.Length >= 4)
        {
            candidates.Add(assetPath);
            if (string.Equals(segments[2], "lib", StringComparison.Ordinal) && segments.Length >= 5)
            {
                candidates.Add(string.Join('/', segments.Skip(4)));
            }
            else if (string.Equals(segments[2], "native", StringComparison.Ordinal))
            {
                candidates.Add(string.Join('/', segments.Skip(3)));
            }
            else
            {
                throw new LoomRuntimeIntegrityException($"Legacy runtime dependency contains an unsupported asset layout '{assetPath}'.");
            }
        }
        else
        {
            throw new LoomRuntimeIntegrityException($"Legacy runtime dependency contains an unsupported asset layout '{assetPath}'.");
        }

        return candidates;
    }

    private static bool FrameworkAssetExists(string bundleDirectory, string assetPath)
    {
        var bundleRoot = Path.GetFullPath(bundleDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var candidate in GetFrameworkAssetCandidates(assetPath))
        {
            var fullPath = Path.GetFullPath(Path.Combine(bundleDirectory, candidate.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(bundleRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new LoomRuntimeIntegrityException($"Legacy runtime dependency asset '{assetPath}' escapes the bundle root.");
            }

            if (File.Exists(fullPath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasNetCoreApp9(string output)
        => output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(line => NetCoreApp9Pattern.IsMatch(line.Trim()));

    private static bool LooksLikeStructuredCliError(string standardOutput, string standardError)
        => standardOutput.Contains("ao_property", StringComparison.OrdinalIgnoreCase) ||
           standardOutput.Contains("<so_property", StringComparison.OrdinalIgnoreCase) ||
           standardOutput.Contains("\"type\":\"error\"", StringComparison.OrdinalIgnoreCase) ||
           standardError.Contains("ao_property", StringComparison.OrdinalIgnoreCase) ||
           standardError.Contains("<so_property", StringComparison.OrdinalIgnoreCase);

    private static string GetGuideString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new KeyNotFoundException(propertyName);
        }

        return property.GetString()!;
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
            if (contentLength.HasValue && contentLength.Value > _packageLimits.MaxArchiveBytes)
            {
                throw new LoomRuntimeIntegrityException($"Runtime download '{url}' exceeds the archive size limit of {_packageLimits.MaxArchiveBytes} bytes.");
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
                if (totalBytes > _packageLimits.MaxArchiveBytes)
                {
                    throw new LoomRuntimeIntegrityException($"Runtime download '{url}' exceeds the archive size limit of {_packageLimits.MaxArchiveBytes} bytes.");
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

    private static string ResolveCacheRoot(string? requestedRoot)
    {
        var configuredRoot = requestedRoot ?? Environment.GetEnvironmentVariable("TECHNE_LOOM_RUNTIME_CACHE_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.GetFullPath(configuredRoot);
        }

        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.GetFullPath(string.IsNullOrWhiteSpace(localApplicationData)
            ? Path.Combine(Path.GetTempPath(), "techne-loom-runtime-cache")
            : Path.Combine(localApplicationData, "Techne", "Loom", "runtime"));
    }

    private static string GetProductCacheName(LoomRuntimeProduct product)
        => product switch
        {
            LoomRuntimeProduct.AgentOrchestrator => "ao",
            LoomRuntimeProduct.SkillOrchestrator => "so",
            _ => throw new ArgumentOutOfRangeException(nameof(product), product, "Unsupported Loom runtime product."),
        };

    private static bool IsExpectedPackageUrl(string packageUrl, string packageId, string version, string channel)
    {
        var normalizedChannel = channel.Trim().ToLowerInvariant();
        var acceptedUrls = new[]
        {
            LoomRuntimeCatalog.GetNuGetPackageUrl(packageId, version),
            LoomRuntimeCatalog.GetGitHubPackageUrl(packageId, version, normalizedChannel),
        };
        return acceptedUrls.Contains(packageUrl.Trim(), StringComparer.Ordinal);
    }

    private static bool IsExpectedPackageHashUrl(string packageHashUrl, string packageId, string version, string channel)
    {
        var normalizedChannel = channel.Trim().ToLowerInvariant();
        var acceptedUrls = new[]
        {
            LoomRuntimeCatalog.GetNuGetHashUrl(packageId, version),
            LoomRuntimeCatalog.GetGitHubPackageUrl(packageId, version, normalizedChannel) + ".sha512",
        };
        return acceptedUrls.Contains(packageHashUrl.Trim(), StringComparer.Ordinal);
    }

    private static string ValidateChannel(string channel)
    {
        if (!string.Equals(channel, "released", StringComparison.OrdinalIgnoreCase) && !string.Equals(channel, "beta", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Runtime channel must be 'released' or 'beta'.", nameof(channel));
        }

        return channel.ToLowerInvariant();
    }

    private static void DeleteCacheEntry(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string FirstNonEmpty(string first, string second)
        => string.IsNullOrWhiteSpace(first) ? second.Trim() : first.Trim();

    private static async Task<IAsyncDisposable> AcquireCacheLockAsync(string lockPath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Cache lock timeout must be positive.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            try
            {
                return new CacheLock(await Task.Run(() => new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, useAsync: true), cancellationToken).ConfigureAwait(false));
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                throw new LoomRuntimeAcquisitionException($"Timed out acquiring the runtime cache lock '{lockPath}' after {timeout}.", exception);
            }
        }
    }

    private sealed record PackageSource(string Name, string PackageUrl, string HashUrl);

    private sealed record DownloadedPackage(byte[] PackageBytes, string PackageHash, string PackageUrl, string PackageHashUrl, LoomRuntimePackageValidationResult Validation);

    private sealed record CachedPackage(string LaunchFile, string PackageUrl, string PackageHash, string PackageHashUrl);

    private sealed record PublishedPackage(string LaunchFile, string PackageUrl, string PackageHash, string PackageHashUrl);

    private sealed class CacheLock : IAsyncDisposable
    {
        private readonly FileStream _stream;

        public CacheLock(FileStream stream)
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

internal sealed class DefaultLoomRuntimeProcessRunner : ILoomRuntimeProcessRunner
{
    public async Task<LoomProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan timeout,
        IDictionary<string, string>? environmentVariables = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (environmentVariables is not null)
        {
            foreach (var environmentVariable in environmentVariables)
            {
                startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
            }
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        try
        {
            if (!process.Start())
            {
                return new LoomProcessResult(false, -1, string.Empty, string.Empty);
            }
        }
        catch (Win32Exception)
        {
            return new LoomProcessResult(false, -1, string.Empty, string.Empty);
        }
        catch (IOException)
        {
            return new LoomProcessResult(false, -1, string.Empty, string.Empty);
        }
        catch (InvalidOperationException)
        {
            return new LoomProcessResult(false, -1, string.Empty, string.Empty);
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            throw new LoomRuntimeHostStartupException($"Runtime process '{fileName}' exceeded the timeout of {timeout}.");
        }

        return new LoomProcessResult(
            true,
            process.ExitCode,
            await standardOutputTask.ConfigureAwait(false),
            await standardErrorTask.ConfigureAwait(false));
    }
}
