namespace Techne.Loom.AgentOrchestrator.Cli;

internal static class AoGuideContentService
{
    public static async Task<string> LoadGuideAsync(string lang, string? section)
    {
        var guidePath = ResolveGuidePath(lang);
        var content = await File.ReadAllTextAsync(guidePath).ConfigureAwait(false);
        return FilterSection(content, section);
    }

    private static string ResolveGuidePath(string lang)
    {
        var langFolder = lang == "zh-cn" ? "zh-cn" : "en";
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "guide-assets", langFolder, "ao-guide.md");
        if (File.Exists(bundledPath))
        {
            return bundledPath;
        }

        return Path.Combine(FindRepositoryRoot(), "docs", langFolder, "reference", "products", "ao-guide.md");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "README.md")) && Directory.Exists(Path.Combine(current.FullName, "docs")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private static string FilterSection(string content, string? section)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return content;
        }

        var lines = content.Split('\n');
        var header = "## " + section.Trim();
        var start = Array.FindIndex(lines, line => string.Equals(line.Trim(), header, StringComparison.OrdinalIgnoreCase));
        if (start < 0)
        {
            return content;
        }

        var end = Array.FindIndex(lines, start + 1, line => line.StartsWith("## ", StringComparison.Ordinal));
        end = end < 0 ? lines.Length : end;
        return string.Join('\n', lines[start..end]);
    }
}
