using System.Diagnostics;
using Techne.Loom.AgentOrchestrator.Models;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class AoMcpCliFailureTests
{
    [Fact]
    public async Task InvalidMcpArgumentsKeepStdoutFreeOfAoEnvelopes()
    {
        var repoRoot = FindRepositoryRoot();
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = repoRoot,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(typeof(AoResumeEnvelope).Assembly.Location);
        startInfo.ArgumentList.Add("mcp");
        startInfo.ArgumentList.Add("stdio");
        startInfo.ArgumentList.Add("--unsupported");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start AO MCP process.");
        process.StandardInput.Close();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(2, process.ExitCode);
        Assert.Empty(stdout);
        Assert.Contains("AO MCP stdio failed", stderr, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Techne.Loom.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
