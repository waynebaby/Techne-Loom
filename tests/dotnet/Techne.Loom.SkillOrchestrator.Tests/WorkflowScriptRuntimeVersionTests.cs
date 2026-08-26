using System.Diagnostics;
using System.IO;
using System.Text;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowScriptRuntimeVersionTests
{
    [Fact]
    public async Task WorkflowScript_MismatchedRuntimeVersionFailsBeforeScriptExecution()
    {
        var root = FindRepositoryRoot();
        var scriptFile = Path.Combine(Path.GetTempPath(), $"loom-so-version-script-{Guid.NewGuid():N}.cs");
        var inputFile = Path.Combine(Path.GetTempPath(), $"loom-so-version-input-{Guid.NewGuid():N}.json");
        var outputFile = Path.Combine(Path.GetTempPath(), $"loom-so-version-output-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(scriptFile, "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) => new(); }");
        await File.WriteAllTextAsync(inputFile, "{\"runtimeBinding\":\"dotnet-so\",\"runtimeVersion\":\"wrong-version\"}");

        try
        {
            var result = await RunCliAsync(root, $"--workflow-script --mode build --script-file \"{scriptFile}\" --input-file \"{inputFile}\" --output-file \"{outputFile}\"");
            Assert.Equal(2, result.ExitCode);
            Assert.Contains("runtimeVersion", result.StdOut, StringComparison.Ordinal);
            Assert.False(File.Exists(outputFile));
        }
        finally
        {
            DeleteFile(scriptFile);
            DeleteFile(inputFile);
            DeleteFile(outputFile);
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string root, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{typeof(DefaultWorkflowTaskTrackingService).Assembly.Location}\" {arguments}",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start SO CLI process.");
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
    }
}
