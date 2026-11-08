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
