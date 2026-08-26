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
            foreach (var propertyName in new[] { "modelFile", "builderScriptFile", "verifierScriptFile" })
            {
                var generatedFile = payload.GetProperty(propertyName).GetString();
                Assert.NotNull(generatedFile);
                Assert.True(File.Exists(generatedFile));
                Assert.EndsWith(".cs", generatedFile, StringComparison.Ordinal);
            }
            Assert.Equal(5, payload.GetProperty("sha256").EnumerateObject().Count());
            Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("runtimeVersion").GetString()));
            Assert.Equal(Path.GetFullPath(outputDirectory), payload.GetProperty("outputDirectory").GetString());
            var scriptInputFile = Path.Combine(outputDirectory, "workflow-script-input.json");
            var scriptCandidateFile = Path.Combine(outputDirectory, "workflow-script-candidate.json");
            var scriptVerificationFile = Path.Combine(outputDirectory, "workflow-script-verification.json");
            await File.WriteAllTextAsync(
                scriptInputFile,
                JsonSerializer.Serialize(
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["runtimeBinding"] = "dotnet-ao",
                        ["runtimeVersion"] = payload.GetProperty("runtimeVersion").GetString(),
                        ["context"] = new Dictionary<string, object?>(StringComparer.Ordinal),
                        ["options"] = new Dictionary<string, object?>(StringComparer.Ordinal),
                    },
                    WorkflowJsonSerializer.CreateDefaultOptions(indented: false)));
            var builderScriptFile = payload.GetProperty("builderScriptFile").GetString();
            var verifierScriptFile = payload.GetProperty("verifierScriptFile").GetString();
            Assert.NotNull(builderScriptFile);
            Assert.NotNull(verifierScriptFile);
            var scriptRun = await RunCliAsync(
                repoRoot,
                $"--workflow-script --mode build --script-file \"{builderScriptFile}\" --input-file \"{scriptInputFile}\" --output-file \"{scriptCandidateFile}\" --verify-script \"{verifierScriptFile}\" --reference-workflow-file \"{demoFile}\" --verification-output-file \"{scriptVerificationFile}\" --audit-output \"{auditDirectory}\"");            Assert.Equal(0, scriptRun.ExitCode);
            Assert.True(File.Exists(scriptCandidateFile));
            Assert.True(File.Exists(scriptVerificationFile));
            using var scriptVerification = JsonDocument.Parse(await File.ReadAllTextAsync(scriptVerificationFile));
            Assert.True(scriptVerification.RootElement.GetProperty("totalChecks").GetInt32() > 0);
            Assert.Equal(0, scriptVerification.RootElement.GetProperty("failedChecks").GetInt32());

            using var schemaDocument = JsonDocument.Parse(await File.ReadAllTextAsync(schemaFile!));
            Assert.Equal("techne-loom.workflow-instance", schemaDocument.RootElement.GetProperty("schemaId").GetString());
            Assert.Contains(
                "workflowPhase",
                schemaDocument.RootElement.GetProperty("requiredNodeFields").GetProperty("state").EnumerateArray().Select(static item => item.GetString()));

            var demo = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(demoFile!));
            Assert.Equal("dotnet-ao", demo.RuntimeBinding);
            Assert.Equal("01 Intake", Assert.IsType<StateNode>(demo.Nodes["state.start"]).WorkflowPhase);
            Assert.Equal("10 Complete", Assert.IsType<StateNode>(demo.Nodes["state.done"]).WorkflowPhase);
            Assert.True(demo.Nodes.Count >= 20);
            Assert.Contains(demo.GetTransitionNodes().Values, item => item.StepKind == WorkflowStepKind.MemoryRead);
            Assert.Contains(demo.GetTransitionNodes().Values, item => item.StepKind == WorkflowStepKind.StateUpdate);
            Assert.Contains(demo.GetTransitionNodes().Values, item => item.StepKind == WorkflowStepKind.MemoryWrite);
            Assert.Contains(demo.GetTransitionNodes().Values, item => item.StepKind == WorkflowStepKind.ModelThink);
            Assert.Contains(demo.GetTransitionNodes().Values, item => item.StepKind == WorkflowStepKind.WaitResume);
            Assert.Contains(demo.GetTransitionNodes().Values, item => item.StepKind == WorkflowStepKind.ArtifactEmit);
            using var demoDocument = JsonDocument.Parse(await File.ReadAllTextAsync(demoFile!));
            Assert.Equal(
                JsonValueKind.Object,
                demoDocument.RootElement.GetProperty("nodes").GetProperty("transition.memory_read").GetProperty("guardExpression").ValueKind);
            Assert.Equal("command", demoDocument.RootElement.GetProperty("nodes").GetProperty("transition.memory_read").GetProperty("$kind").GetString());
            Assert.True(demoDocument.RootElement.GetProperty("nodes").GetProperty("transition.model_think").GetProperty("command").GetProperty("parameters").TryGetProperty("resumeOutputKey", out _));
            Assert.Contains("CommandParameterContracts", await File.ReadAllTextAsync(payload.GetProperty("modelFile").GetString()!));

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
