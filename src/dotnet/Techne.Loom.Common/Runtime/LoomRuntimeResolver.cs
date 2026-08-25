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

        if (!request.ForceSelfContained && !string.IsNullOrWhiteSpace(request.FrameworkBundleDirectory))
        {
            var frameworkDescriptor = await TryResolveFrameworkAsync(request, version, runtimeIdentifier, cancellationToken).ConfigureAwait(false);
            if (frameworkDescriptor is not null)
            {
                return frameworkDescriptor;
            }
        }

        return await ResolveSelfContainedAsync(request, version, runtimeIdentifier, channel, cancellationToken).ConfigureAwait(false);
    }

    private async Task<LoomLaunchDescriptor?> TryResolveFrameworkAsync(
        LoomRuntimeResolutionRequest request,
        string version,
        string runtimeIdentifier,
        CancellationToken cancellationToken)
    {
        LoomProcessResult hostProbe;
        try
        {
            hostProbe = await _processRunner.RunAsync(
                "dotnet",
                ["--list-runtimes"],
                workingDirectory: null,
                request.GuideTimeout,
                cancellationToken).ConfigureAwait(false);
        }
        catch (LoomRuntimeHostStartupException)
        {
            return null;
        }

        if (!hostProbe.Started || hostProbe.ExitCode != 0 || !HasNetCoreApp9(hostProbe.StandardOutput))
        {
            return null;
        }

        var bundleDirectory = Path.GetFullPath(request.FrameworkBundleDirectory!);
        var entryPoint = LoomRuntimeCatalog.GetEntryPoint(request.Product);
        var launchFile = Path.Combine(bundleDirectory, entryPoint + ".dll");
        var runtimeConfigFile = Path.Combine(bundleDirectory, entryPoint + ".runtimeconfig.json");
        if (!File.Exists(launchFile) || !File.Exists(runtimeConfigFile))
        {
            return null;
        }

        var prefixArguments = new List<string> { "exec" };
        var depsFile = Path.Combine(bundleDirectory, entryPoint + ".deps.json");
        if (File.Exists(depsFile))
        {
            prefixArguments.Add("--depsfile");
            prefixArguments.Add(depsFile);
        }

        prefixArguments.Add("--runtimeconfig");
        prefixArguments.Add(runtimeConfigFile);

        try
        {
            var guide = await RunGuideAsync(
                "dotnet",
                [.. prefixArguments, launchFile],
                bundleDirectory,
                version,
                request.GuideTimeout,
                cancellationToken).ConfigureAwait(false);
            var packagePrefix = LoomRuntimeCatalog.GetProductPackageId(request.Product);
            return new LoomLaunchDescriptor(
                LoomRuntimeMode.FrameworkDependent,
                version,
                runtimeIdentifier,
                packagePrefix,
                [packagePrefix, "Techne.Loom.Common", "Techne.Loom.Abstractions"],
                null,
                null,
                bundleDirectory,
                launchFile,
                prefixArguments,
                "framework-dependent-net9-host",
                guide.GuidePath);
        }
        catch (LoomRuntimeHostStartupException)
        {
            return null;
        }
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
                var guide = await RunSelfContainedGuideAsync(cachedPackage.LaunchFile, version, request.GuideTimeout, cancellationToken).ConfigureAwait(false);
                return CreateSelfContainedDescriptor(request, version, runtimeIdentifier, cacheRoot, cachedPackage.PackageUrl, cachedPackage.PackageHash, cachedPackage.LaunchFile, guide.GuidePath);
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

        var downloadedPackage = await DownloadPackageAsync(request, version, runtimeIdentifier, channel, cancellationToken).ConfigureAwait(false);
        var publishedPackage = await PublishCacheEntryAsync(cacheEntry, cacheRoot, downloadedPackage, request, version, runtimeIdentifier, cancellationToken).ConfigureAwait(false);
        try
        {
            var guide = await RunSelfContainedGuideAsync(publishedPackage.LaunchFile, version, request.GuideTimeout, cancellationToken).ConfigureAwait(false);
            return CreateSelfContainedDescriptor(request, version, runtimeIdentifier, cacheRoot, publishedPackage.PackageUrl, publishedPackage.PackageHash, publishedPackage.LaunchFile, guide.GuidePath);
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

            var packageUrlPath = Path.Combine(cacheEntry, "package.url");
            if (!File.Exists(packageUrlPath))
            {
                return null;
            }

            var packageUrl = await File.ReadAllTextAsync(packageUrlPath, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(packageUrl))
            {
                return null;
            }
            if (!IsExpectedPackageUrl(packageUrl, packageId, version, request.Channel))
            {
                return null;
            }
            return new CachedPackage(launchFile, packageUrl, packageHash);
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
            new PackageSource(
                "GitHub latest alias",
                LoomRuntimeCatalog.GetGitHubPackageUrl(packageId, version, channel, latestAlias: true),
                LoomRuntimeCatalog.GetGitHubPackageUrl(packageId, version, channel, latestAlias: true) + ".sha512"),
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
            return new DownloadedPackage(packageBytes, packageHash, source.PackageUrl, validation);
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

            return new PublishedPackage(Path.Combine(cacheEntry, package.Validation.EntryPointName), package.PackageUrl, package.PackageHash);
        }
        catch
        {
            DeleteCacheEntry(temporaryEntry);
            throw;
        }
    }

    private async Task<LoomGuideResult> RunSelfContainedGuideAsync(
        string launchFile,
        string version,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return await RunGuideAsync(
            launchFile,
            ["--guide"],
            Path.GetDirectoryName(launchFile),
            version,
            timeout,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<LoomGuideResult> RunGuideAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        string expectedVersion,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var process = await _processRunner.RunAsync(fileName, arguments, workingDirectory, timeout, cancellationToken).ConfigureAwait(false);
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
            if (!string.Equals(LoomRuntimeCatalog.NormalizeVersion(version), expectedVersion, StringComparison.Ordinal) || !Path.IsPathFullyQualified(guidePath) || !File.Exists(guidePath))
            {
                throw new LoomRuntimeGuideValidationException($"Runtime '--guide' returned a version or guide path that does not match exact version '{expectedVersion}'.");
            }

            if (!Path.IsPathFullyQualified(docsRoot) || !Directory.Exists(docsRoot))
            {
                throw new LoomRuntimeGuideValidationException("Runtime '--guide' returned a docs_root that is not an existing absolute directory.");
            }

            return new LoomGuideResult(version, docsRoot, guidePath);
        }
        catch (LoomRuntimeCommandException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or FormatException)
        {
            throw new LoomRuntimeGuideValidationException($"Runtime '--guide' did not return the required JSON result: {exception.Message}");
        }
    }

    private static LoomLaunchDescriptor CreateSelfContainedDescriptor(
        LoomRuntimeResolutionRequest request,
        string version,
        string runtimeIdentifier,
        string cacheRoot,
        string packageUrl,
        string packageHash,
        string launchFile,
        string guidePath)
        => new(
            LoomRuntimeMode.SelfContained,
            version,
            runtimeIdentifier,
            LoomRuntimeCatalog.GetPackageId(request.Product, runtimeIdentifier),
            [LoomRuntimeCatalog.GetPackageId(request.Product, runtimeIdentifier)],
            packageUrl,
            packageHash,
            cacheRoot,
            launchFile,
            [],
            "self-contained-single-file-package",
            guidePath);

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
            LoomRuntimeCatalog.GetGitHubPackageUrl(packageId, version, normalizedChannel, latestAlias: true),
        };
        return acceptedUrls.Contains(packageUrl.Trim(), StringComparer.Ordinal);
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

    private sealed record DownloadedPackage(byte[] PackageBytes, string PackageHash, string PackageUrl, LoomRuntimePackageValidationResult Validation);

    private sealed record CachedPackage(string LaunchFile, string PackageUrl, string PackageHash);

    private sealed record PublishedPackage(string LaunchFile, string PackageUrl, string PackageHash);

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
