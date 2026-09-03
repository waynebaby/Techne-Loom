using System.Text.RegularExpressions;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class RoslynCapabilityDocumentationTests
{
    [Fact]
    public void EveryCapabilityIsDocumentedInAllProductAndLanguageChapters()
    {
        var root = FindRepositoryRoot();
        var documents = new[]
        {
            Path.Combine(root, "docs", "en", "guides", "ao-guide-reference-tools.md"),
            Path.Combine(root, "docs", "en", "guides", "so-guide-reference-tools.md"),
            Path.Combine(root, "docs", "zh-cn", "guides", "ao-guide-reference-tools.md"),
            Path.Combine(root, "docs", "zh-cn", "guides", "so-guide-reference-tools.md"),
        };
        var contents = documents.Select(path => (Path: path, Content: File.ReadAllText(path))).ToArray();

        foreach (var entry in RoslynCapabilityCatalog.Entries)
        {
            foreach (var document in contents)
            {
                Assert.Contains(entry.Id, document.Content);
                Assert.Contains(entry.DocumentationId, document.Content);
            }
        }

        Assert.Contains("../../zh-cn/guides/ao-guide-reference-tools.md", contents[0].Content);
        Assert.Contains("../../zh-cn/guides/so-guide-reference-tools.md", contents[1].Content);
        Assert.Contains("../../en/guides/ao-guide-reference-tools.md", contents[2].Content);
        Assert.Contains("../../en/guides/so-guide-reference-tools.md", contents[3].Content);
    }

    [Fact]
    public void LocalMarkdownLinksInToolChaptersResolve()
    {
        var root = FindRepositoryRoot();
        var documents = new[]
        {
            Path.Combine(root, "docs", "en", "guides", "ao-guide-reference-tools.md"),
            Path.Combine(root, "docs", "en", "guides", "so-guide-reference-tools.md"),
            Path.Combine(root, "docs", "zh-cn", "guides", "ao-guide-reference-tools.md"),
            Path.Combine(root, "docs", "zh-cn", "guides", "so-guide-reference-tools.md"),
        };

        foreach (var document in documents)
        {
            var directory = Path.GetDirectoryName(document)!;
            var content = File.ReadAllText(document);
            foreach (Match match in Regex.Matches(content, @"\[[^\]]+\]\(([^)]+)\)"))
            {
                var target = match.Groups[1].Value.Split('#', 2)[0];
                if (string.IsNullOrWhiteSpace(target)
                    || target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var path = Path.GetFullPath(Path.Combine(directory, target.Replace('/', Path.DirectorySeparatorChar)));
                Assert.True(File.Exists(path), $"{document} contains a missing local link: {target}");
            }
        }
    }

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
}
