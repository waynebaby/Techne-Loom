using System.Text.RegularExpressions;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class GuideDocumentationContractTests
{
    private const string VersionStartMarker = "<!-- guide-version:start -->";
    private const string VersionEndMarker = "<!-- guide-version:end -->";

    [Fact]
    public void PairedGuidesHaveExactlyOneVersionMarkerAndMatchingVersion()
    {
        foreach (var pair in EnumerateGuidePairs())
        {
            var english = ReadGuideHeader(pair.EnglishPath, isEnglish: true);
            var chinese = ReadGuideHeader(pair.ChinesePath, isEnglish: false);

            Assert.Equal(english.Version, chinese.Version);
            Assert.Contains(english.Version, english.Build);
            Assert.Contains(chinese.Version, chinese.Build);
        }
    }

    [Fact]
    public void PairedGuidesHaveOneCanonicalReciprocalHeader()
    {
        foreach (var pair in EnumerateGuidePairs())
        {
            var english = ReadGuideHeader(pair.EnglishPath, isEnglish: true);
            var chinese = ReadGuideHeader(pair.ChinesePath, isEnglish: false);

            Assert.Equal(1, CountOccurrences(english.NavigationLine, $"../../zh-cn/guides/{pair.FileName}"));
            Assert.Equal(1, CountOccurrences(chinese.NavigationLine, $"../../en/guides/{pair.FileName}"));
            Assert.DoesNotContain(english.NavigationLine, "guide-version", StringComparison.Ordinal);
            Assert.DoesNotContain(chinese.NavigationLine, "guide-version", StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RuntimePackageLockGuideExamplesMatchExactRidPackageContract()
    {
        var repositoryRoot = FindRepositoryRoot();
        foreach (var language in new[] { "en", "zh-cn" })
        {
            var guidePath = Path.Combine(repositoryRoot, "docs", language, "guides", "so-guide-reference-examples.md");
            var guide = File.ReadAllText(guidePath);

            Assert.Contains("\"source\": \"nuget-registration\"", guide, StringComparison.Ordinal);
            Assert.Contains("\"fallback_source\": \"same-version-github-release-asset\"", guide, StringComparison.Ordinal);
            Assert.Contains("registration_sha512_or_github_sidecar_matches", guide, StringComparison.Ordinal);
            Assert.Contains("runtime_manifest_matches", guide, StringComparison.Ordinal);
            Assert.Contains("archive_paths_and_sizes_are_safe", guide, StringComparison.Ordinal);
            Assert.Contains("apphost_and_english_guide_are_present", guide, StringComparison.Ordinal);
            Assert.DoesNotContain("complete_dotnet_cli_runtime_bundle", guide, StringComparison.Ordinal);
            Assert.DoesNotContain("required_bundle_validation", guide, StringComparison.Ordinal);
            Assert.DoesNotContain("reuse_exact_local_bundle_when_valid", guide, StringComparison.Ordinal);
        }
    }

    private static IEnumerable<(string FileName, string EnglishPath, string ChinesePath)> EnumerateGuidePairs()
    {
        var root = FindRepositoryRoot();
        var englishRoot = Path.Combine(root, "docs", "en", "guides");
        var chineseRoot = Path.Combine(root, "docs", "zh-cn", "guides");
        var englishFiles = Directory.EnumerateFiles(englishRoot, "*-guide*.md")
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal);

        var pairs = englishFiles.Select(path =>
        {
            var fileName = Path.GetFileName(path);
            var chinesePath = Path.Combine(chineseRoot, fileName);
            Assert.True(File.Exists(chinesePath), $"Missing Chinese guide pair for {fileName}.");
            return (FileName: fileName, EnglishPath: path, ChinesePath: chinesePath);
        }).ToArray();

        Assert.NotEmpty(pairs);
        return pairs;
    }

    private static GuideHeader ReadGuideHeader(string path, bool isEnglish)
    {
        var lines = File.ReadAllLines(path);
        var titleIndex = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));
        Assert.Equal(0, titleIndex);
        Assert.StartsWith("# ", lines[titleIndex], StringComparison.Ordinal);

        var startIndexes = Enumerable.Range(0, lines.Length).Where(index => lines[index] == VersionStartMarker).ToArray();
        var endIndexes = Enumerable.Range(0, lines.Length).Where(index => lines[index] == VersionEndMarker).ToArray();
        Assert.Equal(1, startIndexes.Length);
        Assert.Equal(1, endIndexes.Length);
        Assert.True(endIndexes[0] > startIndexes[0]);

        var navigationLines = lines[(titleIndex + 1)..startIndexes[0]]
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        Assert.Single(navigationLines);

        var versionPattern = isEnglish ? @"^Version:\s*(?<version>\S+)\s*$" : @"^版本：\s*(?<version>\S+)\s*$";
        var buildPattern = isEnglish ? @"^Build:\s*(?<build>.+)$" : @"^构建：\s*(?<build>.+)$";
        var versionLine = lines[startIndexes[0] + 1];
        var buildLine = lines[startIndexes[0] + 2];
        var versionMatch = Regex.Match(versionLine, versionPattern);
        var buildMatch = Regex.Match(buildLine, buildPattern);
        Assert.True(versionMatch.Success, $"Invalid version marker in {path}.");
        Assert.True(buildMatch.Success, $"Invalid build marker in {path}.");

        return new GuideHeader(navigationLines[0], versionMatch.Groups["version"].Value, buildMatch.Groups["build"].Value);
    }

    private static int CountOccurrences(string text, string value)
        => Regex.Matches(text, Regex.Escape(value)).Count;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root could not be located from the test output directory.");
    }

    private sealed record GuideHeader(string NavigationLine, string Version, string Build);
}
