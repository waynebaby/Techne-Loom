using System.Diagnostics;
using System.Reflection;
using Techne.Loom.AgentOrchestrator.Runtime;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class WorkflowScriptOutputMetadataTests
{
    [Fact]
    public async Task WorkflowScript_MissingRuntimeBindingFailsBeforeCandidateOutput()
    {
        var root = FindRepositoryRoot();
        var scriptFile = Path.Combine(Path.GetTempPath(), $"loom-ao-missing-binding-script-{Guid.NewGuid():N}.cs");
        var inputFile = Path.Combine(Path.GetTempPath(), $"loom-ao-missing-binding-input-{Guid.NewGuid():N}.json");
        var outputFile = Path.Combine(Path.GetTempPath(), $"loom-ao-missing-binding-output-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(scriptFile, "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) => new WorkflowInstance { RuntimeVersion = input.RuntimeVersion }; }");
        await File.WriteAllTextAsync(inputFile, $"{{\"runtimeBinding\":\"dotnet-ao\",\"runtimeVersion\":\"{GetRuntimeVersion()}\"}}");

        try
        {
            var result = await RunCliAsync(root, $"--workflow-script --mode build --script-file \"{scriptFile}\" --input-file \"{inputFile}\" --output-file \"{outputFile}\"");
            Assert.Equal(2, result.ExitCode);
            Assert.Contains("runtimeBinding", result.StdOut, StringComparison.Ordinal);
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
    public async Task WorkflowScript_MissingRuntimeVersionFailsBeforeCandidateOutput()
    {
        var root = FindRepositoryRoot();
        var scriptFile = Path.Combine(Path.GetTempPath(), $"loom-ao-missing-version-script-{Guid.NewGuid():N}.cs");
        var inputFile = Path.Combine(Path.GetTempPath(), $"loom-ao-missing-version-input-{Guid.NewGuid():N}.json");
        var outputFile = Path.Combine(Path.GetTempPath(), $"loom-ao-missing-version-output-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(scriptFile, "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) => new WorkflowInstance { RuntimeBinding = input.RuntimeBinding }; }");
        await File.WriteAllTextAsync(inputFile, $"{{\"runtimeBinding\":\"dotnet-ao\",\"runtimeVersion\":\"{GetRuntimeVersion()}\"}}");

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

    private static string GetRuntimeVersion()
        => typeof(AoRuntimeService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+', 2)[0]
            ?? throw new InvalidOperationException("AO runtime version was not found.");

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string root, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{typeof(AoRuntimeService).Assembly.Location}\" {arguments}",
            WorkingDirectory = root,
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
    }
}
