using System.Diagnostics;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class SchemaDemoExportBehaviorTests
{
    [Fact]
    public async Task CliSchemaDemoOutput_WritesBothFilesAndDemoCompiles()
    {
        var repoRoot = FindRepositoryRoot();
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-schema-demo-{Guid.NewGuid():N}");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-schema-demo-audit-{Guid.NewGuid():N}");

        try
        {
            var help = await RunCliAsync(repoRoot, "--help");
            Assert.Equal(0, help.ExitCode);
            Assert.Contains("--schema-demo-output", help.StdOut, StringComparison.Ordinal);
            Assert.Contains("workflow.schema.json", help.StdOut, StringComparison.Ordinal);
            Assert.Contains("workflow.demo.json", help.StdOut, StringComparison.Ordinal);

            var export = await RunCliAsync(repoRoot, $"--schema-demo-output \"{outputDirectory}\"");
            Assert.Equal(0, export.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(export.StdErr), export.StdErr);

            using var exportDocument = JsonDocument.Parse(export.StdOut);
            var payload = exportDocument.RootElement;
            var schemaFile = payload.GetProperty("schemaFile").GetString();
            var demoFile = payload.GetProperty("demoFile").GetString();
            Assert.NotNull(schemaFile);
            Assert.NotNull(demoFile);
            Assert.True(File.Exists(schemaFile));
            Assert.True(File.Exists(demoFile));
            Assert.Equal(Path.GetFullPath(outputDirectory), payload.GetProperty("outputDirectory").GetString());

            using var schemaDocument = JsonDocument.Parse(await File.ReadAllTextAsync(schemaFile!));
            Assert.Equal("techne-loom.workflow-instance", schemaDocument.RootElement.GetProperty("schemaId").GetString());
            Assert.Contains(
                "workflowPhase",
                schemaDocument.RootElement.GetProperty("requiredNodeFields").GetProperty("state").EnumerateArray().Select(static item => item.GetString()));

            var demo = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(demoFile!));
            Assert.Equal("dotnet-ao", demo.RuntimeBinding);
            Assert.Equal("01 Start", Assert.IsType<StateNode>(demo.Nodes["state.start"]).WorkflowPhase);
            Assert.Equal("02 Complete", Assert.IsType<StateNode>(demo.Nodes["state.done"]).WorkflowPhase);
            using var demoDocument = JsonDocument.Parse(await File.ReadAllTextAsync(demoFile!));
            Assert.Equal(
                JsonValueKind.Object,
                demoDocument.RootElement.GetProperty("nodes").GetProperty("transition.echo").GetProperty("guardExpression").ValueKind);

            var compile = await RunCliAsync(
                repoRoot,
                $"compile --workflow-file \"{demoFile}\" --audit-output \"{auditDirectory}\"");
            Assert.Equal(0, compile.ExitCode);
            Assert.Contains("Validation artifacts:", compile.StdErr, StringComparison.Ordinal);
            Assert.NotEmpty(Directory.GetFiles(auditDirectory, "workflow.json", SearchOption.AllDirectories));
        }
        finally
        {
            DeleteDirectory(outputDirectory);
            DeleteDirectory(auditDirectory);
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string repoRoot, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{typeof(Techne.Loom.AgentOrchestrator.Runtime.AoRuntimeService).Assembly.Location}\" {arguments}",
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

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
