using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Analysis;
using Techne.Loom.SkillOrchestrator.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;
using Techne.Loom.SkillOrchestrator.Visualizer;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class SkillOrchestratorValidationTests : SkillOrchestratorBehaviorTestBase
{
    [Fact]
    public async Task CliCompile_BlockedRouteReusingCompileGate_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-governed-invalid-blocked-reuse-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateBlockedRouteWorkflowReusingCompileGate()));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("gate.blocked_candidate", run.StdOut);
        Assert.Contains("transition.compile_candidate", run.StdOut);
        Assert.Contains("dedicated blocked gate", run.StdOut);
    }

    [Fact]
    public async Task DefaultCommandDispatcher_WriteFile_DotTmpPathResolvesUnderTempRoot()
    {
        var dispatcher = new DefaultCommandDispatcher();
        var fileName = $"techne-loom-completion-{Guid.NewGuid():N}.md";
        var invocation = new CommandInvocation
        {
            Kind = CommandInvocationKind.Tool,
            Name = "write-file",
            Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["path"] = $".tmp/{fileName}",
                ["content"] = "self-bootstrap complete",
            },
        };

        var result = await dispatcher.ExecuteAsync(invocation, new Dictionary<string, object?>(StringComparer.Ordinal), progress: null, CancellationToken.None);

        var path = Assert.IsType<string>(result);
        Assert.StartsWith(Path.Combine(Path.GetTempPath(), ".tmp"), path, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(path));
        Assert.Equal("self-bootstrap complete", await File.ReadAllTextAsync(path));
        File.Delete(path);
    }

    [Fact]
    public async Task DefaultCommandDispatcher_WriteFile_JsonTargetIsPrettyPrintedAndAbsolute()
    {
        var dispatcher = new DefaultCommandDispatcher();
        var path = $".tmp/techne-loom-json-{Guid.NewGuid():N}.json";
        var invocation = new CommandInvocation
        {
            Kind = CommandInvocationKind.Tool,
            Name = "write-file",
            Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["path"] = path,
                ["content"] = """{"status":"ready","nested":{"value":true}}""",
            },
        };

        var result = await dispatcher.ExecuteAsync(invocation, new Dictionary<string, object?>(StringComparer.Ordinal), progress: null, CancellationToken.None);
        var outputPath = Assert.IsType<string>(result);

        try
        {
            Assert.True(Path.IsPathFullyQualified(outputPath));
            Assert.True(File.Exists(outputPath));
            Assert.True((await File.ReadAllLinesAsync(outputPath)).Length > 1);
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            Assert.Equal("ready", document.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }


    [Fact]
    public async Task DefaultCommandDispatcher_WriteFile_UniqueName_GeneratesDistinctTempPaths()
    {
        var dispatcher = new DefaultCommandDispatcher();
        var invocation = new CommandInvocation
        {
            Kind = CommandInvocationKind.Tool,
            Name = "write-file",
            Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["path"] = ".tmp/loom-skill-enhancement-completion-manifest.md",
                ["content"] = "manifest",
                ["uniqueName"] = true,
            },
        };

        var first = Assert.IsType<string>(await dispatcher.ExecuteAsync(invocation, new Dictionary<string, object?>(StringComparer.Ordinal), progress: null, CancellationToken.None));
        var second = Assert.IsType<string>(await dispatcher.ExecuteAsync(invocation, new Dictionary<string, object?>(StringComparer.Ordinal), progress: null, CancellationToken.None));

        try
        {
            Assert.NotEqual(first, second);
            Assert.True(File.Exists(first));
            Assert.True(File.Exists(second));
        }
        finally
        {
            if (File.Exists(first))
            {
                File.Delete(first);
            }

            if (File.Exists(second))
            {
                File.Delete(second);
            }
        }
    }

    [Fact]
    public async Task CliCompile_GovernedWorkflowMissingValidationContract_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-governed-missing-validation-{Guid.NewGuid():N}.json");
        var workflow = CreateGovernedWorkflow();
        workflow.Validation = null;
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(workflow));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("SO3000", run.StdOut);
        Assert.Contains("root validation contract", run.StdOut);
    }

    [Fact]
    public async Task CliCompile_GovernanceOnlyDonePath_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-governed-invalid-done-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateGovernanceOnlyDoneWorkflow()));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("SO4000", run.StdOut);
        Assert.Contains("gate.assessment", run.StdOut);
    }

    [Fact]
    public async Task CliCompile_AskUserRuntimeOwnedField_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-governed-invalid-ask-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateAskUserRuntimeOwnedFieldWorkflow()));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("SO2000", run.StdOut);
        Assert.Contains("workflow_file", run.StdOut);
    }

    [Fact]
    public async Task CliCompile_BlockedRouteMissingStrongestEarnedOutputs_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-governed-invalid-blocked-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateBlockedRouteWorkflowMissingOutputs()));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("SO3000", run.StdOut);
        Assert.Contains("validator_output", run.StdOut);
    }

    [Fact]
    public async Task CliCompile_MalformedJson_WritesFailureEvidenceWithoutPlaceholderRenders()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-malformed-json-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-malformed-json-audit-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(workflowFile, "{\"nodes\":");

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Single(Directory.GetFiles(auditDirectory, "workflow.json", SearchOption.AllDirectories));
        Assert.Single(Directory.GetFiles(auditDirectory, "workflow.compile-feedback.json", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(auditDirectory, "workflow.html", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CliCompile_MissingWorkflowPhase_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-missing-phase-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-missing-phase-audit-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateWorkflowMissingPhase()));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("SO1000", run.StdOut);
        Assert.Contains("workflowPhase", run.StdOut);
        Assert.Contains("state.start", run.StdOut);
        Assert.Contains("overall workflow stage", run.StdOut);
        Assert.Contains("01 Intake", run.StdOut);
        Assert.Empty(Directory.GetFiles(auditDirectory, "workflow.html", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories));
        Assert.Single(Directory.GetFiles(auditDirectory, "workflow.json", SearchOption.AllDirectories));
        Assert.Single(Directory.GetFiles(auditDirectory, "workflow.compile-feedback.json", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CliCompile_MixedInvalidContractPointerEscape_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-invalid-contract-pointer-{Guid.NewGuid():N}.json");
        var workflow = CreateResumeWorkflow();
        var ask = Assert.IsType<CommandTransition>(workflow.Nodes["transition.ask"]);
        workflow.ContractBinding = new ContractBinding();
        workflow.Nodes[ask.Id] = new CommandTransition
        {
            Id = ask.Id,
            Name = ask.Name,
            Description = ask.Description,
            TargetNodeId = ask.TargetNodeId,
            WorkflowPhase = ask.WorkflowPhase,
            StepKind = ask.StepKind,
            GuardExpression = ask.GuardExpression,
            SucceedExpression = ask.SucceedExpression,
            Command = ask.Command,
            ContractRefs = ["/rules/~2~0value"],
        };
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(workflow));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("SO1000", run.StdOut);
        Assert.Contains("invalid JSON Pointer escape", run.StdOut, StringComparison.Ordinal);
        Assert.Contains("~2~0value", run.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CliCompile_GovernedExternalDuplicateWrapper_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-duplicate-wrapper-{Guid.NewGuid():N}.json");
        var workflow = CreateGovernedWorkflow();
        var start = Assert.IsType<StateNode>(workflow.Nodes["state.start"]);
        start.Groups[0].TransitionIds = ["transition.external"];
        var external = new CommandTransition
        {
            Id = "transition.external",
            Name = "External selection",
            TargetNodeId = workflow.EndNodeId,
            StepKind = WorkflowStepKind.WaitResume,
            OutputPath = "stage_selection",
            SatisfiesGateIds = ["gate.assessment"],
            PublishesOutputFamilies = ["assessment_summary_json", "assessment_report_md"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new List<object?> { "stage_selection" },
                },
            },
        };
        workflow.Nodes[external.Id] = external;
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(workflow));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("implicit wrapper projection", run.StdOut, StringComparison.Ordinal);
        Assert.Contains("resumeOutputKey", run.StdOut, StringComparison.Ordinal);
    }
    [Fact]
    public async Task CliCompile_GovernedOutputBindingCycle_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-output-cycle-{Guid.NewGuid():N}.json");
        var workflow = CreateGovernedWorkflow();
        var emit = Assert.IsType<CommandTransition>(workflow.Nodes["transition.emit_assessment"]);
        emit.Command.Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["assessment_summary_json"] = "$context:assessment_report_md",
                ["assessment_report_md"] = "$context:assessment_summary_json",
            },
        };
        workflow.Nodes[emit.Id] = emit;
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(workflow));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("cyclic $context output binding", run.StdOut, StringComparison.Ordinal);
    }
    [Fact]
    public async Task CliCompile_InvalidOutputBindingsExpression_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-invalid-output-bindings-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateInvalidOutputBindingsWorkflow()));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("SO1000", run.StdOut);
        Assert.Contains("outputBindings", run.StdOut);
        Assert.Contains("$unknown", run.StdOut);
    }

    [Fact]
    public async Task CliCompile_SelfReferentialResultOutputBinding_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-self-ref-output-bindings-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateSelfReferentialOutputBindingsWorkflow()));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("SO1000", run.StdOut);
        Assert.Contains("self-referential result object", run.StdOut);
        Assert.Contains("tool.result.copy", run.StdOut);
    }

    [Fact]
    public async Task CliRun_InvalidGovernedWorkflow_IsRejectedOnLoadWithoutCompile()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-governed-invalid-run-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateAskUserRuntimeOwnedFieldWorkflow()));

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("SO2000", run.StdOut);
    }

    [Fact]
    public async Task StartOrAdvanceAsync_TimeoutGroup_MovesToTimeoutTarget()
    {
        var instance = CreateImmediateTimeoutWorkflow();
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var engine = new DefaultTaskTrackingEngine(store);
        var service = new DefaultWorkflowTaskTrackingService(engine);

        var first = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.True(first.Suspended);
        Assert.Equal(WorkflowStatus.WaitingExternal, first.StatusProjection.Status);

        var second = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.False(second.Suspended);
        Assert.False(second.Failed);
        Assert.Equal(WorkflowStatus.Succeeded, second.StatusProjection.Status);
        Assert.Equal("state.timeout", second.StatusProjection.CurrentNodeId);

        var saved = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(saved);
        Assert.Empty(saved!.ActiveWaitGroups);
    }

    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadCheckedInAssets_LoadsFileSnapshotsFromTargetSkillPath()
    {
        var targetSkillPath = Path.Combine(Path.GetTempPath(), $"techne-loom-memory-read-{Guid.NewGuid():N}");
        Directory.CreateDirectory(targetSkillPath);
        var skillFile = Path.Combine(targetSkillPath, "SKILL.md");
        await File.WriteAllTextAsync(skillFile, "# Skill\n");

        var instance = CreateCheckedInAssetMemoryReadWorkflow();
        instance.Context["target_skill_path"] = targetSkillPath;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var engine = new DefaultTaskTrackingEngine(store);
        var service = new DefaultWorkflowTaskTrackingService(engine);

        var first = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.True(first.Progressed, first.ErrorMessage);
        Assert.Equal("state.done", first.StatusProjection.CurrentNodeId);

        var second = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.Equal(WorkflowStatus.Succeeded, second.StatusProjection.Status);

        var saved = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(saved);
        var inspection = Assert.IsAssignableFrom<IDictionary<string, object?>>(saved!.Context["inspection"]);
        Assert.Equal(Path.GetFullPath(targetSkillPath), Convert.ToString(inspection["checkedInAssetRoot"]));

        var assets = Assert.IsAssignableFrom<IEnumerable<object?>>(inspection["checkedInAssets"]);
        var asset = Assert.Single(assets);
        var assetSnapshot = Assert.IsAssignableFrom<IDictionary<string, object?>>(asset);
        Assert.Equal("SKILL.md", Convert.ToString(assetSnapshot["path"]));
        Assert.Equal(Path.GetFullPath(skillFile), Convert.ToString(assetSnapshot["resolvedPath"]));
        Assert.Equal("# Skill\n", Convert.ToString(assetSnapshot["content"]));
    }

    [Fact]

    public async Task StartOrAdvanceAsync_MemoryReadDocumentCopyManifest_PublishesValidatedInternalEvidence()

    {

        var targetSkillPath = Path.Combine(Path.GetTempPath(), $"techne-loom-document-manifest-{Guid.NewGuid():N}");

        var referenceRoot = Path.Combine(targetSkillPath, "assets", "so-workflow", "reference", "so");

        Directory.CreateDirectory(referenceRoot);

        var manifestRelativePath = "assets/so-workflow/reference/document-copy-manifest.json";

        var mapRelativePath = "assets/so-workflow/node-to-file-map.md";

        var documentRelativePath = "assets/so-workflow/reference/so/runtime-contracts.md";

        var manifestPath = Path.Combine(targetSkillPath, manifestRelativePath.Replace('/', Path.DirectorySeparatorChar));

        var mapPath = Path.Combine(targetSkillPath, mapRelativePath.Replace('/', Path.DirectorySeparatorChar));

        var documentPath = Path.Combine(targetSkillPath, documentRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var sourceRelativePath = "docs/en/guides/so-guide-reference-contracts.md";
        var sourcePath = Path.Combine(targetSkillPath, sourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var packageRoot = Path.Combine(targetSkillPath, "extracted-package");
        var runtimeRoot = Path.Combine(packageRoot, "tools", "win-x64");
        var packageGuidePath = Path.Combine(runtimeRoot, "docs", "en", "guides", "so-guide-reference-contracts.md");
        var packageRuntimeManifestPath = Path.Combine(runtimeRoot, "runtime.json");
        var packageLockPath = Path.Combine(targetSkillPath, "assets", "so-workflow", "so-package-lock.json");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(packageGuidePath)!);



        await File.WriteAllTextAsync(documentPath, "# Source contract\n");
        await File.WriteAllTextAsync(sourcePath, "# Source contract\n");
        await File.WriteAllTextAsync(packageGuidePath, "# Source contract\n");
        await File.WriteAllTextAsync(
            packageRuntimeManifestPath,
            JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["schema"] = "techne-loom-runtime-v1",
                ["product"] = "so",
                ["package_id"] = "Techne.Loom.SkillOrchestrator.Runtime.win-x64",
                ["version"] = "0.3.253-beta",
                ["rid"] = "win-x64",
                ["docs_root"] = "tools/win-x64/docs/en",
            }));
        var sourceSha256 = ComputeCanonicalDocumentHash(packageGuidePath);
        await File.WriteAllTextAsync(packageLockPath, JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["package_id"] = "Techne.Loom.SkillOrchestrator",
            ["channel"] = "beta",
            ["resolved_version"] = "0.3.253-beta",
        }));

        await File.WriteAllTextAsync(mapPath, "# Node To File Map" + Environment.NewLine + "All checked-in document paths in this map are relative to the target skill root." + Environment.NewLine + "| Node | File |" + Environment.NewLine + "| --- | --- |" + Environment.NewLine + "| inspect | `assets/so-workflow/reference/document-copy-manifest.json` and `assets/so-workflow/reference/so/runtime-contracts.md` |" + Environment.NewLine);

        var manifest = new Dictionary<string, object?>(StringComparer.Ordinal)

        {

            ["schema_version"] = "1",

            ["target_skill_root"] = "target",

            ["target_bound_product"] = "so",

            ["target_bound_channel"] = "beta",

            ["target_bound_version"] = "0.3.253-beta",

            ["documents"] = new object?[]

            {

                new Dictionary<string, object?>(StringComparer.Ordinal)

                {

                    ["target_path"] = documentRelativePath,

                    ["source_path"] = "docs/en/guides/so-guide-reference-contracts.md",
                    ["source_package_id"] = "Techne.Loom.SkillOrchestrator.Runtime.win-x64",
                    ["source_package_rid"] = "win-x64",
                    ["source_package_path"] = "tools/win-x64/docs/en/guides/so-guide-reference-contracts.md",
                    ["content_mode"] = "full-document",

                    ["source_product"] = "so",

                    ["source_channel"] = "beta",

                    ["source_version"] = "0.3.253-beta",

                    ["source_sha256"] = sourceSha256,

                    ["artifact_origin"] = "verified-copy",

                    ["authority_scope"] = "target-local context only",

                    ["refreshed_by"] = "test",

                },

            },

        };

        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));



        var instance = CreateCheckedInAssetMemoryReadWorkflow(

            checkedInAssets: [manifestRelativePath, mapRelativePath, documentRelativePath],

            documentCopyManifestPath: manifestRelativePath,

            nodeToFileMapPath: mapRelativePath);

        instance.Context["target_skill_path"] = targetSkillPath;
        var inspect = Assert.IsType<CommandTransition>(instance.Nodes["transition.inspect"]);
        inspect.Command.Parameters!["documentCopySourceRootPath"] = packageRoot;



        var store = new InMemoryInstanceStore();

        await store.SaveNewAsync(instance);

        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));



        var first = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(first.Progressed, first.ErrorMessage);

        var second = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.Equal(WorkflowStatus.Succeeded, second.StatusProjection.Status);



        var saved = await service.GetInstanceAsync(instance.InstanceId);

        Assert.NotNull(saved);

        var inspection = Assert.IsAssignableFrom<IDictionary<string, object?>>(saved!.Context["inspection"]);

        var manifestEvidence = Assert.IsAssignableFrom<IDictionary<string, object?>>(inspection["documentCopyManifest"]);

        Assert.Equal("0.3.253-beta", Convert.ToString(manifestEvidence["targetBoundVersion"]));

        Assert.Equal(1, Convert.ToInt32(manifestEvidence["documentCount"]));
        Assert.Contains("targetContainsCompleteSource", JsonSerializer.Serialize(manifestEvidence["documents"]), StringComparison.Ordinal);

        var mapEvidence = Assert.IsAssignableFrom<IDictionary<string, object?>>(inspection["nodeToFileMap"]);

        Assert.Equal("target-root-relative", Convert.ToString(mapEvidence["pathPolicy"]));

    }



    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadDocumentCopyManifest_UsesMatchingExtractedPackageGuide()
    {
        var fixture = await CreateDocumentCopyManifestFixtureAsync(
            targetDocumentContent: "# Contract reference\n# Package source\n");
        var packageRoot = Path.Combine(fixture.TargetSkillPath, "extracted-package");
        var runtimeRoot = Path.Combine(packageRoot, "tools", "win-x64");
        var packageGuide = Path.Combine(runtimeRoot, "docs", "en", "guides", "so-guide-reference-contracts.md");
        Directory.CreateDirectory(Path.GetDirectoryName(packageGuide)!);
        await File.WriteAllTextAsync(packageGuide, "# Package source\r\n\r\n");
        await File.WriteAllTextAsync(
            Path.Combine(runtimeRoot, "runtime.json"),
            JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["schema"] = "techne-loom-runtime-v1",
                ["product"] = "so",
                ["package_id"] = "Techne.Loom.SkillOrchestrator.Runtime.win-x64",
                ["version"] = "0.3.253-beta",
                ["rid"] = "win-x64",
                ["docs_root"] = "tools/win-x64/docs/en",
            }));

        var packageHash = ComputeCanonicalDocumentHash(packageGuide);
        var manifestPath = Path.Combine(fixture.TargetSkillPath, fixture.ManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var manifestText = await File.ReadAllTextAsync(manifestPath);
        var updatedManifestText = new Regex("\"source_sha256\"\\s*:\\s*\"[0-9a-fA-F]{64}\"").Replace(
            manifestText,
            $"\"source_sha256\": \"{packageHash}\"",
            1);
        Assert.NotEqual(manifestText, updatedManifestText);
        await File.WriteAllTextAsync(manifestPath, updatedManifestText);

        var instance = CreateCheckedInAssetMemoryReadWorkflow(
            checkedInAssets: [fixture.ManifestRelativePath, fixture.MapRelativePath, fixture.DocumentRelativePath],
            documentCopyManifestPath: fixture.ManifestRelativePath,
            nodeToFileMapPath: fixture.MapRelativePath,
            documentCopySourceRootPath: fixture.PackageRoot);
        instance.Context["target_skill_path"] = fixture.TargetSkillPath;
        var inspect = Assert.IsType<CommandTransition>(instance.Nodes["transition.inspect"]);
        inspect.Command.Parameters!["documentCopySourceRootPath"] = packageRoot;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var first = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.True(first.Progressed, first.ErrorMessage);
        var second = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.Equal(WorkflowStatus.Succeeded, second.StatusProjection.Status);

        var saved = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(saved);
        var inspection = Assert.IsAssignableFrom<IDictionary<string, object?>>(saved!.Context["inspection"]);
        var manifestEvidence = Assert.IsAssignableFrom<IDictionary<string, object?>>(inspection["documentCopyManifest"]);
        var documents = Assert.IsType<List<object>>(manifestEvidence["documents"]);
        var document = Assert.IsAssignableFrom<IDictionary<string, object?>>(Assert.Single(documents));
        Assert.Equal(Path.GetFullPath(packageGuide), Convert.ToString(document["sourceResolvedPath"]));
    }

    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadDocumentCopyManifest_RejectsSourceWithoutMatchingPackageRoot()
    {
        var fixture = await CreateDocumentCopyManifestFixtureAsync(includePackageRoot: false);
        var fallbackSourcePath = Path.Combine(fixture.TargetSkillPath, "docs", "en", "guides", "so-guide-reference-contracts.md");
        Directory.CreateDirectory(Path.GetDirectoryName(fallbackSourcePath)!);
        await File.WriteAllTextAsync(fallbackSourcePath, "# Source contract\n");

        var instance = CreateCheckedInAssetMemoryReadWorkflow(
            checkedInAssets: [fixture.ManifestRelativePath, fixture.MapRelativePath, fixture.DocumentRelativePath],
            documentCopyManifestPath: fixture.ManifestRelativePath,
            nodeToFileMapPath: fixture.MapRelativePath);
        instance.Context["target_skill_path"] = fixture.TargetSkillPath;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(tick.Failed);
        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);
        Assert.Contains("not found for provenance verification", tick.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadDocumentCopyManifest_RejectsSourceVersionMismatch()
    {
        var fixture = await CreateDocumentCopyManifestFixtureAsync(sourceVersion: "0.3.248-beta");
        var instance = CreateCheckedInAssetMemoryReadWorkflow(
            checkedInAssets: [fixture.ManifestRelativePath, fixture.MapRelativePath, fixture.DocumentRelativePath],
            documentCopyManifestPath: fixture.ManifestRelativePath,
            nodeToFileMapPath: fixture.MapRelativePath,
            documentCopySourceRootPath: fixture.PackageRoot);
        instance.Context["target_skill_path"] = fixture.TargetSkillPath;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(tick.Failed);
        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);
        Assert.Contains("source_version", tick.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("target_bound_version", tick.ErrorMessage, StringComparison.Ordinal);
    }
    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadDocumentCopyManifest_RejectsSourceHashMismatch()
    {
        var fixture = await CreateDocumentCopyManifestFixtureAsync(sourceSha256: new string('a', 64));
        var instance = CreateCheckedInAssetMemoryReadWorkflow(
            checkedInAssets: [fixture.ManifestRelativePath, fixture.MapRelativePath, fixture.DocumentRelativePath],
            documentCopyManifestPath: fixture.ManifestRelativePath,
            nodeToFileMapPath: fixture.MapRelativePath,
            documentCopySourceRootPath: fixture.PackageRoot);
        instance.Context["target_skill_path"] = fixture.TargetSkillPath;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(tick.Failed);
        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);
        Assert.Contains("source_sha256", tick.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("does not match the source file", tick.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadDocumentCopyManifest_RejectsMissingPackageProvenance()
    {
        var fixture = await CreateDocumentCopyManifestFixtureAsync(sourcePackageId: null);
        var instance = CreateCheckedInAssetMemoryReadWorkflow(
            checkedInAssets: [fixture.ManifestRelativePath, fixture.MapRelativePath, fixture.DocumentRelativePath],
            documentCopyManifestPath: fixture.ManifestRelativePath,
            nodeToFileMapPath: fixture.MapRelativePath,
            documentCopySourceRootPath: fixture.PackageRoot);
        instance.Context["target_skill_path"] = fixture.TargetSkillPath;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(tick.Failed);
        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);
        Assert.Contains("source_package_id", tick.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadDocumentCopyManifest_RejectsPackageIdMismatch()
    {
        var fixture = await CreateDocumentCopyManifestFixtureAsync(sourcePackageId: "Techne.Loom.AgentOrchestrator.Runtime.win-x64");
        var instance = CreateCheckedInAssetMemoryReadWorkflow(
            checkedInAssets: [fixture.ManifestRelativePath, fixture.MapRelativePath, fixture.DocumentRelativePath],
            documentCopyManifestPath: fixture.ManifestRelativePath,
            nodeToFileMapPath: fixture.MapRelativePath,
            documentCopySourceRootPath: fixture.PackageRoot);
        instance.Context["target_skill_path"] = fixture.TargetSkillPath;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(tick.Failed);
        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);
        Assert.Contains("source_package_id", tick.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("does not match", tick.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadDocumentCopyManifest_RejectsUnsafePackageRid()
    {
        var fixture = await CreateDocumentCopyManifestFixtureAsync(sourcePackageRid: "win-x64/../linux-x64");
        var instance = CreateCheckedInAssetMemoryReadWorkflow(
            checkedInAssets: [fixture.ManifestRelativePath, fixture.MapRelativePath, fixture.DocumentRelativePath],
            documentCopyManifestPath: fixture.ManifestRelativePath,
            nodeToFileMapPath: fixture.MapRelativePath,
            documentCopySourceRootPath: fixture.PackageRoot);
        instance.Context["target_skill_path"] = fixture.TargetSkillPath;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(tick.Failed);
        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);
        Assert.Contains("source_package_rid", tick.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("unsafe", tick.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadDocumentCopyManifest_RejectsUnsupportedPackageRid()
    {
        var fixture = await CreateDocumentCopyManifestFixtureAsync(
            sourcePackageId: "Techne.Loom.SkillOrchestrator.Runtime.bogus",
            sourcePackageRid: "bogus",
            sourcePackagePath: "tools/bogus/docs/en/guides/so-guide-reference-contracts.md");
        var instance = CreateCheckedInAssetMemoryReadWorkflow(
            checkedInAssets: [fixture.ManifestRelativePath, fixture.MapRelativePath, fixture.DocumentRelativePath],
            documentCopyManifestPath: fixture.ManifestRelativePath,
            nodeToFileMapPath: fixture.MapRelativePath,
            documentCopySourceRootPath: fixture.PackageRoot);
        instance.Context["target_skill_path"] = fixture.TargetSkillPath;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(tick.Failed);
        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);
        Assert.Contains("source_package_rid", tick.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("unsupported", tick.ErrorMessage, StringComparison.Ordinal);
    }
    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadDocumentCopyManifest_RejectsPackagePathOutsideGuideTree()
    {
        var fixture = await CreateDocumentCopyManifestFixtureAsync(sourcePackagePath: "tools/win-x64/lib/so-guide-reference-contracts.md");
        var instance = CreateCheckedInAssetMemoryReadWorkflow(
            checkedInAssets: [fixture.ManifestRelativePath, fixture.MapRelativePath, fixture.DocumentRelativePath],
            documentCopyManifestPath: fixture.ManifestRelativePath,
            nodeToFileMapPath: fixture.MapRelativePath,
            documentCopySourceRootPath: fixture.PackageRoot);
        instance.Context["target_skill_path"] = fixture.TargetSkillPath;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(tick.Failed);
        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);
        Assert.Contains("source_package_path", tick.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("English package guide page", tick.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadDocumentCopyManifest_RejectsTruncatedTargetCopies()
    {
        var fixture = await CreateDocumentCopyManifestFixtureAsync(targetDocumentContent: "# Contract reference\n");
        var instance = CreateCheckedInAssetMemoryReadWorkflow(
            checkedInAssets: [fixture.ManifestRelativePath, fixture.MapRelativePath, fixture.DocumentRelativePath],
            documentCopyManifestPath: fixture.ManifestRelativePath,
            nodeToFileMapPath: fixture.MapRelativePath,
            documentCopySourceRootPath: fixture.PackageRoot);
        instance.Context["target_skill_path"] = fixture.TargetSkillPath;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(tick.Failed);
        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);
        Assert.Contains("does not contain the complete source document", tick.ErrorMessage, StringComparison.Ordinal);
    }
    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadDocumentCopyManifest_RejectsExcerptCopies()
    {
        var fixture = await CreateDocumentCopyManifestFixtureAsync(contentMode: "controlled-excerpt");
        var instance = CreateCheckedInAssetMemoryReadWorkflow(
            checkedInAssets: [fixture.ManifestRelativePath, fixture.MapRelativePath, fixture.DocumentRelativePath],
            documentCopyManifestPath: fixture.ManifestRelativePath,
            nodeToFileMapPath: fixture.MapRelativePath,
            documentCopySourceRootPath: fixture.PackageRoot);
        instance.Context["target_skill_path"] = fixture.TargetSkillPath;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(tick.Failed);
        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);
        Assert.Contains("content_mode", tick.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("full-document", tick.ErrorMessage, StringComparison.Ordinal);
    }
    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadDocumentCopyManifest_RejectsPackageLockMismatch()
    {
        var fixture = await CreateDocumentCopyManifestFixtureAsync(lockVersion: "0.3.248-beta");
        var instance = CreateCheckedInAssetMemoryReadWorkflow(
            checkedInAssets: [fixture.ManifestRelativePath, fixture.MapRelativePath, fixture.DocumentRelativePath],
            documentCopyManifestPath: fixture.ManifestRelativePath,
            nodeToFileMapPath: fixture.MapRelativePath,
            documentCopySourceRootPath: fixture.PackageRoot);
        instance.Context["target_skill_path"] = fixture.TargetSkillPath;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(tick.Failed);
        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);
        Assert.Contains("does not match package lock", tick.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadDocumentCopyManifest_RejectsMapMissingManifestDocument()
    {
        var fixture = await CreateDocumentCopyManifestFixtureAsync(includeDocumentInMap: false);
        var instance = CreateCheckedInAssetMemoryReadWorkflow(
            checkedInAssets: [fixture.ManifestRelativePath, fixture.MapRelativePath, fixture.DocumentRelativePath],
            documentCopyManifestPath: fixture.ManifestRelativePath,
            nodeToFileMapPath: fixture.MapRelativePath,
            documentCopySourceRootPath: fixture.PackageRoot);
        instance.Context["target_skill_path"] = fixture.TargetSkillPath;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(tick.Failed);
        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);
        Assert.Contains("does not list manifest document path", tick.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadDocumentCopyManifest_RejectsMapPathTraversal()
    {
        var fixture = await CreateDocumentCopyManifestFixtureAsync(mapExtraPath: "../outside.md");
        var instance = CreateCheckedInAssetMemoryReadWorkflow(
            checkedInAssets: [fixture.ManifestRelativePath, fixture.MapRelativePath, fixture.DocumentRelativePath],
            documentCopyManifestPath: fixture.ManifestRelativePath,
            nodeToFileMapPath: fixture.MapRelativePath,
            documentCopySourceRootPath: fixture.PackageRoot);
        instance.Context["target_skill_path"] = fixture.TargetSkillPath;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(tick.Failed);
        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);
        Assert.Contains("outside the target skill root", tick.ErrorMessage, StringComparison.Ordinal);
    }
    [Fact]

    public async Task StartOrAdvanceAsync_MemoryReadDocumentCopyManifest_RejectsCompleteGuideCopies()

    {

        var targetSkillPath = Path.Combine(Path.GetTempPath(), $"techne-loom-document-manifest-{Guid.NewGuid():N}");

        var manifestDirectory = Path.Combine(targetSkillPath, "assets", "so-workflow", "reference");
        var packageLockDirectory = Path.Combine(targetSkillPath, "assets", "so-workflow");

        Directory.CreateDirectory(manifestDirectory);
        await File.WriteAllTextAsync(Path.Combine(packageLockDirectory, "so-package-lock.json"), JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["package_id"] = "Techne.Loom.SkillOrchestrator",
            ["channel"] = "beta",
            ["resolved_version"] = "0.3.253-beta",
        }));

        var manifestRelativePath = "assets/so-workflow/reference/document-copy-manifest.json";

        var manifestPath = Path.Combine(targetSkillPath, manifestRelativePath.Replace('/', Path.DirectorySeparatorChar));

        var manifest = new Dictionary<string, object?>(StringComparer.Ordinal)

        {

            ["schema_version"] = "1",

            ["target_skill_root"] = "target",

            ["target_bound_product"] = "so",

            ["target_bound_channel"] = "beta",

            ["target_bound_version"] = "0.3.253-beta",

            ["documents"] = new object?[]

            {

                new Dictionary<string, object?>(StringComparer.Ordinal)

                {

                    ["target_path"] = "assets/so-workflow/reference/so/so-guide.md",

                    ["source_path"] = "docs/en/guides/so-guide.md",
                    ["source_package_id"] = "Techne.Loom.SkillOrchestrator.Runtime.win-x64",
                    ["source_package_rid"] = "win-x64",
                    ["source_package_path"] = "tools/win-x64/docs/en/guides/so-guide.md",
                    ["content_mode"] = "full-document",

                    ["source_product"] = "so",

                    ["source_channel"] = "beta",

                    ["source_version"] = "0.3.253-beta",

                    ["source_sha256"] = new string('a', 64),

                    ["artifact_origin"] = "verified-copy",

                    ["authority_scope"] = "target-local context only",

                    ["refreshed_by"] = "test",

                },

            },

        };

        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));



        var instance = CreateCheckedInAssetMemoryReadWorkflow(

            checkedInAssets: [manifestRelativePath],

            documentCopyManifestPath: manifestRelativePath);

        instance.Context["target_skill_path"] = targetSkillPath;



        var store = new InMemoryInstanceStore();

        await store.SaveNewAsync(instance);

        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));



        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.True(tick.Failed);

        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);

        Assert.Contains("outside the target-local so reference policy", tick.ErrorMessage, StringComparison.Ordinal);

    }
}
