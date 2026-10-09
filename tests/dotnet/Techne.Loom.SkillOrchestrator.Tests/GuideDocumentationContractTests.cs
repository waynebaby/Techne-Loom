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

    [Fact]
    public void SkillUsageGuideLinksEverySkillEnhancementAgentAndFallbackRule()
    {
        var repositoryRoot = FindRepositoryRoot();
        var agentRoot = Path.Combine(repositoryRoot, ".agents", "skills", "loom-skill-enhancement", "assets", "agents");
        var agentNames = Directory.EnumerateFiles(agentRoot, "*.agent.md")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(agentNames);

        var agentLinkRoot = "../../../.agents/skills/loom-skill-enhancement/assets/agents";
        var fallbackLinks = new[]
        {
            (
                MarkdownLink: "../../../.agents/skills/loom-skill-enhancement/SKILL.md#named-agent-resolution",
                RelativePath: "../../../.agents/skills/loom-skill-enhancement/SKILL.md",
                Heading: "## Named Agent Resolution"),
            (
                MarkdownLink: "../../../.github/instructions/loom-skill-governance.instructions.md#subagent-authority-rules",
                RelativePath: "../../../.github/instructions/loom-skill-governance.instructions.md",
                Heading: "## Subagent Authority Rules")
        };
        foreach (var language in new[] { "en", "zh-cn" })
        {
            var guidePath = Path.Combine(repositoryRoot, "docs", language, "guides", "skill-usage.md");
            var guide = File.ReadAllText(guidePath);
            foreach (var fallbackLink in fallbackLinks)
            {
                Assert.Contains(fallbackLink.MarkdownLink, guide, StringComparison.Ordinal);

                var targetPath = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(guidePath)!,
                    fallbackLink.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                Assert.True(File.Exists(targetPath), $"Missing fallback-contract target: {targetPath}.");
                Assert.Contains(fallbackLink.Heading, File.ReadAllText(targetPath), StringComparison.Ordinal);
            }

            foreach (var agentName in agentNames)
            {
                Assert.Contains($"[{agentName}]({agentLinkRoot}/{agentName})", guide, StringComparison.Ordinal);
                Assert.Contains($"`assets/agents/{agentName}`", guide, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void AskUserSkillRuntimeVersionTracksPublishedPackageReleases()
    {
        var repositoryRoot = FindRepositoryRoot();
        var skillPath = Path.Combine(repositoryRoot, ".agents", "skills", "loom-ask-user", "SKILL.md");
        var skill = File.ReadAllText(skillPath);
        var versionMatch = Regex.Match(
            skill,
            @"(?m)^- Current published SO package runtime version: `(?<version>\d+\.\d+\.\d+(?:-beta)?)`\.\s*$");

        Assert.True(versionMatch.Success, "The AskUser skill must expose one exact published SO runtime version.");
        Assert.Matches(@"^\d+\.\d+\.\d+(?:-beta)?$", versionMatch.Groups["version"].Value);
        Assert.Single(Regex.Matches(skill, Regex.Escape("<!-- skill-package-version-block:start -->")));
        Assert.Single(Regex.Matches(skill, Regex.Escape("<!-- skill-package-version-block:end -->")));
        Assert.Contains("use only a version verified to include AskUser Web UI support.", skill, StringComparison.Ordinal);
        Assert.Contains("regardless of which agent is active.", skill, StringComparison.Ordinal);
        Assert.Contains("one ordered form and collect one submission.", skill, StringComparison.Ordinal);

        var englishGuidePath = Path.Combine(repositoryRoot, "docs", "en", "guides", "ask-user-guide.md");
        var chineseGuidePath = Path.Combine(repositoryRoot, "docs", "zh-cn", "guides", "ask-user-guide.md");
        var englishGuide = File.ReadAllText(englishGuidePath);
        var chineseGuide = File.ReadAllText(chineseGuidePath);
        Assert.Contains("Default across agents", englishGuide, StringComparison.Ordinal);
        Assert.Contains("one ordered form and collect one submission", englishGuide, StringComparison.Ordinal);
        Assert.Contains("跨 agent 默认优先级", chineseGuide, StringComparison.Ordinal);
        Assert.Contains("一次提交", chineseGuide, StringComparison.Ordinal);

        var englishSkillUsagePath = Path.Combine(repositoryRoot, "docs", "en", "guides", "skill-usage.md");
        var chineseSkillUsagePath = Path.Combine(repositoryRoot, "docs", "zh-cn", "guides", "skill-usage.md");
        var englishSkillUsage = File.ReadAllText(englishSkillUsagePath);
        var chineseSkillUsage = File.ReadAllText(chineseSkillUsagePath);
        Assert.Contains("Prefer one shared AskUser form", englishSkillUsage, StringComparison.Ordinal);
        Assert.Contains("跨 agent 的业务输入优先", chineseSkillUsage, StringComparison.Ordinal);

        foreach (var workflowName in new[] { "publish-development.yml", "publish-main.yml" })
        {
            var workflowPath = Path.Combine(repositoryRoot, ".github", "workflows", workflowName);
            var workflow = File.ReadAllText(workflowPath);

            Assert.Contains("Path(\".agents/skills/loom-ask-user/SKILL.md\")", workflow, StringComparison.Ordinal);
            Assert.Contains("Current published SO package runtime version: `{version}`.", workflow, StringComparison.Ordinal);
            Assert.Contains("published package that includes AskUser Web UI support.", workflow, StringComparison.Ordinal);
            var stagingStart = workflow.IndexOf("git add README.md", StringComparison.Ordinal);
            Assert.True(stagingStart >= 0, "The publish workflow must stage release refreshes.");
            Assert.Contains(".agents/skills/loom-ask-user/SKILL.md", workflow[stagingStart..], StringComparison.Ordinal);
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
