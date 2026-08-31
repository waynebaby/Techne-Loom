using System.Diagnostics;
using System.Text.Json;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpGovernedStartupTests
{
    [Fact]
    public async Task SoMcpStdio_StartsAndUsesFragmentToolForExternalWorkflowCopy()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-mcp-governed-copy-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            workflowFile,
            "{\"instanceId\":\"mcp-governed-copy\",\"status\":\"readyToStart\",\"version\":1,\"nodes\":{},\"context\":{\"private_value\":\"must-not-be-returned-by-summary\"}}");

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

        try
        {
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start SO MCP process.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"governed-test\",\"version\":\"1.0.0\"}}}");
            await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
            var call = JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 2,
                ["method"] = "tools/call",
                ["params"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["name"] = "so_inspect_workflow_fragment",
                    ["arguments"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["workflow_file"] = workflowFile,
                    },
                },
            });
            await process.StandardInput.WriteLineAsync(call);
            process.StandardInput.Close();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            Assert.Equal(0, process.ExitCode);
            Assert.DoesNotContain("error", stderr, StringComparison.OrdinalIgnoreCase);
            var lines = stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, lines.Length);
            using var response = JsonDocument.Parse(lines[1]);
            var result = response.RootElement.GetProperty("result");
            Assert.False(result.GetProperty("isError").GetBoolean());
            var text = result.GetProperty("content")[0].GetProperty("text").GetString();
            Assert.Contains("mcp-governed-copy", text, StringComparison.Ordinal);
            Assert.DoesNotContain("must-not-be-returned-by-summary", text, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(workflowFile))
            {
                File.Delete(workflowFile);
            }
        }
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
