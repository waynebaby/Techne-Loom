using System.Diagnostics;
using System.Text.Json;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class SoMcpStdioCliTests
{
    [Fact]
    public async Task Cli_McpStdio_ExposesOnlySoWorkflowTools()
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
        startInfo.ArgumentList.Add(typeof(DefaultWorkflowTaskTrackingService).Assembly.Location);
        startInfo.ArgumentList.Add("mcp");
        startInfo.ArgumentList.Add("stdio");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start SO MCP process.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test-client\",\"version\":\"1.0.0\"}}}");
        await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
        await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}");
        process.StandardInput.Close();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        Assert.Equal(0, process.ExitCode);
        Assert.DoesNotContain("<so_property>", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("error", stderr, StringComparison.OrdinalIgnoreCase);
        var lines = stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var initialize = JsonDocument.Parse(lines[0]);
        Assert.Equal("Loom Skill Orchestrator", initialize.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
        using var list = JsonDocument.Parse(lines[1]);
        var names = list.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().Select(static item => item.GetProperty("name").GetString()).ToArray();
        Assert.All(names, name => Assert.StartsWith("so_", name, StringComparison.Ordinal));
        Assert.Contains("so_inspect_workflow_fragment", names);
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
