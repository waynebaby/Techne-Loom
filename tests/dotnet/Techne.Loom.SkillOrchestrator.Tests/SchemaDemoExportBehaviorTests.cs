using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class SchemaDemoExportBehaviorTests
{
    [Fact]
    public async Task CliSchemaDemoOutput_WritesBothFilesAndDemoCompiles()
    {
        var repoRoot = FindRepositoryRoot();
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-schema-demo-{Guid.NewGuid():N}");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-schema-demo-audit-{Guid.NewGuid():N}");

        try
        {
            var help = await RunCliAsync(repoRoot, "--help");
            Assert.Equal(0, help.ExitCode);
            Assert.Contains("--schema-demo-output", help.StdOut, StringComparison.Ordinal);
            Assert.Contains("--schema-demo-output <directory>", help.StdOut);
            Assert.Contains("compile --workflow-file <path>", help.StdOut);
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
                        ["runtimeBinding"] = "dotnet-so",
                        ["runtimeVersion"] = payload.GetProperty("runtimeVersion").GetString(),
                        ["context"] = new Dictionary<string, object?>(StringComparer.Ordinal),
                        ["contractBinding"] = new { path = "contract.json", format = "json", assetRootPath = outputDirectory },
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
            Assert.Equal("2", schemaDocument.RootElement.GetProperty("schemaVersion").GetString());
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
                "plan",
                schemaDocument.RootElement.GetProperty("allowedValues").GetProperty("workflowStepKind").EnumerateArray().Select(static item => item.GetString()));
            Assert.Contains(
                "plan",
                schemaDocument.RootElement.GetProperty("nodeFields").GetProperty("command").EnumerateArray().Select(static item => item.GetString()));
            Assert.Contains(
                "workflowPhase",
                schemaDocument.RootElement.GetProperty("requiredNodeFields").GetProperty("state").EnumerateArray().Select(static item => item.GetString()));

            var demo = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(demoFile!));
            Assert.Equal("dotnet-so", demo.RuntimeBinding);
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

    [Fact]
    public async Task FragmentDesignCommandsResumeAcrossProcessesAndCompileAnIndependentCandidate()
    {
        var repoRoot = FindRepositoryRoot();
        var sourceDirectory = Directory.CreateTempSubdirectory("loom-fragment-cli-test-").FullName;
        string? sessionDirectory = null;
        try
        {
            var demo = await WorkflowSchemaDemoExporter.WriteAsync(sourceDirectory, "dotnet-so", SkillCli.GetRuntimeVersion());
            var baseline = await File.ReadAllTextAsync(demo.DemoFile);
            var scope = Path.Combine(sourceDirectory, "scope.json");
            await File.WriteAllTextAsync(scope, """[{"kind":"state","owner":"state.start"}]""");
            var start = await RunCliAsync(repoRoot, $"fragment-design start --workflow-file \"{demo.DemoFile}\" --scope-file \"{scope}\"");
            Assert.True(start.ExitCode == 0, start.StdOut + start.StdErr);
            using var started = JsonDocument.Parse(start.StdOut);
            sessionDirectory = started.RootElement.GetProperty("session_dir").GetString();
            Assert.NotNull(sessionDirectory);
            var objective = Path.Combine(sourceDirectory, "objective.md");
            await File.WriteAllTextAsync(objective, "Keep fixture objective content file-backed.");
            var prompt = await RunCliAsync(repoRoot,
                $"fragment-design prompt --objective-file \"{objective}\" --session-dir \"{sessionDirectory}\"");
            Assert.True(prompt.ExitCode == 0, prompt.StdOut + prompt.StdErr);
            using var packet = JsonDocument.Parse(prompt.StdOut);
            Assert.Equal("edit", packet.RootElement.GetProperty("mode").GetString());
            Assert.Equal(0, packet.RootElement.GetProperty("expected_generation").GetInt64());
            Assert.Equal("state.start", packet.RootElement.GetProperty("authorized_fragments")[0].GetProperty("owner").GetString());
            Assert.DoesNotContain("Keep fixture objective content", prompt.StdOut, StringComparison.Ordinal);

            var edit = Path.Combine(sourceDirectory, "edit.json");
            await File.WriteAllTextAsync(edit,
                """{"expectedGeneration":5,"expectedGeneration":0,"reason":"Ambiguous","edit":{"kind":"state-description","stateId":"state.start","description":"Must not apply."}}""");
            var ambiguous = await RunCliAsync(repoRoot, $"fragment-design edit --session-dir \"{sessionDirectory}\" --edit-file \"{edit}\"");
            Assert.Equal(2, ambiguous.ExitCode);
            Assert.Contains("/expectedGeneration", ambiguous.StdOut, StringComparison.Ordinal);

            await File.WriteAllTextAsync(edit,
                """{"expectedGeneration":0,"reason":"Clarify the original role.","edit":{"kind":"state-description","stateId":"state.start","description":"Design fixture update."}}""");
            var changed = await RunCliAsync(repoRoot, $"fragment-design edit --session-dir \"{sessionDirectory}\" --edit-file \"{edit}\"");
            Assert.True(changed.ExitCode == 0, changed.StdOut + changed.StdErr);
            using var changedStatus = JsonDocument.Parse(changed.StdOut);
            Assert.Equal(1, changedStatus.RootElement.GetProperty("generation").GetInt32());

            var updatedPrompt = await RunCliAsync(repoRoot,
                $"fragment-design prompt --objective-file \"{objective}\" --session-dir \"{sessionDirectory}\"");
            Assert.True(updatedPrompt.ExitCode == 0, updatedPrompt.StdOut + updatedPrompt.StdErr);
            using var updatedPacket = JsonDocument.Parse(updatedPrompt.StdOut);
            Assert.Equal(sessionDirectory, updatedPacket.RootElement.GetProperty("session_dir").GetString());
            Assert.Equal(1, updatedPacket.RootElement.GetProperty("expected_generation").GetInt64());
            var handoffFile = updatedPacket.RootElement.GetProperty("handoff_file").GetString();
            Assert.NotNull(handoffFile);
            Assert.NotEqual(packet.RootElement.GetProperty("handoff_file").GetString(), handoffFile);
            using var handoff = JsonDocument.Parse(await File.ReadAllTextAsync(handoffFile));
            Assert.Equal(1, handoff.RootElement.GetProperty("generation").GetInt64());
            var workflow = WorkflowDraftImport.Read(handoff.RootElement.GetProperty("workflow").GetRawText());
            Assert.Equal("Design fixture update.", workflow.Nodes["state.start"].Description);
            Assert.Equal("state.start", handoff.RootElement.GetProperty("authorizedFragments")[0].GetProperty("owner").GetString());

            var repeat = await RunCliAsync(repoRoot, $"fragment-design edit --session-dir \"{sessionDirectory}\" --edit-file \"{edit}\"");
            Assert.Equal(2, repeat.ExitCode);
            var compose = await RunCliAsync(repoRoot,
                $"fragment-design compose --session-dir \"{sessionDirectory}\" --audit-output \"{Path.Combine(sourceDirectory, "audit")}\"");
            Assert.True(compose.ExitCode == 0, compose.StdOut + compose.StdErr);
            using var composed = JsonDocument.Parse(compose.StdOut);
            var candidate = composed.RootElement.GetProperty("candidate_file").GetString();
            Assert.NotNull(candidate);
            Assert.True(Directory.Exists(composed.RootElement.GetProperty("compile_audit_path").GetString()));
            var instance = WorkflowDraftImport.Read(await File.ReadAllTextAsync(candidate));
            Assert.Equal("Design fixture update.", instance.Nodes["state.start"].Description);
            Assert.Equal(baseline, await File.ReadAllTextAsync(demo.DemoFile));
            Assert.Contains("Validation artifacts:", compose.StdErr, StringComparison.Ordinal);
        }
        finally
        {
            if (sessionDirectory is not null)
            {
                DeleteDirectory(sessionDirectory);
            }
            DeleteDirectory(sourceDirectory);
        }
    }

    [Fact]
    public async Task FragmentBlueprintBuildsOnlyAfterIsolatedFragmentsFormACompleteDraft()
    {
        var repo = FindRepositoryRoot();
        var root = Directory.CreateTempSubdirectory("loom-blueprint-test-").FullName;
        string? candidateDirectory = null;
        string? checkCache = null;
        try
        {
            var demo = await WorkflowSchemaDemoExporter.WriteAsync(root, "dotnet-so", SkillCli.GetRuntimeVersion());
            var original = JsonNode.Parse(await File.ReadAllTextAsync(demo.DemoFile))!;
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
            var designPath = Path.Combine(root, "design.json");
            await File.WriteAllTextAsync(designPath, manifest.ToJsonString());
            var prompt = await RunCliAsync(repo, $"fragment-design prompt --objective-file \"{designPath}\"");
            Assert.True(prompt.ExitCode == 0, prompt.StdOut + prompt.StdErr);
            using var packet = JsonDocument.Parse(prompt.StdOut);
            Assert.Equal("new", packet.RootElement.GetProperty("mode").GetString());
            var early = await RunCliAsync(repo, $"fragment-design check --design-file \"{designPath}\"");
            Assert.True(early.ExitCode == 0, early.StdOut + early.StdErr);
            using var checkedEarly = JsonDocument.Parse(early.StdOut);
            Assert.Equal("incomplete", checkedEarly.RootElement.GetProperty("status").GetString());
            checkCache = checkedEarly.RootElement.GetProperty("cache_dir").GetString();
            var rejected = await RunCliAsync(repo, $"fragment-design build --design-file \"{designPath}\"");
            Assert.NotEqual(0, rejected.ExitCode);

            foreach (var key in new[] { "startNodeId", "endNodeId", "templateKind", "context", "contractBinding", "expressionBinding" })
            {
                manifest[key] = original[key]?.DeepClone();
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
            foreach (var key in new[] { "declaredUserOwnedFields", "reservedRuntimeOwnedFields" })
            {
                manifest[key] = original["validation"]![key]?.DeepClone();
            }
            await File.WriteAllTextAsync(designPath, manifest.ToJsonString());
            var validManifest = manifest.ToJsonString();
            manifest["context"]!["precisionProbe"] = JsonNode.Parse("1e0");
            await File.WriteAllTextAsync(designPath, manifest.ToJsonString());
            var normalized = await RunCliAsync(repo, $"fragment-design check --design-file \"{designPath}\"");
            Assert.NotEqual(0, normalized.ExitCode);
            Assert.Contains("/context/precisionProbe", normalized.StdOut, StringComparison.Ordinal);
            await File.WriteAllTextAsync(designPath, validManifest);
            var check = $"fragment-design check --design-file \"{designPath}\" --cache-dir \"{checkCache}\"";
            var cold = await RunCliAsync(repo, check);
            Assert.True(cold.ExitCode == 0, cold.StdOut + cold.StdErr);
            using var checkedCold = JsonDocument.Parse(cold.StdOut);
            var coldCount = checkedCold.RootElement.GetProperty("checked_expression_count").GetInt32();
            Assert.True(coldCount > 0);
            var warm = await RunCliAsync(repo, check);
            Assert.True(warm.ExitCode == 0, warm.StdOut + warm.StdErr);
            using var checkedWarm = JsonDocument.Parse(warm.StdOut);
            Assert.Equal(0, checkedWarm.RootElement.GetProperty("checked_expression_count").GetInt32());
            Assert.Equal(coldCount, checkedWarm.RootElement.GetProperty("reused_expression_count").GetInt32());
            Assert.Equal(checkedCold.RootElement.GetProperty("status").GetString(), checkedWarm.RootElement.GetProperty("status").GetString());
            await File.WriteAllTextAsync(Directory.EnumerateFiles(checkCache!).First(), "corrupt");
            var rebuilt = await RunCliAsync(repo, check);
            Assert.True(rebuilt.ExitCode == 0, rebuilt.StdOut + rebuilt.StdErr);
            using var checkedRebuilt = JsonDocument.Parse(rebuilt.StdOut);
            Assert.Equal(1, checkedRebuilt.RootElement.GetProperty("checked_expression_count").GetInt32());
            Assert.Equal(coldCount - 1, checkedRebuilt.RootElement.GetProperty("reused_expression_count").GetInt32());
            var gateFile = Path.Combine(root, manifest["gates"]![0]!.GetValue<string>());
            var validGate = await File.ReadAllTextAsync(gateFile);
            var invalidGate = JsonNode.Parse(validGate)!;
            invalidGate["gate"]!["passExpression"]!["source"] = "1";
            await File.WriteAllTextAsync(gateFile, invalidGate.ToJsonString());
            var invalid = await RunCliAsync(repo, check);
            Assert.True(invalid.ExitCode == 0, invalid.StdOut + invalid.StdErr);
            using var checkedInvalid = JsonDocument.Parse(invalid.StdOut);
            Assert.Equal("invalid", checkedInvalid.RootElement.GetProperty("status").GetString());
            Assert.True(checkedInvalid.RootElement.GetProperty("error_count").GetInt32() > 0);
            Assert.Equal(1, checkedInvalid.RootElement.GetProperty("checked_expression_count").GetInt32());
            await File.WriteAllTextAsync(gateFile, validGate);
            var build = await RunCliAsync(repo, $"fragment-design build --design-file \"{designPath}\" --audit-output \"{Path.Combine(root, "audit")}\"");
            Assert.True(build.ExitCode == 0, build.StdOut + build.StdErr);
            using var receipt = JsonDocument.Parse(build.StdOut);
            var candidate = receipt.RootElement.GetProperty("candidate_file").GetString();
            Assert.NotNull(candidate);
            candidateDirectory = Path.GetDirectoryName(candidate);
            Assert.True(JsonNode.DeepEquals(original, JsonNode.Parse(await File.ReadAllTextAsync(candidate))));
        }
        finally
        {
            if (candidateDirectory is not null)
            {
                DeleteDirectory(candidateDirectory);
            }
            if (checkCache is not null)
            {
                DeleteDirectory(checkCache);
            }
            DeleteDirectory(root);
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string repoRoot, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{typeof(DefaultWorkflowTaskTrackingService).Assembly.Location}\" {arguments}",
            WorkingDirectory = repoRoot,
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

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
