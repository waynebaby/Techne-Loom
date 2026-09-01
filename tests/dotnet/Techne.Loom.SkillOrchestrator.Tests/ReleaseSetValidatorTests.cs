using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using Techne.Loom.Common.ReleaseSet;
using Techne.Loom.Common.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class ReleaseSetValidatorTests
{
    [Fact]
    public async Task CheckInStableClosurePassesWithOneNuGetVersion()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        var report = await fixture.ValidateAsync(LoomReleaseSetAuthorityMode.CheckIn);

        Assert.True(report.IsValid, report.ToDiagnosticString());
        Assert.Equal("0.3.270", report.ExpectedVersion);
        Assert.Equal(20, report.LatestPackageVersions.Count);
    }

    [Fact]
    public async Task CheckInBetaClosurePassesWithOneNuGetVersion()
    {
        using var fixture = ReleaseSetFixture.Create("beta", "0.3.258-beta");
        var report = await fixture.ValidateAsync(LoomReleaseSetAuthorityMode.CheckIn);

        Assert.True(report.IsValid, report.ToDiagnosticString());
        Assert.Equal("0.3.258-beta", report.ExpectedVersion);
    }

    [Fact]
    public async Task MixedPublishedVersionsFailClosed()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        fixture.PackageVersions["Techne.Loom.Common"] = "0.3.269";

        var report = await fixture.ValidateAsync(LoomReleaseSetAuthorityMode.CheckIn);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "mixed-published-versions");
    }

    [Fact]
    public async Task StaleBetaSurfaceFailsClosed()
    {
        using var fixture = ReleaseSetFixture.Create("beta", "0.3.258-beta");
        fixture.Replace(".agents/skills/so/SKILL.md", "0.3.258-beta", "0.3.253-beta");

        var report = await fixture.ValidateAsync(LoomReleaseSetAuthorityMode.CheckIn);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "stale-surface-version");
    }

    [Fact]
    public async Task ClassifiedHistoricalArtifactDoesNotBecomeActiveVersionEvidence()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        File.WriteAllText(Path.Combine(fixture.Root, "historical.md"), "old package 0.1.1");
        fixture.Manifest.ExcludedArtifacts!.Add(new LoomReleaseSetExcludedArtifact
        {
            Path = "historical.md",
            Classification = "historical",
            Reason = "Reproducible audit fixture.",
        });
        fixture.WriteManifest();

        var report = await fixture.ValidateAsync(LoomReleaseSetAuthorityMode.CheckIn);

        Assert.True(report.IsValid, report.ToDiagnosticString());
    }

    [Fact]
    public async Task ExplicitBadPackageExampleDoesNotBecomeActiveVersionEvidence()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        fixture.Replace("indexes/released.md", "<!-- package-version-block:end -->", "<!-- package-version-block:end -->\n- Bad: restore one package at 0.2.77 instead of the active version");

        var report = await fixture.ValidateAsync(LoomReleaseSetAuthorityMode.CheckIn);

        Assert.True(report.IsValid, report.ToDiagnosticString());
    }

    [Fact]
    public async Task DocumentCopyHashCanonicalizesLineEndingsAndTrailingWhitespace()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        File.WriteAllText(Path.Combine(fixture.Root, "docs", "ao-source.md"), "# ao guide\r\n \r\n");

        var report = await fixture.ValidateAsync(LoomReleaseSetAuthorityMode.CheckIn);

        Assert.True(report.IsValid, report.ToDiagnosticString());
    }

    [Fact]
    public async Task DocumentCopyHashMismatchFailsClosed()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        File.WriteAllText(Path.Combine(fixture.Root, "docs", "ao-source.md"), "# changed\n");

        var report = await fixture.ValidateAsync(LoomReleaseSetAuthorityMode.CheckIn);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "document-copy-hash");
    }

    [Fact]
    public async Task FloatingAutomationDependencyFailsClosed()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        fixture.Replace(".github/workflows/publish.yml", "needs.version.outputs.package_version", "needs.version.outputs.package_version\n          dotnet add package Techne.Loom.Common --version latest");

        var report = await fixture.ValidateAsync(LoomReleaseSetAuthorityMode.CheckIn);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "floating-automation-dependency" || issue.Code == "forbidden-automation-pattern");
    }

    [Fact]
    public async Task MissingAndMalformedFilesFailClosed()
    {
        using var missingFixture = ReleaseSetFixture.Create("released", "0.3.270");
        File.Delete(Path.Combine(missingFixture.Root, "locks", "ao.json"));
        var missingReport = await missingFixture.ValidateAsync(LoomReleaseSetAuthorityMode.CheckIn);
        Assert.False(missingReport.IsValid);
        Assert.Contains(missingReport.Issues, issue => issue.Code == "missing-package-lock");

        using var malformedFixture = ReleaseSetFixture.Create("released", "0.3.270");
        File.WriteAllText(Path.Combine(malformedFixture.Root, "locks", "ao.json"), "{");
        var malformedReport = await malformedFixture.ValidateAsync(LoomReleaseSetAuthorityMode.CheckIn);
        Assert.False(malformedReport.IsValid);
        Assert.Contains(malformedReport.Issues, issue => issue.Code == "malformed-package-lock");
    }

    [Fact]
    public async Task NuGetMetadataFailureFailsClosed()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        fixture.MetadataFailure = true;

        var report = await fixture.ValidateAsync(LoomReleaseSetAuthorityMode.CheckIn);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "nuget-metadata-unavailable");
    }

    [Fact]
    public async Task MissingPackageMetadataKeyFailsClosedAsStructuredIssue()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        fixture.PackageVersions.Remove("Techne.Loom.Common");

        var report = await fixture.ValidateAsync(LoomReleaseSetAuthorityMode.CheckIn);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "nuget-metadata-unavailable");
    }

    [Fact]
    public async Task UnsafeDocumentCopyPathFailsClosed()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        fixture.Replace("manifests/ao.json", "assets/ao.md", "../escape.md");

        var report = await fixture.ValidateAsync(LoomReleaseSetAuthorityMode.CheckIn);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "unsafe-path");
    }

    [Fact]
    public async Task IncompleteDocumentCopyFailsClosed()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        fixture.Replace(".agents/skills/ao/assets/ao.md", "# ao guide", "# replaced guide");

        var report = await fixture.ValidateAsync(LoomReleaseSetAuthorityMode.CheckIn);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "document-copy-content");
    }

    [Fact]
    public async Task PrePublishPackageClosureRequiresAllTwentyExactPackages()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        fixture.WritePackageArtifacts("0.3.271");

        var report = await fixture.ValidateAsync(
            LoomReleaseSetAuthorityMode.Release,
            LoomReleaseSetValidationPhase.PrePublishPackageClosure,
            "0.3.271",
            "package-artifacts");

        Assert.True(report.IsValid, report.ToDiagnosticString());
    }

    [Fact]
    public async Task MissingPrePublishPackageArtifactFailsClosed()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        fixture.WritePackageArtifacts("0.3.271");
        File.Delete(Path.Combine(fixture.Root, "package-artifacts", "Techne.Loom.Common.0.3.271.nupkg"));

        var report = await fixture.ValidateAsync(
            LoomReleaseSetAuthorityMode.Release,
            LoomReleaseSetValidationPhase.PrePublishPackageClosure,
            "0.3.271",
            "package-artifacts");

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "missing-package-artifact");
    }



    [Fact]

    public async Task WrongPackageNuspecIdFailsClosed()

    {

        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");

        fixture.WritePackageArtifacts("0.3.271");

        fixture.RewritePackageNuspec("Techne.Loom.Common.0.3.271.nupkg", "Techne.Loom.Wrong", "0.3.271");



        var report = await fixture.ValidateAsync(

            LoomReleaseSetAuthorityMode.Release,

            LoomReleaseSetValidationPhase.PrePublishPackageClosure,

            "0.3.271",

            "package-artifacts");



        Assert.False(report.IsValid);

        Assert.Contains(report.Issues, issue => issue.Code == "package-artifact-identity");

    }



    [Fact]

    public async Task WrongPackageNuspecVersionFailsClosed()

    {

        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");

        fixture.WritePackageArtifacts("0.3.271");

        fixture.RewritePackageNuspec("Techne.Loom.Common.0.3.271.nupkg", "Techne.Loom.Common", "0.3.270");



        var report = await fixture.ValidateAsync(

            LoomReleaseSetAuthorityMode.Release,

            LoomReleaseSetValidationPhase.PrePublishPackageClosure,

            "0.3.271",

            "package-artifacts");



        Assert.False(report.IsValid);

        Assert.Contains(report.Issues, issue => issue.Code == "package-artifact-identity");

    }



    [Fact]
    public async Task InternalPackageDependencyVersionMustMatchCandidate()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        fixture.WritePackageArtifacts("0.3.271");
        fixture.RewritePackageDependencyVersion("Techne.Loom.Common.0.3.271.nupkg", "Techne.Loom.Abstractions", "0.3.270");

        var report = await fixture.ValidateAsync(
            LoomReleaseSetAuthorityMode.Release,
            LoomReleaseSetValidationPhase.PrePublishPackageClosure,
            "0.3.271",
            "package-artifacts");

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "package-artifact-internal-dependency-version");
    }

    [Fact]
    public async Task InternalPackageDependencyRangeFailsClosed()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        fixture.WritePackageArtifacts("0.3.271");
        fixture.RewritePackageDependencyVersion("Techne.Loom.Common.0.3.271.nupkg", "Techne.Loom.Abstractions", "[0.3.271]");

        var report = await fixture.ValidateAsync(
            LoomReleaseSetAuthorityMode.Release,
            LoomReleaseSetValidationPhase.PrePublishPackageClosure,
            "0.3.271",
            "package-artifacts");

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "package-artifact-internal-dependency-version");
    }

    [Fact]
    public async Task RuntimePackageGuideVersionMustMatchCandidate()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        fixture.WritePackageArtifacts("0.3.271");
        fixture.RewritePackageEntry("Techne.Loom.AgentOrchestrator.Runtime.linux-x64.0.3.271.nupkg", "tools/linux-x64/docs/en/guides/ao-guide.md", "# ao\nVersion: 0.3.270\nBuild: published package 0.3.270\n");

        var report = await fixture.ValidateAsync(
            LoomReleaseSetAuthorityMode.Release,
            LoomReleaseSetValidationPhase.PrePublishPackageClosure,
            "0.3.271",
            "package-artifacts");

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "runtime-package-guide-version");
    }

    [Fact]
    public async Task RuntimePackageMetadataIsValidatedFromLocalArtifact()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        fixture.WritePackageArtifacts("0.3.271");
        fixture.RemovePackageEntry("Techne.Loom.AgentOrchestrator.Runtime.linux-x64.0.3.271.nupkg", "tools/linux-x64/runtime.json");

        var report = await fixture.ValidateAsync(
            LoomReleaseSetAuthorityMode.Release,
            LoomReleaseSetValidationPhase.PrePublishPackageClosure,
            "0.3.271",
            "package-artifacts");

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "runtime-package-integrity");
    }

    [Fact]

    public async Task UnexpectedPackageArtifactFailsClosed()

    {

        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");

        fixture.WritePackageArtifacts("0.3.271");

        fixture.WritePackageArtifact("Unexpected.Package.0.3.271.nupkg", "Unexpected.Package", "0.3.271");



        var report = await fixture.ValidateAsync(

            LoomReleaseSetAuthorityMode.Release,

            LoomReleaseSetValidationPhase.PrePublishPackageClosure,

            "0.3.271",

            "package-artifacts");



        Assert.False(report.IsValid);

        Assert.Contains(report.Issues, issue => issue.Code == "unexpected-package-artifact");

    }



    [Fact]

    public async Task MalformedPackageArtifactFailsClosed()

    {

        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");

        fixture.WritePackageArtifacts("0.3.271");

        File.WriteAllText(Path.Combine(fixture.Root, "package-artifacts", "Techne.Loom.Common.0.3.271.nupkg"), "not a zip archive");



        var report = await fixture.ValidateAsync(

            LoomReleaseSetAuthorityMode.Release,

            LoomReleaseSetValidationPhase.PrePublishPackageClosure,

            "0.3.271",

            "package-artifacts");



        Assert.False(report.IsValid);

        Assert.Contains(report.Issues, issue => issue.Code == "invalid-package-artifact");

    }



    [Fact]

    public async Task MismatchedDocumentCopyRuntimePackageFailsClosed()

    {

        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");

        fixture.Replace("manifests/ao.json", "Techne.Loom.AgentOrchestrator.Runtime.linux-x64", "Techne.Loom.SkillOrchestrator.Runtime.linux-x64");



        var report = await fixture.ValidateAsync(LoomReleaseSetAuthorityMode.CheckIn);



        Assert.False(report.IsValid);

        Assert.Contains(report.Issues, issue => issue.Code == "document-copy-package-id");

    }


    [Fact]
    public async Task ReleasePackageClosureCanRunBeforeSurfaceRefresh()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        fixture.WritePackageArtifacts("0.3.271");

        var report = await fixture.ValidateAsync(
            LoomReleaseSetAuthorityMode.Release,
            LoomReleaseSetValidationPhase.PostPublishPackageClosure,
            "0.3.271",
            "package-artifacts");

        Assert.True(report.IsValid, report.ToDiagnosticString());
        Assert.Equal("0.3.271", report.ExpectedVersion);
    }

    [Fact]
    public async Task ReleasePostPublishUsesLocalArtifactsWithoutNuGetConvergence()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.271");
        fixture.WritePackageArtifacts("0.3.271");
        fixture.MetadataFailure = true;

        var report = await fixture.ValidateAsync(
            LoomReleaseSetAuthorityMode.Release,
            LoomReleaseSetValidationPhase.PostPublish,
            "0.3.271",
            "package-artifacts");

        Assert.True(report.IsValid, report.ToDiagnosticString());
        Assert.Equal("0.3.271", report.ExpectedVersion);
        Assert.Empty(report.LatestPackageVersions);
    }

    [Fact]
    public async Task ReleasePrePublishUsesCandidateWithoutRequiringCheckedInSurfacesToAlreadyBeRefreshed()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.270");
        var report = await fixture.ValidateAsync(
            LoomReleaseSetAuthorityMode.Release,
            LoomReleaseSetValidationPhase.PrePublish,
            "0.3.271");

        Assert.True(report.IsValid, report.ToDiagnosticString());
        Assert.Empty(report.LatestPackageVersions);
    }

    [Fact]
    public async Task ReleasePostPublishFailsWhenALocalPackageArtifactIsMissing()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.271");
        fixture.WritePackageArtifacts("0.3.271");
        fixture.MetadataFailure = true;
        File.Delete(Path.Combine(fixture.Root, "package-artifacts", "Techne.Loom.Common.0.3.271.nupkg"));

        var report = await fixture.ValidateAsync(
            LoomReleaseSetAuthorityMode.Release,
            LoomReleaseSetValidationPhase.PostPublish,
            "0.3.271",
            "package-artifacts");

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "missing-package-artifact");
        Assert.Empty(report.LatestPackageVersions);
    }

    [Fact]
    public async Task ReleasePostPublishRejectsInternalDependencyDrift()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.271");
        fixture.WritePackageArtifacts("0.3.271");
        fixture.MetadataFailure = true;
        fixture.RewritePackageDependencyVersion("Techne.Loom.Common.0.3.271.nupkg", "Techne.Loom.Abstractions", "0.3.270");

        var report = await fixture.ValidateAsync(
            LoomReleaseSetAuthorityMode.Release,
            LoomReleaseSetValidationPhase.PostPublish,
            "0.3.271",
            "package-artifacts");

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "package-artifact-internal-dependency-version");
        Assert.Empty(report.LatestPackageVersions);
    }

    [Fact]
    public async Task ReleasePostPublishRejectsRuntimeGuideVersionDrift()
    {
        using var fixture = ReleaseSetFixture.Create("released", "0.3.271");
        fixture.WritePackageArtifacts("0.3.271");
        fixture.MetadataFailure = true;
        fixture.RewritePackageEntry("Techne.Loom.AgentOrchestrator.Runtime.linux-x64.0.3.271.nupkg", "tools/linux-x64/docs/en/guides/ao-guide.md", "# ao\nVersion: 0.3.270\nBuild: published package 0.3.270\n");

        var report = await fixture.ValidateAsync(
            LoomReleaseSetAuthorityMode.Release,
            LoomReleaseSetValidationPhase.PostPublish,
            "0.3.271",
            "package-artifacts");

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "runtime-package-guide-version");
        Assert.Empty(report.LatestPackageVersions);
    }

    private sealed class ReleaseSetFixture : IDisposable
    {
        private readonly string _manifestPath;
        private readonly string _channel;
        public string Root { get; }
        public LoomReleaseSetManifest Manifest { get; }
        public Dictionary<string, string> PackageVersions { get; } = new(StringComparer.Ordinal);
        public bool MetadataFailure { get; set; }

        private ReleaseSetFixture(string root, LoomReleaseSetManifest manifest, string channel, string version)
        {
            Root = root;
            Manifest = manifest;
            _channel = channel;
            _manifestPath = Path.Combine(root, "release-set.json");
            foreach (var packageId in LoomReleaseSetValidator.GetExpectedPackageIds())
            {
                PackageVersions[packageId] = version;
            }

            WriteManifest();
        }

        public static ReleaseSetFixture Create(string channel, string version)
        {
            var root = Path.Combine(Path.GetTempPath(), $"loom-release-set-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var manifest = BuildManifest(channel, root);
            var fixture = new ReleaseSetFixture(root, manifest, channel, version);
            fixture.WriteSurfaceFiles(channel, version);
            return fixture;
        }

        public async Task<LoomReleaseSetValidationReport> ValidateAsync(
            LoomReleaseSetAuthorityMode mode,
            LoomReleaseSetValidationPhase phase = LoomReleaseSetValidationPhase.PostPublish,
            string? candidateVersion = null,
            string? packageRoot = null)
        {
            var source = new FakeMetadataSource(PackageVersions, () => MetadataFailure);
            return await LoomReleaseSetValidator.ValidateAsync(new LoomReleaseSetValidationRequest
            {
                RepositoryRoot = Root,
                Channel = _channel,
                AuthorityMode = mode,
                Phase = phase,
                CandidateVersion = candidateVersion,
                PackageRoot = packageRoot,
                PackageMetadataSource = source,
            });
        }

        public void WritePackageArtifacts(string version)
        {
            foreach (var packageId in LoomReleaseSetValidator.GetExpectedPackageIds())
            {
                WritePackageArtifact(packageId + "." + version + ".nupkg", packageId, version);
            }
        }

        public void WritePackageArtifact(string fileName, string packageId, string version)
        {
            var packageRoot = Path.Combine(Root, "package-artifacts");
            Directory.CreateDirectory(packageRoot);
            var packagePath = Path.Combine(packageRoot, fileName);
            using var fileStream = File.Create(packagePath);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

            if (TryGetRuntimePackageIdentity(packageId, out var runtimeProduct, out var runtimeIdentifier))
            {
                var entryPoint = LoomRuntimeCatalog.GetEntryPoint(runtimeProduct);
                var entryPointFile = LoomRuntimeCatalog.GetEntryFile(runtimeProduct, runtimeIdentifier);
                WriteZipEntry(archive, packageId + ".nuspec", $"<package><metadata><id>{packageId}</id><version>{version}</version><tags>runtime rid:{runtimeIdentifier}</tags></metadata></package>");
                var runtimeManifest = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["schema"] = "techne-loom-runtime-v1",
                    ["product"] = entryPoint,
                    ["package_id"] = packageId,
                    ["version"] = version,
                    ["rid"] = runtimeIdentifier,
                    ["entrypoint"] = entryPointFile,
                    ["docs_root"] = $"tools/{runtimeIdentifier}/docs/en",
                    ["guide_path"] = $"guides/{entryPoint}-guide.md",
                    ["single_file"] = true,
                };
                WriteZipEntry(archive, $"tools/{runtimeIdentifier}/runtime.json", JsonSerializer.Serialize(runtimeManifest));
                WriteZipEntry(archive, $"tools/{runtimeIdentifier}/{entryPointFile}", "single-file-placeholder");
                WriteZipEntry(archive, $"tools/{runtimeIdentifier}/docs/en/guides/{entryPoint}-guide.md", $"# {entryPoint}{Environment.NewLine}Version: {version}{Environment.NewLine}Build: published package {version}{Environment.NewLine}");
                return;
            }

            var dependencyMarkup = packageId switch
            {
                "Techne.Loom.Common" => $"<dependency id=\"Techne.Loom.Abstractions\" version=\"{version}\" />",
                "Techne.Loom.AgentOrchestrator" or "Techne.Loom.SkillOrchestrator" => $"<dependency id=\"Techne.Loom.Abstractions\" version=\"{version}\" /><dependency id=\"Techne.Loom.Common\" version=\"{version}\" />",
                _ => string.Empty,
            };
            var dependencies = string.IsNullOrEmpty(dependencyMarkup)
                ? string.Empty
                : $"<dependencies><group targetFramework=\"net9.0\">{dependencyMarkup}</group></dependencies>";
            WriteZipEntry(archive, packageId + ".nuspec", $"<package><metadata><id>{packageId}</id><version>{version}</version>{dependencies}</metadata></package>");
        }

        public void RewritePackageNuspec(string fileName, string packageId, string version)
        {
            var packagePath = Path.Combine(Root, "package-artifacts", fileName);
            using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Update);
            var existingNuspec = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
            var nuspecName = existingNuspec.Name;
            XDocument document;
            using (var reader = new StreamReader(existingNuspec.Open()))
            {
                document = XDocument.Parse(reader.ReadToEnd());
            }

            var metadata = document.Descendants().Single(element => string.Equals(element.Name.LocalName, "metadata", StringComparison.Ordinal));
            metadata.Elements().Single(element => string.Equals(element.Name.LocalName, "id", StringComparison.Ordinal)).Value = packageId;
            metadata.Elements().Single(element => string.Equals(element.Name.LocalName, "version", StringComparison.Ordinal)).Value = version;
            existingNuspec.Delete();
            using var writer = new StreamWriter(archive.CreateEntry(nuspecName).Open());
            document.Save(writer);
        }

        public void RewritePackageDependencyVersion(string fileName, string dependencyId, string dependencyVersion)
        {
            var packagePath = Path.Combine(Root, "package-artifacts", fileName);
            using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Update);
            var existingNuspec = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
            var nuspecName = existingNuspec.Name;
            XDocument document;
            using (var reader = new StreamReader(existingNuspec.Open()))
            {
                document = XDocument.Parse(reader.ReadToEnd());
            }

            var dependency = document.Descendants().Single(element =>
                string.Equals(element.Name.LocalName, "dependency", StringComparison.Ordinal) &&
                string.Equals(element.Attribute("id")?.Value, dependencyId, StringComparison.Ordinal));
            dependency.SetAttributeValue("version", dependencyVersion);
            existingNuspec.Delete();
            using var writer = new StreamWriter(archive.CreateEntry(nuspecName).Open());
            document.Save(writer);
        }

        public void RewritePackageEntry(string fileName, string entryPath, string content)
        {
            var packagePath = Path.Combine(Root, "package-artifacts", fileName);
            using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Update);
            var existingEntry = archive.GetEntry(entryPath) ?? throw new InvalidOperationException("Package entry was not found: " + entryPath);
            existingEntry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry(entryPath).Open());
            writer.Write(content);
        }

        public void RemovePackageEntry(string fileName, string entryPath)
        {
            var packagePath = Path.Combine(Root, "package-artifacts", fileName);
            using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Update);
            var entry = archive.GetEntry(entryPath) ?? throw new InvalidOperationException("Package entry was not found: " + entryPath);
            entry.Delete();
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
                if (LoomRuntimeCatalog.SupportedRuntimeIdentifiers.Contains(candidateRid, StringComparer.Ordinal))
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

        private static void WriteZipEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        public void Replace(string relativePath, string oldValue, string newValue)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(path, File.ReadAllText(path).Replace(oldValue, newValue, StringComparison.Ordinal));
        }

        public void WriteManifest()
        {
            File.WriteAllText(_manifestPath, JsonSerializer.Serialize(Manifest, new JsonSerializerOptions { WriteIndented = true }));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }

        private void WriteSurfaceFiles(string channel, string version)
        {
            foreach (var path in Manifest.Surfaces!.PackageIndexes.Select(surface => surface.Path!).Distinct(StringComparer.Ordinal))
            {
                var surface = Manifest.Surfaces.PackageIndexes.Single(item => item.Path == path);
                var indexVersion = surface.Channel == channel ? version : surface.Channel == "beta" ? "0.3.258-beta" : "0.3.270";
                Write(path, $"# Package index\n<!-- package-version-block:start -->\nCurrent version `{indexVersion}`\n<!-- package-version-block:end -->\n| install | --version {indexVersion} |\n");
            }

            foreach (var skill in Manifest.Skills!)
            {
                Write(skill.VersionBlock!, $"- Current published {skill.Product!.ToUpperInvariant()} package runtime version: `{version}`.\\n");
                Write(skill.PackageLock!, $"{{\\n  \"resolved_version\": \"{version}\",\\n  \"runtime_restore\": {{\\n    \"never_float_to_latest\": true\\n  }}\\n}}\\n");
                var skillRoot = skill.Root!;
                var sourcePath = $"docs/{skill.Product}-source.md";
                var targetPath = $"assets/{skill.Product}.md";
                Write(sourcePath, $"# {skill.Product} guide\\n");
                var sourceText = File.ReadAllText(Path.Combine(Root, sourcePath)).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd();
                var sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sourceText))).ToLowerInvariant();
                Write(Path.Combine(skillRoot, targetPath).Replace('\\', '/'), $"# {skill.Product} guide\\n");
                var manifestPath = skill.DocumentCopyManifest!;

                var sourcePackageId = skill.Product == "ao"

                    ? "Techne.Loom.AgentOrchestrator.Runtime.linux-x64"

                    : "Techne.Loom.SkillOrchestrator.Runtime.linux-x64";

                var sourcePackagePath = "tools/linux-x64/docs/en/guides/" + Path.GetFileName(sourcePath);

                var manifest = new

                {

                    schema_version = "1",

                    target_skill_root = skill.Root,

                    target_bound_product = skill.Product,

                    target_bound_channel = channel,

                    target_bound_version = version,

                    documents = new[]

                    {

                        new

                        {

                            target_path = targetPath,

                            source_path = sourcePath,

                            source_package_id = sourcePackageId,

                            source_package_rid = "linux-x64",

                            source_package_path = sourcePackagePath,

                            source_product = skill.Product,

                            source_channel = channel,

                            source_version = version,

                            source_sha256 = sourceHash,

                            artifact_origin = "verified-copy"

                        }

                    }

                };

                Write(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);

            }

            foreach (var guide in Manifest.Surfaces.GuideMetadata)
            {
                var directory = Path.GetDirectoryName(guide.Glob!)!.Replace('\\', '/');
                Write($"{directory}/{guide.Product}-guide.md", $"# {guide.Product}\\nVersion: {version}\\nBuild: published package {version}\\n");
            }

            foreach (var workflow in Manifest.Ci!.WorkflowPaths!)
            {
                Write(workflow, "jobs:\\n  version:\\n    outputs:\\n      package_version: value\\n    steps:\\n      - uses: gittools/actions/gitversion/execute@v4\\n  runtime-packages:\\n    needs: [version]\\n    run: needs.version.outputs.package_version\\n  publish:\\n    needs: [version, runtime-packages]\\n    run: needs.version.outputs.package_version\\n");
            }

            foreach (var excluded in Manifest.ExcludedArtifacts!)
            {
                Write(excluded.Path!, "classified historical content");
            }
        }

        private void Write(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, content.Replace("\\n", Environment.NewLine, StringComparison.Ordinal));
        }

        private static LoomReleaseSetManifest BuildManifest(string channel, string root)
        {
            var packageIndexes = new List<LoomReleaseSetPackageIndexSurface>
            {
                new() { Path = "indexes/released.md", Channel = "released" },
                new() { Path = "indexes/beta.md", Channel = "beta" },
            };
            return new LoomReleaseSetManifest
            {
                SchemaVersion = "loom-release-set.v1",
                ReleaseSetId = "fixture",
                VersionAuthority = new LoomReleaseSetVersionAuthority
                {
                    CheckIn = new LoomReleaseSetAuthorityRule { Source = "nuget-flat-container-index", RequiresAllPackagesSameExactVersion = true },
                    Release = new LoomReleaseSetAuthorityRule { Source = "shared-version-job-output", RequiresAllPackagesSameExactVersion = true },
                },
                Channels = new Dictionary<string, LoomReleaseSetChannelRule>(StringComparer.Ordinal)
                {
                    ["released"] = new() { VersionPattern = "^[0-9]+\\.[0-9]+\\.[0-9]+$", PackageIndexPaths = ["indexes/released.md"] },
                    ["beta"] = new() { VersionPattern = "^[0-9]+\\.[0-9]+\\.[0-9]+-beta$", PackageIndexPaths = ["indexes/beta.md"] },
                },
                Packages = new LoomReleaseSetPackageScope
                {
                    Core = [.. new[] { "Techne.Loom.Abstractions", "Techne.Loom.Common", "Techne.Loom.AgentOrchestrator", "Techne.Loom.SkillOrchestrator" }],
                    Runtime = new LoomReleaseSetRuntimeScope
                    {
                        Products = ["ao", "so"],
                        RuntimeIdentifiers = [.. LoomRuntimeCatalog.SupportedRuntimeIdentifiers],
                    },
                },
                Skills =
                [
                    new() { Product = "ao", Root = ".agents/skills/ao", VersionBlock = ".agents/skills/ao/SKILL.md", PackageLock = "locks/ao.json", DocumentCopyManifest = "manifests/ao.json" },
                    new() { Product = "so", Root = ".agents/skills/so", VersionBlock = ".agents/skills/so/SKILL.md", PackageLock = "locks/so.json", DocumentCopyManifest = "manifests/so.json" },
                ],
                Surfaces = new LoomReleaseSetSurfaceSet
                {
                    PackageIndexes = packageIndexes,
                    GuideMetadata =
                    [
                        new() { Glob = "docs/en/guides/ao-guide*.md", Product = "ao" },
                        new() { Glob = "docs/en/guides/so-guide*.md", Product = "so" },
                    ],
                    WorkflowPaths = [".github/workflows/publish.yml"],
                },
                Ci = new LoomReleaseSetCiContract
                {
                    WorkflowPaths = [".github/workflows/publish.yml"],
                    SharedVersionJobId = "version",
                    PackageVersionOutput = "needs.version.outputs.package_version",
                    ForbiddenAutomationPatterns = ["--version latest", "Version=latest", "PackageVersion=latest"],
                },
                ExcludedArtifacts =
                [
                    new() { Path = "audit/old.txt", Classification = "historical", Reason = "Historical fixture." },
                ],
            };
        }
    }

    private sealed class FakeMetadataSource : ILoomReleaseSetPackageMetadataSource
    {
        private readonly IReadOnlyDictionary<string, string> _versions;
        private readonly Func<bool> _shouldFail;

        public FakeMetadataSource(IReadOnlyDictionary<string, string> versions, Func<bool> shouldFail)
        {
            _versions = versions;
            _shouldFail = shouldFail;
        }

        public Task<string> GetLatestVersionAsync(string packageId, string channel, CancellationToken cancellationToken = default)
        {
            if (_shouldFail())
            {
                throw new HttpRequestException("simulated network failure", null, HttpStatusCode.ServiceUnavailable);
            }

            if (!_versions.TryGetValue(packageId, out var version))
            {
                throw new InvalidDataException($"No metadata was configured for package '{packageId}'.");
            }

            return Task.FromResult(version);
        }
    }
}
