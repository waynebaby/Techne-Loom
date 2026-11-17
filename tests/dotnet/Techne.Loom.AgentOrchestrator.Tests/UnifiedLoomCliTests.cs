using System.Text;
using Xunit;

[CollectionDefinition("Unified Loom CLI", DisableParallelization = true)]
public sealed class UnifiedLoomCliCollection
{
}

[Collection("Unified Loom CLI")]
public sealed class UnifiedLoomCliTests
{
    [Theory]
    [InlineData("agent")]
    [InlineData("skill")]
    public async Task ModuleHelpOmitsTopLevelGuideAndAskCommands(string module)
    {
        var result = await RunCliAsync(module, "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"Usage: loom {module} --help", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("--guide", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Standalone workflow-independent ask:", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("ask start --contract-file", result.StandardOutput, StringComparison.Ordinal);

        var nestedAsk = await RunCliAsync(module, "ask", "start", "--contract-file", "unused.json");
        Assert.Equal(2, nestedAsk.ExitCode);
        Assert.Contains("Standalone questions are available only through the top-level 'loom ask' command.", nestedAsk.StandardError, StringComparison.Ordinal);

        var subcommandHelp = await RunCliAsync(module, "compile", "--help");

        Assert.Equal(0, subcommandHelp.ExitCode);
        Assert.Contains($"Usage: loom {module} --help", subcommandHelp.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Usage: ao", subcommandHelp.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Usage: so", subcommandHelp.StandardOutput, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("agent")]
    [InlineData("skill")]
    public async Task ModuleGuideCommandDirectsUsersToTopLevelGuide(string module)
    {
        var result = await RunCliAsync(module, "--guide");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Use the unified top-level 'loom --guide' command.", result.StandardError, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ask")]
    [InlineData("ask --help")]
    [InlineData("ask start --help")]
    public async Task AskHelpUsesOnlyTheUnifiedEntryPoint(string command)
    {
        var result = await RunCliAsync(command.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: loom ask <start|result|wait>", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Usage: ao", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Standalone workflow-independent ask:", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownAskCommandReportsUnifiedUsage()
    {
        var result = await RunCliAsync("ask", "unknown");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown ask command 'unknown'.", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("Usage: loom ask", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProgressHelpUsesUnifiedEntryPoint()
    {
        var result = await RunCliAsync("progress", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: loom progress append", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--entry-file <path> --output-root <path>", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProgressAppendPreservesPriorBytesAndReturnsEncodedFileUrl()
    {
        var temporaryRoot = Directory.CreateTempSubdirectory("loom-progress-report-");
        try
        {
            var outputRoot = Path.Combine(temporaryRoot.FullName, "Folder With Spaces");
            Directory.CreateDirectory(outputRoot);
            var entryPath = Path.Combine(temporaryRoot.FullName, "entry.md");
            var targetPath = Path.Combine(outputRoot, "progress-report.md");
            const string entry = "## Update\nDetails.\n";
            await File.WriteAllTextAsync(entryPath, entry, new UTF8Encoding(false));
            await File.WriteAllTextAsync(targetPath, "Previous bytes.", new UTF8Encoding(false));

            var result = await RunCliAsync("progress", "append", "--entry-file", entryPath, "--output-root", outputRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("Previous bytes.\n" + entry, await File.ReadAllTextAsync(targetPath));
            Assert.Equal(new Uri(targetPath).AbsoluteUri, result.StandardOutput.Trim());
            Assert.Contains("Folder%20With%20Spaces", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            temporaryRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ProgressAppendRejectsInvalidEntryWithoutChangingTarget()
    {
        var temporaryRoot = Directory.CreateTempSubdirectory("loom-progress-report-");
        try
        {
            var entryPath = Path.Combine(temporaryRoot.FullName, "entry.md");
            var targetPath = Path.Combine(temporaryRoot.FullName, "progress-report.md");
            await File.WriteAllTextAsync(entryPath, "entry without line feed", new UTF8Encoding(false));
            await File.WriteAllTextAsync(targetPath, "Previous bytes.", new UTF8Encoding(false));

            var result = await RunCliAsync("progress", "append", "--entry-file", entryPath, "--output-root", temporaryRoot.FullName);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("line feed", result.StandardError, StringComparison.Ordinal);
            Assert.Equal("Previous bytes.", await File.ReadAllTextAsync(targetPath));
        }
        finally
        {
            temporaryRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ProgressAppendDoesNotCreateOutputRoot()
    {
        var temporaryRoot = Directory.CreateTempSubdirectory("loom-progress-report-");
        try
        {
            var entryPath = Path.Combine(temporaryRoot.FullName, "entry.md");
            var outputRoot = Path.Combine(temporaryRoot.FullName, "missing");
            await File.WriteAllTextAsync(entryPath, "entry\n", new UTF8Encoding(false));

            var result = await RunCliAsync("progress", "append", "--entry-file", entryPath, "--output-root", outputRoot);

            Assert.Equal(1, result.ExitCode);
            Assert.False(Directory.Exists(outputRoot));
        }
        finally
        {
            temporaryRoot.Delete(recursive: true);
        }
    }

    private static async Task<CliResult> RunCliAsync(params string[] args)
    {
        var originalOutput = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            var exitCode = await global::LoomCli.RunAsync(args);
            return new CliResult(exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }
    }

    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);
}
