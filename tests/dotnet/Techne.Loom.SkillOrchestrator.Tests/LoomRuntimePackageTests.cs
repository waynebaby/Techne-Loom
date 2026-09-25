using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Techne.Loom.Common.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class LoomRuntimePackageTests
{
    [Fact]
    public void Catalog_MapsSupportedRidsAndExactPackageUrls()
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
    }

    [Fact]
    public void Catalog_DetectRuntimeIdentifierFailsClosedForUnsupportedPlatform()
    {
        var exception = Assert.Throws<PlatformNotSupportedException>(() =>
            LoomRuntimeCatalog.DetectRuntimeIdentifier(false, false, false, System.Runtime.InteropServices.Architecture.X64, false));

        Assert.Contains("Supported runtime identifiers", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageValidator_AcceptsStandardNuGetSignatureMetadata()
    {
        var package = CreateRuntimePackage(
            LoomRuntimeProduct.SkillOrchestrator,
            "1.2.3",
            "win-x64",
            additionalEntries: [".signature.p7s"]);

        var result = LoomRuntimePackageValidator.Validate(
            package,
            LoomRuntimeProduct.SkillOrchestrator,
            "1.2.3",
            "win-x64");

        Assert.Equal("Techne.Loom.SkillOrchestrator.Runtime.win-x64", result.PackageId);
        Assert.Equal("1.2.3", result.Version);
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
    public Task DefaultProcessRunner_CallerCancellationTerminatesTheChild()
        => AssertRunnerStopsChildAsync(TimeSpan.FromMinutes(1), callerCancellation: true);

    [Fact]
    public Task DefaultProcessRunner_TimeoutTerminatesTheChild()
        => AssertRunnerStopsChildAsync(TimeSpan.FromSeconds(15), callerCancellation: false);

    [Fact]
    public async Task DefaultProcessRunner_PreCanceledTokenIsPropagatedBeforeStartAttempt()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var missingExecutable = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new DefaultLoomRuntimeProcessRunner().RunAsync(
                missingExecutable,
                Array.Empty<string>(),
                null,
                TimeSpan.FromMinutes(1),
                cancellationToken: cancellation.Token));
    }

    private static async Task AssertRunnerStopsChildAsync(TimeSpan timeout, bool callerCancellation)
    {
        var root = Path.Combine(Path.GetTempPath(), $"techne-loom-process-cancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var startedPath = Path.Combine(root, "started.marker");
        var releasePath = Path.Combine(root, "release.marker");
        var completedPath = Path.Combine(root, "completed.marker");
        var command = CreateCancellationProbeCommand(startedPath, releasePath, completedPath);
        using var cancellation = new CancellationTokenSource();
        var processTask = new DefaultLoomRuntimeProcessRunner().RunAsync(
            command.FileName,
            command.Arguments,
            root,
            timeout,
            cancellationToken: cancellation.Token);

        try
        {
            await WaitForMarkerAsync(startedPath);
            if (callerCancellation)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await processTask);
            }
            else
            {
                await Assert.ThrowsAsync<LoomRuntimeCommandException>(async () => await processTask);
            }

            await File.WriteAllTextAsync(releasePath, "release");
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            Assert.False(File.Exists(completedPath));
        }
        finally
        {
            cancellation.Cancel();
            await File.WriteAllTextAsync(releasePath, "release");
            if (!processTask.IsCompleted)
            {
                try
                {
                    await processTask;
                }
                catch (OperationCanceledException)
                {
                }
                catch (LoomRuntimeCommandException)
                {
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task WaitForMarkerAsync(string markerPath)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!File.Exists(markerPath) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }

        Assert.True(File.Exists(markerPath), "The child process did not create its startup marker.");
    }
    private static (string FileName, IReadOnlyList<string> Arguments) CreateCancellationProbeCommand(
        string startedPath,
        string releasePath,
        string completedPath)
    {
        if (OperatingSystem.IsWindows())
        {
            var powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
            var script = "$ErrorActionPreference = 'Stop'; New-Item -ItemType File -Path "
                + QuotePowerShell(startedPath)
                + " | Out-Null; while (-not (Test-Path -LiteralPath "
                + QuotePowerShell(releasePath)
                + ")) { Start-Sleep -Milliseconds 20 }; New-Item -ItemType File -Path "
                + QuotePowerShell(completedPath)
                + " | Out-Null";
            return (powershell, ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script]);
        }

        var shellScript = $": > {QuoteShell(startedPath)}; while [ ! -f {QuoteShell(releasePath)} ]; do sleep 0.02; done; : > {QuoteShell(completedPath)}";
        return ("/bin/sh", ["-c", shellScript]);
    }

    private static string QuotePowerShell(string value)
        => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string QuoteShell(string value)
        => "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";


    private static byte[] CreateRuntimePackage(
        LoomRuntimeProduct product,
        string version,
        string runtimeIdentifier,
        IReadOnlyList<string>? additionalEntries = null)
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

    private static void AddEntry(ZipArchive archive, string path, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
