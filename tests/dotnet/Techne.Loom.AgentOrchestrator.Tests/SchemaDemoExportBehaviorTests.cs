using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
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
            Assert.Contains("--schema-demo-output <directory>", help.StdOut, StringComparison.Ordinal);
            Assert.Contains("compile --workflow-file <path>", help.StdOut, StringComparison.Ordinal);

            var export = await RunCliAsync(repoRoot, $"--schema-demo-output \"{outputDirectory}\"");
            Assert.Equal(0, export.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(export.StdErr), export.StdErr);

            using var exportDocument = JsonDocument.Parse(export.StdOut);
            var payload = exportDocument.RootElement;
            var schemaFile = payload.GetProperty("schemaFile").GetString();
            var demoFile = payload.GetProperty("demoFile").GetString();
            var contextContractFile = payload.GetProperty("contextContractFile").GetString();
            Assert.NotNull(schemaFile);
            Assert.NotNull(demoFile);
            Assert.True(File.Exists(schemaFile));
            Assert.True(File.Exists(demoFile));
            Assert.NotNull(contextContractFile);
            Assert.True(File.Exists(contextContractFile));
            foreach (var propertyName in new[] { "modelFile", "builderScriptFile", "verifierScriptFile" })
            {
                var generatedFile = payload.GetProperty(propertyName).GetString();
                Assert.NotNull(generatedFile);
                Assert.True(File.Exists(generatedFile));
                Assert.EndsWith(".cs", generatedFile, StringComparison.Ordinal);
            }
            Assert.Equal(6, payload.GetProperty("sha256").EnumerateObject().Count());
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
                        ["contractBinding"] = new { path = "contract.json", format = "json", assetRootPath = outputDirectory },
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

                $"--workflow-script --mode build --script-file \"{builderScriptFile}\" --input-file \"{scriptInputFile}\" --output-file \"{scriptCandidateFile}\" --verify-script \"{verifierScriptFile}\" --reference-workflow-file \"{demoFile}\" --verification-output-file \"{scriptVerificationFile}\" --audit-output \"{auditDirectory}\"");
            Assert.True(scriptRun.ExitCode == 0, $"{scriptRun.StdOut}{Environment.NewLine}{scriptRun.StdErr}");
            Assert.True(File.Exists(scriptCandidateFile));
            Assert.True(File.Exists(scriptVerificationFile));
            using var scriptVerification = JsonDocument.Parse(await File.ReadAllTextAsync(scriptVerificationFile));
            Assert.True(scriptVerification.RootElement.GetProperty("totalChecks").GetInt32() > 0);
            Assert.Equal(0, scriptVerification.RootElement.GetProperty("failedChecks").GetInt32());

            using var schemaDocument = JsonDocument.Parse(await File.ReadAllTextAsync(schemaFile!));
            Assert.Equal("techne-loom.workflow-instance", schemaDocument.RootElement.GetProperty("schemaId").GetString());
            Assert.Equal("3", schemaDocument.RootElement.GetProperty("schemaVersion").GetString());
            var allowedStrategies = schemaDocument.RootElement.GetProperty("allowedValues").GetProperty("concurrencyStrategy").EnumerateArray().Select(static item => item.GetString()).ToArray();
            Assert.Equal(new[] { "firstPass", "whenAnyResponse", "whenAnyPass", "whenAllPass", "whenExpression" }, allowedStrategies);
            var groupFields = schemaDocument.RootElement.GetProperty("transitionGroupFields").EnumerateArray().Select(static item => item.GetString()).ToArray();
            Assert.Contains("passExpression", groupFields);
            Assert.Contains("joinTargetStateId", groupFields);
            Assert.Contains("noPassTargetStateId", groupFields);
            Assert.Contains("errorHandlingTargetStateId", groupFields);
            var commandFields = schemaDocument.RootElement.GetProperty("nodeFields").GetProperty("command").EnumerateArray().Select(static item => item.GetString()).ToArray();
            Assert.Contains("passExpression", commandFields);
            Assert.Contains("maxRetries", commandFields);
            Assert.DoesNotContain("succeedExpression", commandFields);
            Assert.DoesNotContain("maxRetry", commandFields);
            Assert.Contains(
                "workflowPhase",
                schemaDocument.RootElement.GetProperty("requiredNodeFields").GetProperty("state").EnumerateArray().Select(static item => item.GetString()));

            var demo = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(demoFile!));
            Assert.Equal("contract.json", demo.ContractBinding?.Path);
            Assert.Equal(Path.GetFullPath(outputDirectory), demo.ContractBinding?.AssetRootPath);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FragmentDesignUsesAoValidationAndRejectsAnSoBaseline(bool owned)
    {
        var repo = FindRepositoryRoot();
        var root = Directory.CreateTempSubdirectory("loom-ao-fragment-test-").FullName;
        string? session = null;
        try
        {
            var export = await RunCliAsync(repo, $"--schema-demo-output \"{root}\"");
            Assert.True(export.ExitCode == 0, export.StdOut + export.StdErr);
            var baseline = Path.Combine(root, owned ? "sample-skill--baseline.json" : "workflow.demo.json");
            if (owned)
            {
                var ownedWorkflow = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(Path.Combine(root, "workflow.demo.json")));
                ownedWorkflow.OwnerSkillSlug = "sample-skill";
                await File.WriteAllTextAsync(baseline, WorkflowJsonSerializer.Serialize(ownedWorkflow));
            }
            var original = await File.ReadAllTextAsync(baseline);
            var draft = WorkflowJsonSerializer.Deserialize(original);
            var scope = Path.Combine(root, "scope.json");
            await File.WriteAllTextAsync(scope, JsonSerializer.Serialize(new[] { new { kind = "state", owner = draft.StartNodeId } }));

            draft.RuntimeBinding = "dotnet-so";
            var foreign = Path.Combine(root, "foreign.json");
            await File.WriteAllTextAsync(foreign, WorkflowJsonSerializer.Serialize(draft));
            var rejected = await RunCliAsync(repo, $"fragment-design start --workflow-file \"{foreign}\" --scope-file \"{scope}\"");
            Assert.NotEqual(0, rejected.ExitCode);

            var start = await RunCliAsync(repo, $"fragment-design start --workflow-file \"{baseline}\" --scope-file \"{scope}\"");
            Assert.True(start.ExitCode == 0, start.StdOut + start.StdErr);
            using var started = JsonDocument.Parse(start.StdOut);
            session = started.RootElement.GetProperty("session_dir").GetString();
            Assert.NotNull(session);
            var patch = Path.Combine(root, "edit.json");
            await File.WriteAllTextAsync(patch, JsonSerializer.Serialize(new
            {
                expectedGeneration = 0,
                reason = "Clarify the fixture state.",
                edit = new { kind = "state-description", stateId = draft.StartNodeId, description = "AO fixture edit." },
            }));
            var edit = await RunCliAsync(repo, $"fragment-design edit --session-dir \"{session}\" --edit-file \"{patch}\"");
            Assert.True(edit.ExitCode == 0, edit.StdOut + edit.StdErr);
            var compose = await RunCliAsync(repo,
                $"fragment-design compose --session-dir \"{session}\" --audit-output \"{Path.Combine(root, "audit")}\"");
            Assert.True(compose.ExitCode == 0, compose.StdOut + compose.StdErr);
            using var receipt = JsonDocument.Parse(compose.StdOut);
            var candidate = receipt.RootElement.GetProperty("candidate_file").GetString();
            Assert.NotNull(candidate);
            Assert.StartsWith(owned ? "sample-skill--candidate-" : "candidate-", Path.GetFileName(candidate), StringComparison.Ordinal);
            Assert.EndsWith(".json", candidate, StringComparison.Ordinal);
            var result = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(candidate));
            Assert.Equal("dotnet-ao", result.RuntimeBinding);
            Assert.Equal("AO fixture edit.", result.Nodes[draft.StartNodeId].Description);
            Assert.Equal(original, await File.ReadAllTextAsync(baseline));
        }
        finally
        {
            if (session is not null)
            {
                DeleteDirectory(session);
            }
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FragmentBlueprintBuildPreservesOwnedAndGenericNames(bool owned)
    {
        var repo = FindRepositoryRoot();
        var root = Directory.CreateTempSubdirectory("loom-ao-blueprint-name-").FullName;
        string? candidateDirectory = null;
        try
        {
            var export = await RunCliAsync(repo, $"--schema-demo-output \"{root}\"");
            Assert.True(export.ExitCode == 0, export.StdOut + export.StdErr);
            var original = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(root, "workflow.demo.json")))!;
            if (owned)
            {
                original["ownerSkillSlug"] = "sample-skill";
            }
            var manifest = new JsonObject
            {
                ["schema"] = "loom.workflow-fragment-blueprint.v1",
                ["instanceId"] = original["instanceId"]!.DeepClone(),
                ["states"] = new JsonArray(),
                ["transitions"] = new JsonArray(),
                ["groups"] = new JsonArray(),
                ["gates"] = new JsonArray(),
                ["routes"] = new JsonArray(),
            };
            foreach (var key in new[] { "startNodeId", "endNodeId", "templateKind", "ownerSkillSlug", "context", "contractBinding", "expressionBinding" })
            {
                manifest[key] = original[key]?.DeepClone();
            }
            foreach (var key in new[] { "declaredUserOwnedFields", "reservedRuntimeOwnedFields" })
            {
                manifest[key] = original["validation"]![key]?.DeepClone();
            }
            var index = 0;
            async Task AddAsync(string family, JsonNode fragment)
            {
                var name = $"fragment-{index++}.json";
                await File.WriteAllTextAsync(Path.Combine(root, name), fragment.ToJsonString());
                manifest[family]!.AsArray().Add(name);
            }
            foreach (var (id, source) in original["nodes"]!.AsObject())
            {
                var fragment = source!.DeepClone();
                if (fragment["$kind"]!.GetValue<string>() == "state")
                {
                    foreach (var group in fragment["groups"]!.AsArray())
                    {
                        await AddAsync("groups", new JsonObject { ["stateId"] = id, ["group"] = group!.DeepClone() });
                    }
                    fragment["groups"] = new JsonArray();
                    await AddAsync("states", fragment);
                }
                else
                {
                    await AddAsync("transitions", fragment);
                }
            }
            foreach (var (id, gate) in original["validation"]!["gates"]!.AsObject())
            {
                await AddAsync("gates", new JsonObject { ["id"] = id, ["gate"] = gate!.DeepClone() });
            }
            foreach (var (id, route) in original["validation"]!["routes"]!.AsObject())
            {
                await AddAsync("routes", new JsonObject { ["id"] = id, ["route"] = route!.DeepClone() });
            }
            var design = Path.Combine(root, "design.json");
            await File.WriteAllTextAsync(design, manifest.ToJsonString());
            var build = await RunCliAsync(repo, $"fragment-design build --design-file \"{design}\" --audit-output \"{Path.Combine(root, "audit")}\"");
            Assert.True(build.ExitCode == 0, build.StdOut + build.StdErr);
            using var receipt = JsonDocument.Parse(build.StdOut);
            var candidate = receipt.RootElement.GetProperty("candidate_file").GetString();
            Assert.NotNull(candidate);
            candidateDirectory = Path.GetDirectoryName(candidate);
            Assert.Equal(owned ? "sample-skill--candidate.json" : "candidate.json", Path.GetFileName(candidate));
            Assert.True(JsonNode.DeepEquals(original, JsonNode.Parse(await File.ReadAllTextAsync(candidate))));
        }
        finally
        {
            if (candidateDirectory is not null)
            {
                DeleteDirectory(candidateDirectory);
            }
            DeleteDirectory(root);
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
