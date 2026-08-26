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
    public void PackageValidator_RejectsChineseDocsInEnglishTree()
    {
        var package = CreateRuntimePackage(
            LoomRuntimeProduct.AgentOrchestrator,
            "1.2.3",
            "win-x64",
            additionalEntries: ["tools/win-x64/docs/en/zh-cn/ao-guide.md"]);

        var exception = Assert.Throws<LoomRuntimeIntegrityException>(() =>
            LoomRuntimePackageValidator.Validate(package, LoomRuntimeProduct.AgentOrchestrator, "1.2.3", "win-x64"));

        Assert.Contains("Chinese docs tree", exception.Message, StringComparison.Ordinal);
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
        Assert.Single(first.PackageIds);
        Assert.Equal(packageId, first.PackageIds[0]);
        Assert.DoesNotContain("Techne.Loom.Common", first.PackageIds);
        Assert.DoesNotContain("Techne.Loom.Abstractions", first.PackageIds);
        Assert.True(File.Exists(first.LaunchFile));
        Assert.Equal(Path.GetFullPath(Path.Combine(temp.Path, "cache")), first.CacheRoot);
        Assert.Equal(hashUrl, first.PackageHashUrl);
        var cachedGuidePath = Path.Combine(Path.GetDirectoryName(first.LaunchFile)!, "docs", "en", "guides", "so-guide.md");
        Assert.True(File.Exists(cachedGuidePath));
        LoomPreparationDiagnostics.ValidateForMode(first);
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
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(first.LaunchFile)!, "docs", "en", "guides", "so-guide.md"), "tampered");

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
    public async Task Resolver_UsesExplicitFrameworkBundleStrictlyWhenNet9HostAndGuideAreUsable()
    {
        using var temp = new TempDirectory();
        var bundle = Path.Combine(temp.Path, "framework");
        Directory.CreateDirectory(bundle);
        await File.WriteAllTextAsync(Path.Combine(bundle, "ao.dll"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(bundle, "ao.deps.json"), CreateFrameworkDeps(LoomRuntimeProduct.AgentOrchestrator, "1.2.3"));
        await File.WriteAllTextAsync(Path.Combine(bundle, "ao.runtimeconfig.json"), "{\"runtimeOptions\":{\"tfm\":\"net9.0\",\"framework\":{\"name\":\"Microsoft.NETCore.App\",\"version\":\"9.0.0\"}}}");
        await File.WriteAllTextAsync(Path.Combine(bundle, "Techne.Loom.Common.dll"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(bundle, "Techne.Loom.Abstractions.dll"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(bundle, "Microsoft.CodeAnalysis.dll"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(bundle, "Microsoft.CodeAnalysis.CSharp.dll"), string.Empty);
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
        Assert.Equal(LoomRuntimeProduct.AgentOrchestrator, descriptor.Product);
        Assert.Equal("released", descriptor.Channel);
        Assert.Equal(Path.Combine(bundle, "ao.dll"), descriptor.LaunchFile);
        Assert.Equal(Path.GetFullPath(bundle), descriptor.RuntimeRoot);
        Assert.Equal(["exec", "--depsfile", Path.Combine(bundle, "ao.deps.json"), "--runtimeconfig", Path.Combine(bundle, "ao.runtimeconfig.json")], descriptor.LaunchPrefixArgs);
        Assert.Equal(["Techne.Loom.AgentOrchestrator", "Techne.Loom.Common", "Techne.Loom.Abstractions"], descriptor.PackageIds);
        Assert.Null(descriptor.ExtractionBaseDirectory);
        Assert.StartsWith("prep-", descriptor.PreparationId, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.GuideHash));
        Assert.Equal(runner.DocsRoot, descriptor.DocsRoot);
        LoomPreparationDiagnostics.ValidateForMode(descriptor);
        Assert.Contains(runner.Invocations, invocation => invocation.FileName == "dotnet" && invocation.Arguments.SequenceEqual(["--list-runtimes"]));
    }

    [Fact]
    public async Task Resolver_FailsClosedForExplicitFrameworkWithoutNet9Host()
    {
        using var temp = new TempDirectory();
        var bundle = Path.Combine(temp.Path, "framework");
        Directory.CreateDirectory(bundle);
        await File.WriteAllTextAsync(Path.Combine(bundle, "ao.dll"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(bundle, "ao.deps.json"), CreateFrameworkDeps(LoomRuntimeProduct.AgentOrchestrator, "1.2.3"));
        await File.WriteAllTextAsync(Path.Combine(bundle, "ao.runtimeconfig.json"), "{\"runtimeOptions\":{\"tfm\":\"net9.0\",\"framework\":{\"name\":\"Microsoft.NETCore.App\",\"version\":\"9.0.0\"}}}");
        var handler = new MappingHandler();
        var runner = new FakeProcessRunner(temp.Path, "1.2.3") { ReportNet9Host = false };
        var resolver = new LoomRuntimeResolver(new HttpClient(handler), runner);

        var exception = await Assert.ThrowsAsync<LoomRuntimeHostStartupException>(() => resolver.ResolveAsync(new LoomRuntimeResolutionRequest
        {
            Product = LoomRuntimeProduct.AgentOrchestrator,
            Version = "1.2.3",
            RuntimeIdentifier = "win-x64",
            FrameworkBundleDirectory = bundle,
        }));

        Assert.Equal(LoomRuntimeFailureCategory.HostStartup, exception.FailureCategory);
        Assert.Contains("never falls back to self-contained mode", exception.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Resolver_FailsClosedWhenFrameworkDependencyClosureIsIncomplete()
    {
        using var temp = new TempDirectory();
        var bundle = Path.Combine(temp.Path, "framework");
        Directory.CreateDirectory(bundle);
        await File.WriteAllTextAsync(Path.Combine(bundle, "ao.dll"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(bundle, "ao.deps.json"), CreateFrameworkDeps(LoomRuntimeProduct.AgentOrchestrator, "1.2.3", includeCommon: false));
        await File.WriteAllTextAsync(Path.Combine(bundle, "ao.runtimeconfig.json"), "{\"runtimeOptions\":{\"tfm\":\"net9.0\",\"framework\":{\"name\":\"Microsoft.NETCore.App\",\"version\":\"9.0.0\"}}}");
        var runner = new FakeProcessRunner(temp.Path, "1.2.3") { ReportNet9Host = true };

        var exception = await Assert.ThrowsAsync<LoomRuntimeIntegrityException>(() => new LoomRuntimeResolver(new HttpClient(new MappingHandler()), runner)
            .ResolveAsync(new LoomRuntimeResolutionRequest
            {
                Product = LoomRuntimeProduct.AgentOrchestrator,
                Version = "1.2.3",
                RuntimeIdentifier = "win-x64",
                FrameworkBundleDirectory = bundle,
            }));

        Assert.Contains(".NET CLI runtime bundle", exception.Message, StringComparison.Ordinal);
        Assert.Contains("does not contain the exact .NET runtime closure", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Techne.Loom.Common/1.2.3", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolver_FailsClosedWhenFrameworkBundleMissingDepsFile()
    {
        using var temp = new TempDirectory();
        var bundle = Path.Combine(temp.Path, "framework");
        Directory.CreateDirectory(bundle);
        await File.WriteAllTextAsync(Path.Combine(bundle, "ao.dll"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(bundle, "ao.runtimeconfig.json"), "{\"runtimeOptions\":{\"tfm\":\"net9.0\",\"framework\":{\"name\":\"Microsoft.NETCore.App\",\"version\":\"9.0.0\"}}}");
        var runner = new FakeProcessRunner(temp.Path, "1.2.3") { ReportNet9Host = true };

        var exception = await Assert.ThrowsAsync<LoomRuntimeIntegrityException>(() => new LoomRuntimeResolver(new HttpClient(new MappingHandler()), runner)
            .ResolveAsync(new LoomRuntimeResolutionRequest
            {
                Product = LoomRuntimeProduct.AgentOrchestrator,
                Version = "1.2.3",
                RuntimeIdentifier = "win-x64",
                FrameworkBundleDirectory = bundle,
            }));

        Assert.Equal(LoomRuntimeFailureCategory.Integrity, exception.FailureCategory);
        Assert.Contains("ao.deps.json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolver_RejectsConflictingModeSelection()
    {
        using var temp = new TempDirectory();
        var resolver = new LoomRuntimeResolver(new HttpClient(new MappingHandler()), new FakeProcessRunner(temp.Path, "1.2.3"));

        await Assert.ThrowsAsync<ArgumentException>(() => resolver.ResolveAsync(new LoomRuntimeResolutionRequest
        {
            Product = LoomRuntimeProduct.SkillOrchestrator,
            Version = "1.2.3",
            RuntimeIdentifier = "win-x64",
            FrameworkBundleDirectory = Path.Combine(temp.Path, "framework"),
            ForceSelfContained = true,
        }));
    }

    [Fact]
    public async Task Resolver_NeverAcquiresFromLatestAliasSource()
    {
        using var temp = new TempDirectory();
        var packageId = LoomRuntimeCatalog.GetPackageId(LoomRuntimeProduct.SkillOrchestrator, "win-x64");
        var latestAliasUrl = LoomRuntimeCatalog.GetGitHubPackageUrl(packageId, "1.2.3", "beta", latestAlias: true);
        var handler = new MappingHandler();
        handler.Add(latestAliasUrl, CreateRuntimePackage(LoomRuntimeProduct.SkillOrchestrator, "1.2.3", "win-x64"));
        handler.Add(latestAliasUrl + ".sha512", Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes("irrelevant"))));

        var exception = await Assert.ThrowsAsync<LoomRuntimeAcquisitionException>(() => new LoomRuntimeResolver(new HttpClient(handler), new FakeProcessRunner(temp.Path, "1.2.3"))
            .ResolveAsync(new LoomRuntimeResolutionRequest
            {
                Product = LoomRuntimeProduct.SkillOrchestrator,
                Version = "1.2.3",
                Channel = "beta",
                RuntimeIdentifier = "win-x64",
                CacheRoot = Path.Combine(temp.Path, "cache"),
                ForceSelfContained = true,
            }));

        Assert.Contains("Unable to acquire exact self-contained runtime package", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(handler.Requests, url => url == latestAliasUrl);
    }

    [Fact]
    public async Task Resolver_ClassifiesUnreadableGuideAsGuideValidationFailure()
    {
        using var temp = new TempDirectory();
        using var runner = new UnreadableGuideProcessRunner("1.2.3");
        var resolver = new LoomRuntimeResolver(new HttpClient(new MappingHandler()), runner);
        var bundle = Path.Combine(temp.Path, "framework");
        Directory.CreateDirectory(bundle);
        await File.WriteAllTextAsync(Path.Combine(bundle, "ao.dll"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(bundle, "ao.deps.json"), CreateFrameworkDeps(LoomRuntimeProduct.AgentOrchestrator, "1.2.3"));
        await File.WriteAllTextAsync(Path.Combine(bundle, "ao.runtimeconfig.json"), "{\"runtimeOptions\":{\"tfm\":\"net9.0\",\"framework\":{\"name\":\"Microsoft.NETCore.App\",\"version\":\"9.0.0\"}}}");
        await File.WriteAllTextAsync(Path.Combine(bundle, "Techne.Loom.Common.dll"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(bundle, "Techne.Loom.Abstractions.dll"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(bundle, "Microsoft.CodeAnalysis.dll"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(bundle, "Microsoft.CodeAnalysis.CSharp.dll"), string.Empty);

        var exception = await Assert.ThrowsAsync<LoomRuntimeGuideValidationException>(() => resolver.ResolveAsync(new LoomRuntimeResolutionRequest
        {
            Product = LoomRuntimeProduct.AgentOrchestrator,
            Version = "1.2.3",
            RuntimeIdentifier = "win-x64",
            FrameworkBundleDirectory = bundle,
        }));

        Assert.Equal(LoomRuntimeFailureCategory.GuideValidation, exception.FailureCategory);
        Assert.IsType<IOException>(exception.InnerException);
    }

    [Fact]
    public void RuntimeExceptions_ExposeStructuredFailureCategories()
    {
        Assert.Equal(LoomRuntimeFailureCategory.Acquisition, new LoomRuntimeAcquisitionException("x").FailureCategory);
        Assert.Equal(LoomRuntimeFailureCategory.Integrity, new LoomRuntimeIntegrityException("x").FailureCategory);
        Assert.Equal(LoomRuntimeFailureCategory.HostStartup, new LoomRuntimeHostStartupException("x").FailureCategory);
        Assert.Equal(LoomRuntimeFailureCategory.GuideValidation, new LoomRuntimeGuideValidationException("x").FailureCategory);
        Assert.Equal(LoomRuntimeFailureCategory.Command, new LoomRuntimeCommandException("x").FailureCategory);
    }

    [Fact]
    public void PreparationDiagnostics_ValidatesModeSpecificRequiredFields()
    {
        var valid = CreateSelfContainedDescriptorForDiagnostics();
        LoomPreparationDiagnostics.ValidateForMode(valid);
        var json = LoomPreparationDiagnostics.ToJson(valid);
        Assert.Contains("\"preparation_id\"", json, StringComparison.Ordinal);
        Assert.Contains("\"runtime_root\"", json, StringComparison.Ordinal);

        var missingPackageHash = valid with { PackageHash = null };
        var hashException = Assert.Throws<LoomRuntimeIntegrityException>(() => LoomPreparationDiagnostics.ValidateForMode(missingPackageHash));
        Assert.Contains("package hash", hashException.Message, StringComparison.Ordinal);

        var frameworkWithExtraction = valid with
        {
            RuntimeMode = LoomRuntimeMode.FrameworkDependent,
            PackageId = "Techne.Loom.SkillOrchestrator",
            PackageIds = ["Techne.Loom.SkillOrchestrator", "Techne.Loom.Common", "Techne.Loom.Abstractions"],
            LaunchFile = Path.Combine(valid.RuntimeRoot, "so.dll"),
            LaunchPrefixArgs = ["exec", "--depsfile", "x.deps.json", "--runtimeconfig", "x.runtimeconfig.json"],
        };
        var extractionException = Assert.Throws<LoomRuntimeIntegrityException>(() => LoomPreparationDiagnostics.ValidateForMode(frameworkWithExtraction));
        Assert.Contains("must not carry an extraction base directory", extractionException.Message, StringComparison.Ordinal);

        var selfContainedWithoutExtraction = valid with { ExtractionBaseDirectory = null };
        var missingExtractionException = Assert.Throws<LoomRuntimeIntegrityException>(() => LoomPreparationDiagnostics.ValidateForMode(selfContainedWithoutExtraction));
        Assert.Contains("extraction base directory", missingExtractionException.Message, StringComparison.Ordinal);
    }

    private static LoomLaunchDescriptor CreateSelfContainedDescriptorForDiagnostics()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), "techne-loom-diagnostics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runtimeRoot);
        var launchFile = Path.Combine(runtimeRoot, "so.exe");
        File.WriteAllText(launchFile, "placeholder");
        var docsRoot = Path.Combine(runtimeRoot, "docs", "en");
        Directory.CreateDirectory(docsRoot);
        var guidePath = Path.Combine(docsRoot, "guide.md");
        File.WriteAllText(guidePath, "guide");
        return new LoomLaunchDescriptor(
            LoomRuntimeMode.SelfContained,
            LoomRuntimeProduct.SkillOrchestrator,
            "1.2.3",
            "beta",
            "win-x64",
            "Techne.Loom.SkillOrchestrator.Runtime.win-x64",
            ["Techne.Loom.SkillOrchestrator.Runtime.win-x64"],
            "https://example.invalid/package.nupkg",
            Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes("package"))),
            runtimeRoot,
            runtimeRoot,
            launchFile,
            [],
            "self-contained-single-file-package",
            guidePath,
            docsRoot,
            Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes("guide"))),
            Path.Combine(runtimeRoot, ".extraction"),
            LoomPreparationDiagnostics.CreatePreparationId(LoomRuntimeMode.SelfContained, LoomRuntimeProduct.SkillOrchestrator, "1.2.3", "win-x64", runtimeRoot, "hash"),
            LoomRuntimeCatalog.GetNuGetHashUrl("Techne.Loom.SkillOrchestrator.Runtime.win-x64", "1.2.3"));
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
                ["docs_root"] = $"tools/{runtimeIdentifier}/docs/en",
                ["guide_path"] = $"guides/{LoomRuntimeCatalog.GetEntryPoint(product)}-guide.md",
                ["single_file"] = true,
            };
            if (manifestPaddingBytes > 0)
            {
                manifest["padding"] = new string('x', manifestPaddingBytes);
            }
            AddEntry(archive, $"tools/{runtimeIdentifier}/runtime.json", JsonSerializer.Serialize(manifest));
            AddEntry(archive, $"tools/{runtimeIdentifier}/{entryPoint}", "single-file-placeholder");
            AddEntry(archive, $"tools/{runtimeIdentifier}/docs/en/guides/{LoomRuntimeCatalog.GetEntryPoint(product)}-guide.md", "guide");
            foreach (var additionalEntry in additionalEntries ?? [])
            {
                AddEntry(archive, additionalEntry, "unexpected");
            }
        }

        return output.ToArray();
    }

    private static string CreateFrameworkDeps(
        LoomRuntimeProduct product,
        string version,
        bool includeCommon = true,
        bool includeAbstractions = true)
    {
        var entryPoint = LoomRuntimeCatalog.GetEntryPoint(product);
        var target = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [$"{entryPoint}/{version}"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["runtime"] = new Dictionary<string, object?> { [$"{entryPoint}.dll"] = new Dictionary<string, object?>() },
            },
            ["Microsoft.CodeAnalysis.Common/4.12.0"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["runtime"] = new Dictionary<string, object?> { ["lib/net8.0/Microsoft.CodeAnalysis.dll"] = new Dictionary<string, object?>() },
            },
            ["Microsoft.CodeAnalysis.CSharp/4.12.0"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["runtime"] = new Dictionary<string, object?> { ["lib/net8.0/Microsoft.CodeAnalysis.CSharp.dll"] = new Dictionary<string, object?>() },
            },
        };
        if (includeCommon)
        {
            target[$"Techne.Loom.Common/{version}"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["runtime"] = new Dictionary<string, object?> { ["Techne.Loom.Common.dll"] = new Dictionary<string, object?>() },
            };
        }
        if (includeAbstractions)
        {
            target[$"Techne.Loom.Abstractions/{version}"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["runtime"] = new Dictionary<string, object?> { ["Techne.Loom.Abstractions.dll"] = new Dictionary<string, object?>() },
            };
        }

        var libraries = target.Keys.ToDictionary(
            key => key,
            _ => (object?)new Dictionary<string, object?>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        return JsonSerializer.Serialize(new
        {
            targets = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [".NETCoreApp,Version=v9.0"] = target,
            },
            libraries,
        });
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

    private sealed class UnreadableGuideProcessRunner : ILoomRuntimeProcessRunner, IDisposable
    {
        private readonly string _version;
        private readonly string _docsRoot;
        private readonly string _guidePath;
        private readonly FileStream _guideLock;

        public UnreadableGuideProcessRunner(string version)
        {
            _version = version;
            _docsRoot = Path.Combine(Path.GetTempPath(), "techne-loom-unreadable-guide-docs-" + Guid.NewGuid().ToString("N"));
            var guideDirectory = Path.Combine(_docsRoot, "guides");
            Directory.CreateDirectory(guideDirectory);
            _guidePath = Path.Combine(guideDirectory, "ao-guide.md");
            File.WriteAllText(_guidePath, "guide");
            _guideLock = new FileStream(_guidePath, FileMode.Open, FileAccess.Read, FileShare.None);
        }

        public void Dispose()
        {
            _guideLock.Dispose();
            if (Directory.Exists(_docsRoot))
            {
                Directory.Delete(_docsRoot, recursive: true);
            }
        }

        public Task<LoomProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, TimeSpan timeout, IDictionary<string, string>? environmentVariables = null, CancellationToken cancellationToken = default)
        {
            if (fileName == "dotnet" && arguments.SequenceEqual(["--list-runtimes"]))
            {
                return Task.FromResult(new LoomProcessResult(true, 0, "Microsoft.NETCore.App 9.0.0 [test]", string.Empty));
            }

            return Task.FromResult(new LoomProcessResult(true, 0, JsonSerializer.Serialize(new { version = _version, docs_root = _docsRoot, guide_path = _guidePath }), string.Empty));
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
            var guideDirectory = Path.Combine(DocsRoot, "guides");
            Directory.CreateDirectory(guideDirectory);
            GuidePath = Path.Combine(guideDirectory, "so-guide.md");
            File.WriteAllText(Path.Combine(guideDirectory, "ao-guide.md"), "guide");
            File.WriteAllText(GuidePath, "guide");
        }

        public bool ReportNet9Host { get; init; }
        public string DocsRoot { get; }
        public string GuidePath { get; }
        public List<Invocation> Invocations { get; } = [];

        public Task<LoomProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, TimeSpan timeout, IDictionary<string, string>? environmentVariables = null, CancellationToken cancellationToken = default)
        {
            Invocations.Add(new Invocation(fileName, arguments.ToArray(), environmentVariables is null ? [] : environmentVariables.ToDictionary(pair => pair.Key, pair => pair.Value)));
            if (fileName == "dotnet" && arguments.SequenceEqual(["--list-runtimes"]))
            {
                return Task.FromResult(ReportNet9Host
                    ? new LoomProcessResult(true, 0, "Microsoft.NETCore.App 9.0.0 [test]", string.Empty)
                    : new LoomProcessResult(false, -1, string.Empty, string.Empty));
            }

            var launchArgument = arguments.LastOrDefault(argument => argument.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) ?? fileName;
            var entryPoint = Path.GetFileNameWithoutExtension(launchArgument);
            var guideName = string.Equals(entryPoint, "so", StringComparison.OrdinalIgnoreCase) ? "so-guide.md" : "ao-guide.md";
            var guidePath = Path.Combine(DocsRoot, "guides", guideName);
            var guide = JsonSerializer.Serialize(new { version = _version, docs_root = DocsRoot, guide_path = guidePath });
            return Task.FromResult(new LoomProcessResult(true, 0, guide, string.Empty));
        }
    }

    private sealed record Invocation(string FileName, IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, string> EnvironmentVariables);

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
