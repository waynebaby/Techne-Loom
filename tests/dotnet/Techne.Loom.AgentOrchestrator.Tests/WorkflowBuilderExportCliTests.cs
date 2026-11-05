using System.Diagnostics;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class WorkflowBuilderExportCliTests
{
    [Fact]
    public async Task ExportMode_WritesRoslynBuilderFromDraftWorkflow()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"loom-ao-builder-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var workflowFile = Path.Combine(testDirectory, "workflow.json");
        var scriptFile = Path.Combine(testDirectory, "workflow-builder.cs");
        await File.WriteAllTextAsync(
            workflowFile,
            WorkflowJsonSerializer.Serialize(new WorkflowInstance
            {
                InstanceId = "workflow.ao-export",
                StartNodeId = "state.start",
                CurrentNodeId = "state.start",
                Status = WorkflowStatus.Drafting,
            }));

        try
        {
            var result = await RunCliAsync(
                $"--workflow-script --mode export --input-file \"{workflowFile}\" --output-file \"{scriptFile}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("workflow.ao-export", result.StdOut, StringComparison.Ordinal);
            Assert.True(File.Exists(scriptFile));
            var script = await File.ReadAllTextAsync(scriptFile);
            Assert.Contains("namespace Techne.Loom.WorkflowScripts;", script, StringComparison.Ordinal);
            Assert.Contains("WorkflowJsonSerializer.Deserialize(", script, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ExportMode_RejectsOutputPathThatWouldOverwriteInput()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"loom-ao-builder-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var workflowFile = Path.Combine(testDirectory, "workflow.json");
        var workflowJson = WorkflowJsonSerializer.Serialize(new WorkflowInstance
        {
            InstanceId = "workflow.ao-export-overwrite",
            StartNodeId = "state.start",
            CurrentNodeId = "state.start",
            Status = WorkflowStatus.Drafting,
        });
        await File.WriteAllTextAsync(workflowFile, workflowJson);

        try
        {
            var result = await RunCliAsync(
                $"--workflow-script --mode export --input-file \"{workflowFile}\" --output-file \"{workflowFile}\"");

            Assert.NotEqual(0, result.ExitCode);
            Assert.Equal(workflowJson, await File.ReadAllTextAsync(workflowFile));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string arguments)
    {
        var assemblyPath = typeof(WorkflowBuilderExportCliTests).Assembly.Location;
        var cliPath = assemblyPath.Replace("Techne.Loom.AgentOrchestrator.Tests.dll", "ao.dll", StringComparison.Ordinal);
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{cliPath}\" {arguments}",
            WorkingDirectory = Path.GetTempPath(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start AO CLI process.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

}
