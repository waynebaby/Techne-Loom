using System.Diagnostics;
using System.Text;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class CliFileInputContractTests
{
    [Fact]
    public async Task WorkflowScript_RejectsInlineContentAndDoesNotCreateOutput()
    {
        var root = FindRepositoryRoot();
        var scriptFile = Path.Combine(Path.GetTempPath(), $"loom-so-script-{Guid.NewGuid():N}.cs");
        var inputFile = Path.Combine(Path.GetTempPath(), $"loom-so-input-{Guid.NewGuid():N}.json");
        var outputFile = Path.Combine(Path.GetTempPath(), $"loom-so-output-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(scriptFile, "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) => new(); }");
        await File.WriteAllTextAsync(inputFile, "{\"runtimeBinding\":\"dotnet-so\"}");

        try
        {
            var result = await RunCliAsync(
                root,
                $"--workflow-script --script-file \"{scriptFile}\" --input-file \"{inputFile}\" --output-file \"{outputFile}\" --script-content \"inline\"");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("inline content", result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(outputFile));
        }
        finally
        {
            DeleteFile(scriptFile);
            DeleteFile(inputFile);
            DeleteFile(outputFile);
        }
    }

    [Fact]
    public async Task WorkflowScript_MissingReferenceFileFailsBeforeCandidateOutput()
    {
        var root = FindRepositoryRoot();
        var scriptFile = Path.Combine(Path.GetTempPath(), $"loom-so-script-{Guid.NewGuid():N}.cs");
        var inputFile = Path.Combine(Path.GetTempPath(), $"loom-so-input-{Guid.NewGuid():N}.json");
        var outputFile = Path.Combine(Path.GetTempPath(), $"loom-so-output-{Guid.NewGuid():N}.json");
        var verificationFile = Path.Combine(Path.GetTempPath(), $"loom-so-verification-{Guid.NewGuid():N}.json");
        var missingReference = Path.Combine(Path.GetTempPath(), $"loom-so-reference-missing-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(scriptFile, "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) => new(); }");
        await File.WriteAllTextAsync(inputFile, "{\"runtimeBinding\":\"dotnet-so\"}");

        try
        {
            var result = await RunCliAsync(
                root,
                $"--workflow-script --script-file \"{scriptFile}\" --input-file \"{inputFile}\" --output-file \"{outputFile}\" --verify-script \"{scriptFile}\" --reference-workflow-file \"{missingReference}\" --verification-output-file \"{verificationFile}\"");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("--reference-workflow-file", result.StdOut, StringComparison.Ordinal);
            Assert.False(File.Exists(outputFile));
            Assert.False(File.Exists(verificationFile));
        }
        finally
        {
            DeleteFile(scriptFile);
            DeleteFile(inputFile);
            DeleteFile(outputFile);
            DeleteFile(verificationFile);
        }
    }

    [Fact]
    public async Task Patch_RejectsInlineReplacementContentWithoutChangingTarget()
    {
        var root = FindRepositoryRoot();
        var targetFile = Path.Combine(Path.GetTempPath(), $"loom-so-patch-target-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(targetFile, "before\nafter\n", new UTF8Encoding(false));
        var original = await File.ReadAllTextAsync(targetFile);

        try
        {
            var result = await RunCliAsync(
                root,
                $"--patch --patch-content \"replacement\" --patch-target \"{targetFile}\" --from-line 1 --to-line 1");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("inline content", result.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(original, await File.ReadAllTextAsync(targetFile));
        }
        finally
        {
            DeleteFile(targetFile);
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
