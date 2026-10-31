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
            @"(?m)^- Current published AO/SO runtime release version: `(?<version>\d+\.\d+\.\d+(?:-beta)?)`\.\s*$");

        Assert.True(versionMatch.Success, "The AskUser skill must expose one exact published AO/SO release-set version.");
        Assert.Matches(@"^\d+\.\d+\.\d+(?:-beta)?$", versionMatch.Groups["version"].Value);
        Assert.Single(Regex.Matches(skill, Regex.Escape("<!-- skill-package-version-block:start -->")));
        Assert.Single(Regex.Matches(skill, Regex.Escape("<!-- skill-package-version-block:end -->")));
        Assert.Contains("This is the exact shared AO/SO release-set version; it is not a feature-availability claim.", skill, StringComparison.Ordinal);
        Assert.Contains("The skill and the node consume the same infrastructure independently.", skill, StringComparison.Ordinal);
        Assert.Contains("one ordered form and collect one submission", skill, StringComparison.Ordinal);

        var englishGuidePath = Path.Combine(repositoryRoot, "docs", "en", "guides", "ask-user-guide.md");
        var chineseGuidePath = Path.Combine(repositoryRoot, "docs", "zh-cn", "guides", "ask-user-guide.md");
        var englishGuide = File.ReadAllText(englishGuidePath);
        var chineseGuide = File.ReadAllText(chineseGuidePath);
        Assert.Contains("The skill and node are independent consumers of XO Ask.", englishGuide, StringComparison.Ordinal);
        Assert.Contains("After one submission", englishGuide, StringComparison.Ordinal);
        Assert.Contains("ao ask start --contract-file <path>", englishGuide, StringComparison.Ordinal);
        Assert.Contains("so ask start --contract-file <path>", englishGuide, StringComparison.Ordinal);
        Assert.Contains("ao ask result --ask-id <id>", englishGuide, StringComparison.Ordinal);
        Assert.Contains("so ask result --ask-id <id>", englishGuide, StringComparison.Ordinal);
        Assert.Contains("ao ask start --contract-file <path>", chineseGuide, StringComparison.Ordinal);
        Assert.Contains("so ask start --contract-file <path>", chineseGuide, StringComparison.Ordinal);
        Assert.Contains("ao ask result --ask-id <id>", chineseGuide, StringComparison.Ordinal);
        Assert.Contains("so ask result --ask-id <id>", chineseGuide, StringComparison.Ordinal);
        Assert.Contains("Standalone `ask start --contract-file <path>` and `ask result --ask-id <id>` support is version-specific.", englishGuide, StringComparison.Ordinal);
        Assert.Contains("Before use, confirm the exact selected published AO or SO apphost's fresh help exposes both commands.", englishGuide, StringComparison.Ordinal);
        Assert.Contains("Skill 和 node 是 XO Ask 的两个独立消费者", chineseGuide, StringComparison.Ordinal);
        Assert.Contains("用户一次提交后", chineseGuide, StringComparison.Ordinal);
        Assert.Contains("Standalone `ask start --contract-file <path>` 与 `ask result --ask-id <id>` 的支持取决于 runtime 版本。", chineseGuide, StringComparison.Ordinal);
        Assert.Contains("使用前必须检查精确所选的已发布 AO 或 SO apphost 的 fresh help", chineseGuide, StringComparison.Ordinal);

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
            Assert.Contains("Current published AO/SO runtime release version: `{version}`.", workflow, StringComparison.Ordinal);
            Assert.Contains("exact shared AO/SO release-set version; it is not a feature-availability claim.", workflow, StringComparison.Ordinal);
            var stagingStart = workflow.IndexOf("git add README.md", StringComparison.Ordinal);
            Assert.True(stagingStart >= 0, "The publish workflow must stage release refreshes.");
            Assert.Contains(".agents/skills/loom-ask-user/SKILL.md", workflow[stagingStart..], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AgentQuestionsAndDependencyAcquisitionFollowSharedPolicies()
    {
        var repositoryRoot = FindRepositoryRoot();
        string Read(string relativePath) => File.ReadAllText(Path.Combine(
            repositoryRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

        var askSkill = Read(".agents/skills/loom-ask-user/SKILL.md");
        var askRuntime = Read(".agents/skills/loom-ask-user/reference/runtime-dependency.md");
        var planSkill = Read(".agents/skills/loom-plan-execution/SKILL.md");
        var planAuthority = Read(".agents/skills/loom-plan-execution/assets/agents/workflow-designer-01-authority.md");
        var planValidation = Read(".agents/skills/loom-plan-execution/assets/agents/workflow-designer-05-validation.md");
        var planContract = Read(".agents/skills/loom-plan-execution/contract.json");
        using var planContractDocument = System.Text.Json.JsonDocument.Parse(planContract);
        var contractFields = planContractDocument.RootElement
            .GetProperty("context_fields")
            .GetProperty("fields")
            .EnumerateArray()
            .ToArray();
        foreach (var supportHashPath in new[] { "plan.reference_pack.manifest_sha256", "plan.workflow.candidate_sha256" })
        {
            var supportHashField = contractFields.Single(field =>
                string.Equals(field.GetProperty("path").GetString(), supportHashPath, StringComparison.Ordinal));
            Assert.True(supportHashField.GetProperty("nullable").GetBoolean());
            var description = supportHashField.GetProperty("description").GetString();
            Assert.NotNull(description);
            Assert.Contains("optional", description!, StringComparison.OrdinalIgnoreCase);
        }
        var englishAskGuide = Read("docs/en/guides/ask-user-guide.md");
        var chineseAskGuide = Read("docs/zh-cn/guides/ask-user-guide.md");
        Assert.Contains("class MANAGED,CLI runtime;", englishAskGuide, StringComparison.Ordinal);
        Assert.Contains("class MANAGED,CLI runtime;", chineseAskGuide, StringComparison.Ordinal);
        var englishUsage = Read("docs/en/guides/skill-usage.md");
        var chineseUsage = Read("docs/zh-cn/guides/skill-usage.md");

        foreach (var tier in Enumerable.Range(0, 7).Select(static value => $"L{value}"))
        {
            Assert.Contains(tier, askRuntime, StringComparison.Ordinal);
            Assert.Contains(tier, planSkill, StringComparison.Ordinal);
            Assert.Contains(tier, englishAskGuide, StringComparison.Ordinal);
            Assert.Contains(tier, chineseAskGuide, StringComparison.Ordinal);
        }

        foreach (var cli in new[] { "Invoke-WebRequest", "Invoke-RestMethod", "curl", "gh", "wget", "BITS" })
        {
            Assert.Contains(cli, askRuntime, StringComparison.Ordinal);
            Assert.Contains(cli, planSkill, StringComparison.Ordinal);
            Assert.Contains(cli, englishAskGuide, StringComparison.Ordinal);
            Assert.Contains(cli, chineseAskGuide, StringComparison.Ordinal);
        }

        Assert.Contains("Use `/loom-ask-user` by default for agent-facing clarification", askSkill, StringComparison.Ordinal);
        Assert.Contains("Use `/loom-ask-user` by default for structured clarification", planSkill, StringComparison.Ordinal);
        Assert.Contains("Use `/loom-ask-user` by default", englishUsage, StringComparison.Ordinal);
        Assert.Contains("默认使用 `/loom-ask-user`", chineseUsage, StringComparison.Ordinal);
        var runtimePolicy = Read(".github/instructions/loom-runtime.instructions.md");
        var enhancementSkill = Read(".agents/skills/loom-skill-enhancement/SKILL.md");
        var enhancementExecution = Read(".agents/skills/loom-skill-enhancement/reference/execution-contract.md");
        var enhancementTemplateText = Read(".agents/skills/loom-skill-enhancement/assets/so-workflow/so-template.json");
        Assert.Contains("After any failed request, do not automatically switch transports to repeat the same artifact/source.", askRuntime, StringComparison.Ordinal);
        Assert.Contains("A nonblocked source may be retried only as an explicit L6 operation after L0-L5 are exhausted", askRuntime, StringComparison.Ordinal);
        Assert.Contains("A nonblocked source may be retried only as an explicit L6 operation after L0-L5 are exhausted", runtimePolicy, StringComparison.Ordinal);
        Assert.Contains("A nonblocked source may be retried only as an explicit L6 operation after L0-L5 are exhausted", enhancementSkill, StringComparison.Ordinal);
        Assert.Contains("A nonblocked source may be retried only as an explicit L6 operation after L0-L5 are exhausted", englishAskGuide, StringComparison.Ordinal);
        Assert.Contains("请求失败后，不要自动切换传输方式重试同一个 artifact/source。", chineseAskGuide, StringComparison.Ordinal);
        Assert.Contains("未被安全策略阻止的来源，只有在 L0-L5 已用尽并且用户明确批准这次精确的 L6 CLI 操作后，才可重试。", chineseAskGuide, StringComparison.Ordinal);
        Assert.Contains("A direct in-process .NET `HttpClient` call from PowerShell is L3", runtimePolicy, StringComparison.Ordinal);
        Assert.Contains("A direct in-process .NET `HttpClient` call from PowerShell is L3", enhancementSkill, StringComparison.Ordinal);
        Assert.Contains("A direct in-process .NET `HttpClient` call from PowerShell is L3", enhancementExecution, StringComparison.Ordinal);
        Assert.Contains("PowerShell `Invoke-WebRequest`/`Invoke-RestMethod` remain L6", enhancementSkill, StringComparison.Ordinal);
        Assert.Contains("PowerShell `Invoke-WebRequest`/`Invoke-RestMethod` remain L6", englishAskGuide, StringComparison.Ordinal);
        Assert.Contains("do not calculate, compare, re-attest, freshness-check, or gate on support hashes", planSkill, StringComparison.Ordinal);
        Assert.Contains("optional development-support metadata", planAuthority, StringComparison.Ordinal);
        Assert.Contains("consumers must not require, calculate, compare, re-attest, freshness-check, or gate on them", planValidation, StringComparison.Ordinal);
        Assert.Contains("Exact package SHA-512 and every artifact hash explicitly required by the target business contract remain mandatory.", planSkill, StringComparison.Ordinal);
        Assert.Contains("catalogEntry.packageHash", askRuntime, StringComparison.Ordinal);
        Assert.Contains("L6 generic CLI operations", planContract, StringComparison.Ordinal);
        Assert.Contains("\"path\": \"plan.reference_pack.manifest_sha256\"", planContract, StringComparison.Ordinal);
        Assert.Contains("Optional generated development-support metadata for the reference manifest", planContract, StringComparison.Ordinal);
        using var enhancementTemplateDocument = System.Text.Json.JsonDocument.Parse(enhancementTemplateText);
        var failureGuidanceGates = enhancementTemplateDocument.RootElement.GetProperty("validation").GetProperty("gates");
        foreach (var gate in failureGuidanceGates.EnumerateObject())
        {
            if (!gate.Value.TryGetProperty("failureGuidance", out var guidance))
            {
                continue;
            }

            foreach (var fieldName in new[] { "summary", "nextAction" })
            {
                var message = guidance.GetProperty(fieldName).GetString();
                Assert.NotNull(message);
                Assert.DoesNotMatch(@"[\u4e00-\u9fff]", message!);
            }
        }
    }

    [Fact]
    public void SkillEnhancementFailureGuidanceEvidenceReferencesAreAccurateAndSafe()
    {
        var repositoryRoot = FindRepositoryRoot();
        var skillRoot = Path.Combine(repositoryRoot, ".agents", "skills", "loom-skill-enhancement");
        var templatePath = Path.Combine(skillRoot, "assets", "so-workflow", "so-template.json");
        using var template = System.Text.Json.JsonDocument.Parse(File.ReadAllText(templatePath));
        var gates = template.RootElement.GetProperty("validation").GetProperty("gates");
        var referenceCount = 0;

        foreach (var gate in gates.EnumerateObject())
        {
            if (!gate.Value.TryGetProperty("failureGuidance", out var guidance))
            {
                continue;
            }

            Assert.True(
                guidance.TryGetProperty("evidenceReferences", out var evidenceReferences),
                $"Gate '{gate.Name}' has no failure-guidance evidence references.");
            var references = evidenceReferences.EnumerateArray().ToArray();
            Assert.NotEmpty(references);

            foreach (var reference in references)
            {
                var relativePath = reference.GetProperty("path").GetString();
                Assert.False(string.IsNullOrWhiteSpace(relativePath));
                Assert.False(Path.IsPathRooted(relativePath!));

                var evidencePath = Path.GetFullPath(Path.Combine(
                    skillRoot,
                    relativePath!.Replace('/', Path.DirectorySeparatorChar)));
                var relativeToSkillRoot = Path.GetRelativePath(skillRoot, evidencePath);
                Assert.False(
                    relativeToSkillRoot == ".." ||
                    relativeToSkillRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal),
                    $"Evidence path '{relativePath}' escapes the skill root.");
                Assert.True(File.Exists(evidencePath), $"Evidence path '{relativePath}' does not exist.");

                var lines = File.ReadAllText(evidencePath)
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace('\r', '\n')
                    .Split('\n');
                var startLine = reference.GetProperty("startLine").GetInt32();
                var endLine = reference.GetProperty("endLine").GetInt32();
                Assert.InRange(startLine, 1, lines.Length);
                Assert.InRange(endLine, startLine, lines.Length);

                var quote = reference.GetProperty("quote").GetString();
                Assert.False(string.IsNullOrWhiteSpace(quote));
                var citedLines = string.Join("\n", lines.Skip(startLine - 1).Take(endLine - startLine + 1));
                Assert.Contains(quote!, citedLines, StringComparison.Ordinal);
                referenceCount++;
            }
        }

        Assert.True(referenceCount > 0, "The SO template must contain at least one failure-guidance evidence reference.");
    }

    [Theory]
    [InlineData("ao", "en")]
    [InlineData("so", "en")]
    [InlineData("ao", "zh-cn")]
    [InlineData("so", "zh-cn")]
    public void ProductCliUsageIsCanonicalAndIncludesIndependentAskUser(string product, string language)
    {
        var repositoryRoot = FindRepositoryRoot();
        var guidesRoot = Path.Combine(repositoryRoot, "docs", language, "guides");
        var fileName = $"{product}-cli-usage.md";
        var usage = File.ReadAllText(Path.Combine(guidesRoot, fileName));
        var counterpart = language == "en" ? "zh-cn" : "en";
        var otherProduct = product == "ao" ? "so" : "ao";

        Assert.False(File.Exists(Path.Combine(guidesRoot, "cli-usage.md")));
        Assert.False(File.Exists(Path.Combine(repositoryRoot, "docs", language, "reference", "cli.md")));
        Assert.Contains($"../../{counterpart}/guides/{fileName}", usage, StringComparison.Ordinal);
        Assert.Contains($"{otherProduct}-cli-usage.md", usage, StringComparison.Ordinal);
        Assert.DoesNotContain($".\\{otherProduct}.exe", usage, StringComparison.Ordinal);
        foreach (var token in new[]
        {
            "ask start", "ask result", "/loom-ask-user", "--contract-file", "--result-file",
            "--ask-id", "--offline-submission-file", "schemaVersion", "expectedGeneration",
            "attachmentIds", "--guide", "--patch", "--schema-demo-output", "wait_id", "result_id"
        })
        {
            Assert.Contains(token, usage, StringComparison.Ordinal);
        }

        var guide = File.ReadAllText(Path.Combine(guidesRoot, $"{product}-guide.md"));
        Assert.Contains($"]({fileName})", guide, StringComparison.Ordinal);
        var reference = File.ReadAllText(Path.Combine(repositoryRoot, "docs", language, "reference", "README.md"));
        Assert.Contains($"](../guides/{fileName})", reference, StringComparison.Ordinal);

        if (product == "ao")
        {
            var resumeRow = usage.Split('\n').Single(line => line.StartsWith("| `resume`", StringComparison.Ordinal));
            Assert.DoesNotContain("--audit-output", resumeRow, StringComparison.Ordinal);
            Assert.DoesNotContain("--workspace-root", resumeRow, StringComparison.Ordinal);
            Assert.Contains("| `status`", usage, StringComparison.Ordinal);
        }

        if (language == "en")
        {
            var shippedRoot = Path.Combine(AppContext.BaseDirectory, "docs", "en");
            var shippedUsage = File.ReadAllText(Path.Combine(shippedRoot, "guides", fileName));
            Assert.Contains("expectedGeneration", shippedUsage, StringComparison.Ordinal);
            Assert.DoesNotContain("../../zh-cn/", shippedUsage, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(shippedRoot, "guides", "cli-usage.md")));
            Assert.False(File.Exists(Path.Combine(shippedRoot, "reference", "cli.md")));
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
        Assert.Single(startIndexes);
        Assert.Single(endIndexes);
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
