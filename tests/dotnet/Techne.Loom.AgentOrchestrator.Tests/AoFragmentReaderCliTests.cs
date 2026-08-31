using System.Diagnostics;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class AoFragmentReaderCliTests
{
    [Fact]
    public async Task CliInspectWorkflowFragment_DefaultsToSummaryAndSupportsJsonPointer()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-fragment-{Guid.NewGuid():N}.json");
        var instance = new WorkflowInstance
        {
            InstanceId = "ao-fragment",
            Status = WorkflowStatus.Running,
            StartNodeId = "state.start",
            CurrentNodeId = "state.start",
            Context = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["secret"] = "do-not-return",
            },
        };
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(instance));

        try
        {
            var summaryRun = await RunCliAsync(repoRoot, $"inspect-workflow-fragment --workflow-file \"{workflowFile}\"");
            Assert.Equal(0, summaryRun.ExitCode);
            using var summary = JsonDocument.Parse(summaryRun.StdOut);
            Assert.Equal(JsonValueKind.Null, summary.RootElement.GetProperty("fragment").ValueKind);
            Assert.DoesNotContain("do-not-return", summaryRun.StdOut, StringComparison.Ordinal);

            var fragmentRun = await RunCliAsync(repoRoot, $"inspect-workflow-fragment --workflow-file \"{workflowFile}\" --json-pointer /context/secret");
            Assert.Equal(0, fragmentRun.ExitCode);
            using var fragment = JsonDocument.Parse(fragmentRun.StdOut);
            Assert.Equal("/context/secret", fragment.RootElement.GetProperty("jsonPointer").GetString());
            Assert.Equal("do-not-return", fragment.RootElement.GetProperty("fragment").GetString());
        }
        finally
        {
            DeleteFile(workflowFile);
        }
    }

    [Fact]
    public async Task CliInspectWorkflowFragment_ReadsLegacyAoSnapshotFields()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-snapshot-fragment-{Guid.NewGuid():N}.json");
        var snapshot = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["objective"] = "Inspect legacy AO snapshot",
            ["status"] = "blocked",
            ["current_node_id"] = "boundary.plan",
            ["audit_step_sequence"] = 4,
            ["context"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["request_kind"] = "analysis",
            },
        };
        await File.WriteAllTextAsync(workflowFile, JsonSerializer.Serialize(snapshot));

        try
        {
            var run = await RunCliAsync(repoRoot, $"inspect-workflow-fragment --workflow-file \"{workflowFile}\"");
            Assert.Equal(0, run.ExitCode);
            using var document = JsonDocument.Parse(run.StdOut);
            var summary = document.RootElement.GetProperty("summary");
            Assert.Equal("boundary.plan", summary.GetProperty("currentNodeId").GetString());
            Assert.Equal(4, summary.GetProperty("version").GetInt32());
            Assert.Equal("blocked", summary.GetProperty("status").GetString());
        }
        finally
        {
            DeleteFile(workflowFile);
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string repoRoot, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{typeof(AoFragmentReaderCliTests).Assembly.Location.Replace("Techne.Loom.AgentOrchestrator.Tests.dll", "ao.dll", StringComparison.Ordinal)}\" {arguments}",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start AO CLI process.");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout, stderr);
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

    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var lockFile = path + ".lock";
        if (File.Exists(lockFile))
        {
            File.Delete(lockFile);
        }
    }
}