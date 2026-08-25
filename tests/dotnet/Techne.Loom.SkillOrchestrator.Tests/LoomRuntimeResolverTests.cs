using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Techne.Loom.Common.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class LoomRuntimeResolverTests
{
    [Fact]
    public void Catalog_MapsSupportedRidsAndExactUrls()
    {
        Assert.Equal(
            ["win-x64", "win-arm64", "linux-x64", "linux-arm64", "linux-musl-x64", "linux-musl-arm64", "osx-x64", "osx-arm64"],
            LoomRuntimeCatalog.SupportedRuntimeIdentifiers);
        Assert.Equal("1.2.3-beta", LoomRuntimeCatalog.NormalizeVersion("01.002.003-BETA"));
        Assert.Equal(
            "Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64",
            LoomRuntimeCatalog.GetPackageId(LoomRuntimeProduct.SkillOrchestrator, "linux-musl-arm64"));
        Assert.Equal(
            "https://api.nuget.org/v3-flatcontainer/techne.loom.skillorchestrator.runtime.linux-musl-arm64/1.2.3-beta/techne.loom.skillorchestrator.runtime.linux-musl-arm64.1.2.3-beta.nupkg",
            LoomRuntimeCatalog.GetNuGetPackageUrl("Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64", "1.2.3-beta"));
        Assert.Equal(
            "https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64.latest.nupkg",
            LoomRuntimeCatalog.GetGitHubPackageUrl("Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64", "1.2.3-beta", "beta", latestAlias: true));
    }

    [Fact]
    public void Catalog_DetectRuntimeIdentifierFailsClosedForUnsupportedPlatform()
    {
        var exception = Assert.Throws<PlatformNotSupportedException>(() =>
            LoomRuntimeCatalog.DetectRuntimeIdentifier(false, false, false, System.Runtime.InteropServices.Architecture.X64, false));

        Assert.Contains("Supported runtime identifiers", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageValidator_RejectsHashMismatchAndUnexpectedPayload()
    {
        var packageId = LoomRuntimeCatalog.GetPackageId(LoomRuntimeProduct.AgentOrchestrator, "win-x64");
        var package = CreateRuntimePackage(LoomRuntimeProduct.AgentOrchestrator, "1.2.3", "win-x64");
        var mismatch = Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes("different")));

        var hashException = Assert.Throws<LoomRuntimeIntegrityException>(() =>
            LoomRuntimePackageValidator.NormalizeAndValidateSha512(package, mismatch));
        Assert.Contains("does not match", hashException.Message, StringComparison.Ordinal);

        var extraPayload = CreateRuntimePackage(
            LoomRuntimeProduct.AgentOrchestrator,
            "1.2.3",
            "win-x64",
            additionalEntries: ["tools/win-x64/extra.dll"]);
        var shapeException = Assert.Throws<LoomRuntimeIntegrityException>(() =>
            LoomRuntimePackageValidator.Validate(extraPayload, LoomRuntimeProduct.AgentOrchestrator, "1.2.3", "win-x64"));
        Assert.Contains("unexpected file", shapeException.Message, StringComparison.Ordinal);
        Assert.Contains(packageId, shapeException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolver_UsesSelfContainedPackageAndReusesValidCacheOffline()
    {
        using var temp = new TempDirectory();
        var package = CreateRuntimePackage(LoomRuntimeProduct.SkillOrchestrator, "1.2.3", "win-x64");
        var packageId = LoomRuntimeCatalog.GetPackageId(LoomRuntimeProduct.SkillOrchestrator, "win-x64");
        var packageUrl = LoomRuntimeCatalog.GetNuGetPackageUrl(packageId, "1.2.3");
        var hashUrl = LoomRuntimeCatalog.GetNuGetHashUrl(packageId, "1.2.3");
        var handler = new MappingHandler();
        handler.Add(packageUrl, package);
        handler.Add(hashUrl, Convert.ToBase64String(SHA512.HashData(package)));
        var guideRunner = new FakeProcessRunner(temp.Path, "1.2.3");
        var resolver = new LoomRuntimeResolver(new HttpClient(handler), guideRunner);

        var request = new LoomRuntimeResolutionRequest
        {
            Product = LoomRuntimeProduct.SkillOrchestrator,
            Version = "1.2.3",
            RuntimeIdentifier = "win-x64",
            CacheRoot = Path.Combine(temp.Path, "cache"),
            ForceSelfContained = true,
        };

        var first = await resolver.ResolveAsync(request);

        Assert.Equal(LoomRuntimeMode.SelfContained, first.RuntimeMode);
        Assert.Equal(packageId, first.PackageId);
        Assert.Equal("win-x64", first.Rid);
        Assert.Equal(packageUrl, first.PackageUrl);
        Assert.True(File.Exists(first.LaunchFile));
        Assert.Equal(Path.GetFullPath(Path.Combine(temp.Path, "cache")), first.CacheRoot);
        Assert.Equal(1, handler.Requests.Count(url => url == packageUrl));

        var offlineHandler = new MappingHandler();
        var offlineResolver = new LoomRuntimeResolver(new HttpClient(offlineHandler), new FakeProcessRunner(temp.Path, "1.2.3"));
        var second = await offlineResolver.ResolveAsync(request);

        Assert.Equal(first.LaunchFile, second.LaunchFile);
        Assert.Empty(offlineHandler.Requests);
    }

    [Fact]
    public async Task Resolver_DoesNotReuseTamperedCachedExecutable()
    {
        using var temp = new TempDirectory();
        var package = CreateRuntimePackage(LoomRuntimeProduct.SkillOrchestrator, "1.2.3", "win-x64");
        var packageId = LoomRuntimeCatalog.GetPackageId(LoomRuntimeProduct.SkillOrchestrator, "win-x64");
        var packageUrl = LoomRuntimeCatalog.GetNuGetPackageUrl(packageId, "1.2.3");
        var hashUrl = LoomRuntimeCatalog.GetNuGetHashUrl(packageId, "1.2.3");
        var handler = new MappingHandler();
        handler.Add(packageUrl, package);
        handler.Add(hashUrl, Convert.ToBase64String(SHA512.HashData(package)));
        var request = new LoomRuntimeResolutionRequest
        {
            Product = LoomRuntimeProduct.SkillOrchestrator,
            Version = "1.2.3",
            RuntimeIdentifier = "win-x64",
            CacheRoot = Path.Combine(temp.Path, "cache"),
            ForceSelfContained = true,
        };

        var first = await new LoomRuntimeResolver(new HttpClient(handler), new FakeProcessRunner(temp.Path, "1.2.3"))
            .ResolveAsync(request);
        await File.WriteAllTextAsync(first.LaunchFile, "tampered");
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(first.LaunchFile)!, "runtime.json"), "tampered");

        var rebuildHandler = new MappingHandler();
        rebuildHandler.Add(packageUrl, package);
        rebuildHandler.Add(hashUrl, Convert.ToBase64String(SHA512.HashData(package)));
        var second = await new LoomRuntimeResolver(new HttpClient(rebuildHandler), new FakeProcessRunner(temp.Path, "1.2.3"))
            .ResolveAsync(request);

        Assert.Equal(first.LaunchFile, second.LaunchFile);
        Assert.Contains(rebuildHandler.Requests, url => url == packageUrl);
        Assert.Equal("single-file-placeholder", await File.ReadAllTextAsync(second.LaunchFile));
    }

    [Fact]
    public async Task Resolver_SerializesConcurrentResolutionAndDownloadsOnlyOnce()
    {
        using var temp = new TempDirectory();
        var package = CreateRuntimePackage(LoomRuntimeProduct.SkillOrchestrator, "1.2.3", "win-x64");
        var packageId = LoomRuntimeCatalog.GetPackageId(LoomRuntimeProduct.SkillOrchestrator, "win-x64");
        var packageUrl = LoomRuntimeCatalog.GetNuGetPackageUrl(packageId, "1.2.3");
        var hashUrl = LoomRuntimeCatalog.GetNuGetHashUrl(packageId, "1.2.3");
        var handler = new MappingHandler();
        handler.Add(packageUrl, package);
        handler.Add(hashUrl, Convert.ToBase64String(SHA512.HashData(package)));
        var request = new LoomRuntimeResolutionRequest
        {
            Product = LoomRuntimeProduct.SkillOrchestrator,
            Version = "1.2.3",
            RuntimeIdentifier = "win-x64",
            CacheRoot = Path.Combine(temp.Path, "cache"),
            ForceSelfContained = true,
        };

        var resolverA = new LoomRuntimeResolver(new HttpClient(handler), new FakeProcessRunner(temp.Path, "1.2.3"));
        var resolverB = new LoomRuntimeResolver(new HttpClient(handler), new FakeProcessRunner(temp.Path, "1.2.3"));
        var results = await Task.WhenAll(resolverA.ResolveAsync(request), resolverB.ResolveAsync(request));

        Assert.Equal(results[0].LaunchFile, results[1].LaunchFile);
        Assert.All(results, result => Assert.True(File.Exists(result.LaunchFile)));
        Assert.Equal(1, handler.Requests.Count(url => url == packageUrl));
    }

    [Fact]
    public async Task Resolver_ReportsCacheLockTimeoutAsAcquisitionFailure()
    {
        using var temp = new TempDirectory();
        var cacheRoot = Path.Combine(temp.Path, "cache");
        var lockPath = Path.Combine(cacheRoot, ".locks", "so.1.2.3.win-x64.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        using var heldLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var resolver = new LoomRuntimeResolver(new HttpClient(new MappingHandler()), new FakeProcessRunner(temp.Path, "1.2.3"));
        var exception = await Assert.ThrowsAsync<LoomRuntimeAcquisitionException>(() => resolver.ResolveAsync(new LoomRuntimeResolutionRequest
        {
            Product = LoomRuntimeProduct.SkillOrchestrator,
            Version = "1.2.3",
            RuntimeIdentifier = "win-x64",
            CacheRoot = cacheRoot,
            ForceSelfContained = true,
            LockTimeout = TimeSpan.FromMilliseconds(150),
        }));

        Assert.Contains("Timed out acquiring", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolver_ReacquiresCacheWhenGuideVersionDoesNotMatch()
    {
        using var temp = new TempDirectory();
        var package = CreateRuntimePackage(LoomRuntimeProduct.SkillOrchestrator, "1.2.3", "win-x64");
        var packageId = LoomRuntimeCatalog.GetPackageId(LoomRuntimeProduct.SkillOrchestrator, "win-x64");
        var packageUrl = LoomRuntimeCatalog.GetNuGetPackageUrl(packageId, "1.2.3");
        var hashUrl = LoomRuntimeCatalog.GetNuGetHashUrl(packageId, "1.2.3");
        var handler = new MappingHandler();
        handler.Add(packageUrl, package);
        handler.Add(hashUrl, Convert.ToBase64String(SHA512.HashData(package)));
        var request = new LoomRuntimeResolutionRequest
        {
            Product = LoomRuntimeProduct.SkillOrchestrator,
            Version = "1.2.3",
            RuntimeIdentifier = "win-x64",
            CacheRoot = Path.Combine(temp.Path, "cache"),
            ForceSelfContained = true,
        };

        await new LoomRuntimeResolver(new HttpClient(handler), new FakeProcessRunner(temp.Path, "1.2.3"))
            .ResolveAsync(request);
        var reloadingHandler = new MappingHandler();
        reloadingHandler.Add(packageUrl, package);
        reloadingHandler.Add(hashUrl, Convert.ToBase64String(SHA512.HashData(package)));

        var exception = await Assert.ThrowsAsync<LoomRuntimeAcquisitionException>(() =>
            new LoomRuntimeResolver(new HttpClient(reloadingHandler), new FakeProcessRunner(temp.Path, "9.9.9"))
                .ResolveAsync(request));

        Assert.Contains("fresh --guide validation gate", exception.Message, StringComparison.Ordinal);
        Assert.Contains(reloadingHandler.Requests, url => url == packageUrl);
    }

    [Fact]
    public async Task Resolver_DoesNotReuseCacheWithUnexpectedPackageSource()
    {
        using var temp = new TempDirectory();
        var package = CreateRuntimePackage(LoomRuntimeProduct.AgentOrchestrator, "1.2.3", "win-x64");
        var packageId = LoomRuntimeCatalog.GetPackageId(LoomRuntimeProduct.AgentOrchestrator, "win-x64");
        var packageUrl = LoomRuntimeCatalog.GetNuGetPackageUrl(packageId, "1.2.3");
        var hashUrl = LoomRuntimeCatalog.GetNuGetHashUrl(packageId, "1.2.3");
        var handler = new MappingHandler();
        handler.Add(packageUrl, package);
        handler.Add(hashUrl, Convert.ToBase64String(SHA512.HashData(package)));
        var request = new LoomRuntimeResolutionRequest
        {
            Product = LoomRuntimeProduct.AgentOrchestrator,
            Version = "1.2.3",
            RuntimeIdentifier = "win-x64",
            CacheRoot = Path.Combine(temp.Path, "cache"),
            ForceSelfContained = true,
        };

        var first = await new LoomRuntimeResolver(new HttpClient(handler), new FakeProcessRunner(temp.Path, "1.2.3"))
            .ResolveAsync(request);
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(first.LaunchFile)!, "package.url"), "https://attacker.invalid/runtime.nupkg");

        var offlineHandler = new MappingHandler();
        var exception = await Assert.ThrowsAsync<LoomRuntimeAcquisitionException>(() =>
            new LoomRuntimeResolver(new HttpClient(offlineHandler), new FakeProcessRunner(temp.Path, "1.2.3"))
                .ResolveAsync(request));

        Assert.Contains("Unable to acquire exact self-contained runtime package", exception.Message, StringComparison.Ordinal);
        Assert.NotEmpty(offlineHandler.Requests);
    }

    [Fact]
    public void PackageValidator_RejectsOversizedManifest()
    {
        var package = CreateRuntimePackage(
            LoomRuntimeProduct.AgentOrchestrator,
            "1.2.3",
            "win-x64",
            manifestPaddingBytes: 2048);

        var exception = Assert.Throws<LoomRuntimeIntegrityException>(() =>
            LoomRuntimePackageValidator.Validate(
                package,
                LoomRuntimeProduct.AgentOrchestrator,
                "1.2.3",
                "win-x64",
                new LoomRuntimePackageLimits(MaxManifestBytes: 1024)));

        Assert.Contains("manifest exceeds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolver_FallsBackToGitHubAfterNuGetPackageUnavailable()
    {
        using var temp = new TempDirectory();
        var package = CreateRuntimePackage(LoomRuntimeProduct.AgentOrchestrator, "1.2.3", "win-x64");
        var packageId = LoomRuntimeCatalog.GetPackageId(LoomRuntimeProduct.AgentOrchestrator, "win-x64");
        var githubPackageUrl = LoomRuntimeCatalog.GetGitHubPackageUrl(packageId, "1.2.3", "released");
        var githubHashUrl = githubPackageUrl + ".sha512";
        var handler = new MappingHandler();
        handler.Add(githubPackageUrl, package);
        handler.Add(githubHashUrl, Convert.ToBase64String(SHA512.HashData(package)));
        var resolver = new LoomRuntimeResolver(new HttpClient(handler), new FakeProcessRunner(temp.Path, "1.2.3"));

        var descriptor = await resolver.ResolveAsync(new LoomRuntimeResolutionRequest
        {
            Product = LoomRuntimeProduct.AgentOrchestrator,
            Version = "1.2.3",
            Channel = "released",
            RuntimeIdentifier = "win-x64",
            CacheRoot = Path.Combine(temp.Path, "cache"),
            ForceSelfContained = true,
        });

        Assert.Equal(githubPackageUrl, descriptor.PackageUrl);
        Assert.Contains(handler.Requests, url => url == LoomRuntimeCatalog.GetNuGetPackageUrl(packageId, "1.2.3"));
        Assert.True(File.Exists(descriptor.LaunchFile));
    }

    [Fact]
    public async Task Resolver_DoesNotHideNuGetIntegrityFailureWithGitHubFallback()
    {
        using var temp = new TempDirectory();
        var package = CreateRuntimePackage(LoomRuntimeProduct.AgentOrchestrator, "1.2.3", "win-x64");
        var packageId = LoomRuntimeCatalog.GetPackageId(LoomRuntimeProduct.AgentOrchestrator, "win-x64");
        var nugetPackageUrl = LoomRuntimeCatalog.GetNuGetPackageUrl(packageId, "1.2.3");
        var nugetHashUrl = LoomRuntimeCatalog.GetNuGetHashUrl(packageId, "1.2.3");
        var githubPackageUrl = LoomRuntimeCatalog.GetGitHubPackageUrl(packageId, "1.2.3", "released");
        var handler = new MappingHandler();
        handler.Add(nugetPackageUrl, package);
        handler.Add(nugetHashUrl, Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes("wrong"))));
        handler.Add(githubPackageUrl, package);
        handler.Add(githubPackageUrl + ".sha512", Convert.ToBase64String(SHA512.HashData(package)));
        var resolver = new LoomRuntimeResolver(new HttpClient(handler), new FakeProcessRunner(temp.Path, "1.2.3"));

        var exception = await Assert.ThrowsAsync<LoomRuntimeIntegrityException>(() => resolver.ResolveAsync(new LoomRuntimeResolutionRequest
        {
            Product = LoomRuntimeProduct.AgentOrchestrator,
            Version = "1.2.3",
            RuntimeIdentifier = "win-x64",
            CacheRoot = Path.Combine(temp.Path, "cache"),
            ForceSelfContained = true,
        }));

        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(handler.Requests, url => url == githubPackageUrl);
    }

    [Fact]
    public async Task Resolver_PrefersFrameworkBundleWhenNet9HostAndGuideAreUsable()
    {
        using var temp = new TempDirectory();
        var bundle = Path.Combine(temp.Path, "framework");
        Directory.CreateDirectory(bundle);
        await File.WriteAllTextAsync(Path.Combine(bundle, "ao.dll"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(bundle, "ao.runtimeconfig.json"), "{}");
        var runner = new FakeProcessRunner(temp.Path, "1.2.3") { ReportNet9Host = true };
        var resolver = new LoomRuntimeResolver(new HttpClient(new MappingHandler()), runner);

        var descriptor = await resolver.ResolveAsync(new LoomRuntimeResolutionRequest
        {
            Product = LoomRuntimeProduct.AgentOrchestrator,
            Version = "1.2.3",
            RuntimeIdentifier = "win-x64",
            FrameworkBundleDirectory = bundle,
        });

        Assert.Equal(LoomRuntimeMode.FrameworkDependent, descriptor.RuntimeMode);
        Assert.Equal(Path.Combine(bundle, "ao.dll"), descriptor.LaunchFile);
        Assert.Equal(["exec", "--runtimeconfig", Path.Combine(bundle, "ao.runtimeconfig.json")], descriptor.LaunchPrefixArgs);
        Assert.Equal(["Techne.Loom.AgentOrchestrator", "Techne.Loom.Common", "Techne.Loom.Abstractions"], descriptor.PackageIds);
        Assert.Contains(runner.Invocations, invocation => invocation.FileName == "dotnet" && invocation.Arguments.SequenceEqual(["--list-runtimes"]));
    }

    private static byte[] CreateRuntimePackage(
        LoomRuntimeProduct product,
        string version,
        string runtimeIdentifier,
        IReadOnlyList<string>? additionalEntries = null,
        int manifestPaddingBytes = 0)
    {
        var packageId = LoomRuntimeCatalog.GetPackageId(product, runtimeIdentifier);
        var entryPoint = LoomRuntimeCatalog.GetEntryFile(product, runtimeIdentifier);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, $"{packageId}.nuspec", $"<package><metadata><id>{packageId}</id><version>{version}</version><tags>runtime rid:{runtimeIdentifier}</tags></metadata></package>");
            var manifest = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["schema"] = "techne-loom-runtime-v1",
                ["product"] = LoomRuntimeCatalog.GetEntryPoint(product),
                ["package_id"] = packageId,
                ["version"] = version,
                ["rid"] = runtimeIdentifier,
                ["entrypoint"] = entryPoint,
                ["single_file"] = true,
            };
            if (manifestPaddingBytes > 0)
            {
                manifest["padding"] = new string('x', manifestPaddingBytes);
            }
            AddEntry(archive, $"tools/{runtimeIdentifier}/runtime.json", JsonSerializer.Serialize(manifest));
            AddEntry(archive, $"tools/{runtimeIdentifier}/{entryPoint}", "single-file-placeholder");
            foreach (var additionalEntry in additionalEntries ?? [])
            {
                AddEntry(archive, additionalEntry, "unexpected");
            }
        }

        return output.ToArray();
    }

    private static void AddEntry(ZipArchive archive, string path, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private sealed class MappingHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _responses = new(StringComparer.Ordinal);

        public ConcurrentQueue<string> Requests { get; } = new();

        public void Add(string url, byte[] content)
            => _responses[url] = content;

        public void Add(string url, string content)
            => Add(url, Encoding.UTF8.GetBytes(content));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(request.RequestUri!.ToString());
            if (!_responses.TryGetValue(request.RequestUri.ToString(), out var content))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content),
                RequestMessage = request,
            });
        }
    }

    private sealed class FakeProcessRunner : ILoomRuntimeProcessRunner
    {
        private readonly string _root;
        private readonly string _version;

        public FakeProcessRunner(string root, string version)
        {
            _root = root;
            _version = version;
            DocsRoot = Path.Combine(root, $"docs-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DocsRoot);
            GuidePath = Path.Combine(DocsRoot, "guide.md");
            File.WriteAllText(GuidePath, "guide");
        }

        public bool ReportNet9Host { get; init; }
        public string DocsRoot { get; }
        public string GuidePath { get; }
        public List<Invocation> Invocations { get; } = [];

        public Task<LoomProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            Invocations.Add(new Invocation(fileName, arguments.ToArray()));
            if (fileName == "dotnet" && arguments.SequenceEqual(["--list-runtimes"]))
            {
                return Task.FromResult(ReportNet9Host
                    ? new LoomProcessResult(true, 0, "Microsoft.NETCore.App 9.0.0 [test]", string.Empty)
                    : new LoomProcessResult(false, -1, string.Empty, string.Empty));
            }

            var guide = JsonSerializer.Serialize(new { version = _version, docs_root = DocsRoot, guide_path = GuidePath });
            return Task.FromResult(new LoomProcessResult(true, 0, guide, string.Empty));
        }
    }

    private sealed record Invocation(string FileName, IReadOnlyList<string> Arguments);

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"techne-loom-runtime-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
