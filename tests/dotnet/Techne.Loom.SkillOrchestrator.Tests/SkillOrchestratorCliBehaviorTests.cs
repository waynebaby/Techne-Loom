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

public sealed class SkillOrchestratorCliBehaviorTests : SkillOrchestratorBehaviorTestBase
{
    [Fact]
    public async Task CliLs_UsesBoundContextFieldCatalog()
    {
        var repoRoot = FindRepositoryRoot();
        var directory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-ls-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "expected.txt"), "directory fixture");

        try
        {
            var run = await RunCliAsync(repoRoot, $"ls \"{directory}\"");

            Assert.Equal(0, run.ExitCode);
            Assert.Contains("expected.txt", run.StdOut, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadCheckedInAssets_RequiresExplicitRoot()
    {
        var instance = CreateCheckedInAssetMemoryReadWorkflow(assetRootInput: null, assetRootPath: null);

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var engine = new DefaultTaskTrackingEngine(store);
        var service = new DefaultWorkflowTaskTrackingService(engine);

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.True(tick.Failed);
        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);
        Assert.Contains("requires assetRootInput or assetRootPath", tick.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadCheckedInAssets_RejectsAbsolutePaths()
    {
        var targetSkillPath = Path.Combine(Path.GetTempPath(), $"techne-loom-memory-read-{Guid.NewGuid():N}");
        Directory.CreateDirectory(targetSkillPath);
        var skillFile = Path.Combine(targetSkillPath, "SKILL.md");
        await File.WriteAllTextAsync(skillFile, "# Skill\n");

        var instance = CreateCheckedInAssetMemoryReadWorkflow(checkedInAssets: [Path.GetFullPath(skillFile)]);
        instance.Context["target_skill_path"] = targetSkillPath;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var engine = new DefaultTaskTrackingEngine(store);
        var service = new DefaultWorkflowTaskTrackingService(engine);

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.True(tick.Failed);
        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);
        Assert.Contains("does not allow absolute asset path", tick.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartOrAdvanceAsync_MemoryReadCheckedInAssets_RejectsEscapingPaths()
    {
        var targetSkillPath = Path.Combine(Path.GetTempPath(), $"techne-loom-memory-read-{Guid.NewGuid():N}");
        Directory.CreateDirectory(targetSkillPath);

        var instance = CreateCheckedInAssetMemoryReadWorkflow(checkedInAssets: [Path.Combine("..", "outside.md")]);
        instance.Context["target_skill_path"] = targetSkillPath;

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var engine = new DefaultTaskTrackingEngine(store);
        var service = new DefaultWorkflowTaskTrackingService(engine);

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.True(tick.Failed);
        Assert.Equal(WorkflowStatus.Failed, tick.StatusProjection.Status);
        Assert.Contains("escapes asset root", tick.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MermaidVisualizer_UsesOwningStateForChainedTransitions()
    {
        var mermaid = await new MermaidWorkflowInstanceVisualizer().VisualizeToStringAsync(CreateChainedWorkflow());

        Assert.Contains("state.start -->|First| state.mid", mermaid);
        Assert.Contains("state.mid -->|Second| state.done", mermaid);
        Assert.DoesNotContain("state.start -->|Second| state.done", mermaid);
    }

    [Fact]
    public async Task HtmlVisualizer_ShowsSourceToTargetTransitionChain()
    {
        var html = await new HtmlWorkflowInstanceVisualizer().VisualizeToStringAsync(CreateChainedWorkflow());

        Assert.Contains("<td>Start</td><td>First</td><td>Mid</td>", html);
        Assert.Contains("<td>Mid</td><td>Second</td><td>Done</td>", html);
    }

    [Fact]
    public async Task MermaidVisualizer_DoesNotProjectUnownedTransitionsFromStart()
    {
        var mermaid = await new MermaidWorkflowInstanceVisualizer().VisualizeToStringAsync(CreateChainedWorkflow(includeUnownedTransition: true));

        Assert.DoesNotContain("state.start -->|Detached| state.done", mermaid);
        Assert.DoesNotContain("-->|Detached|", mermaid);
    }

    [Fact]
    public async Task MermaidVisualizer_ColorsStatesByStepKindWithoutLosingCurrentHighlight()
    {
        var mermaid = await new MermaidWorkflowInstanceVisualizer().VisualizeToStringAsync(CreateStepKindColorWorkflow());

        Assert.Contains("subgraph legend[Legend]", mermaid);
        Assert.Contains("legend_ai[\"🔎 AI\"]", mermaid);
        Assert.Contains("legend_tool[\"⚙️ Code/Tool\"]", mermaid);
        Assert.Contains("legend_branch[\"❓ Conditional branch\"]", mermaid);
        Assert.Contains("legend_optional[\"💬 Optional user choice\"]", mermaid);
        Assert.Contains("legend_required[\"🚧 Required user input\"]", mermaid);
        Assert.Contains("legend_gate[\"📜 Gate\"]", mermaid);
        Assert.Contains("legend_completion[\"✅ Completion\"]", mermaid);
        Assert.Contains("style legend_ai fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px", mermaid);
        Assert.Contains("style legend_tool fill:#dbeafe,stroke:#2563eb,color:#1e3a8a,stroke-width:1px", mermaid);
        Assert.Contains("style legend_branch fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px", mermaid);
        Assert.Contains("style legend_optional fill:#fef3c7,stroke:#d97706,color:#78350f,stroke-width:1px", mermaid);
        Assert.Contains("style legend_required fill:#fee2e2,stroke:#dc2626,color:#7f1d1d,stroke-width:1px", mermaid);
        Assert.Contains("style legend_gate fill:#f8fafc,stroke:#94a3b8,color:#334155,stroke-width:1px", mermaid);
        Assert.Contains("style legend_completion fill:#dcfce7,stroke:#15803d,color:#14532d,stroke-width:1px", mermaid);
        Assert.Contains("state.ai[\"🔎 AI\"]", mermaid);
        Assert.Contains("state.tool[\"⚙️ Tool\"]", mermaid);
        Assert.Contains("state.optional[\"💬 Optional\"]", mermaid);
        Assert.Contains("state.required[\"🚧 Required\"]", mermaid);
        Assert.Contains("state.done[\"✅ Done\"]", mermaid);
        Assert.Contains("state.gate[\"📜 Gate\"]", mermaid);
        Assert.Contains("state.default[\"Default\"]", mermaid);
        Assert.Contains("style state.gate fill:#f8fafc,stroke:#94a3b8,color:#334155,stroke-width:1px", mermaid);
        Assert.Contains("style state.default fill:#f9fafb,stroke:#9ca3af,color:#374151,stroke-width:1px", mermaid);
        Assert.Contains("style state.ai fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px", mermaid);
        Assert.Contains("style state.tool fill:#dbeafe,stroke:#2563eb,color:#1e3a8a,stroke-width:1px", mermaid);
        Assert.Contains("style state.optional fill:#fef3c7,stroke:#d97706,color:#78350f,stroke-width:1px", mermaid);
        Assert.Contains("style state.required fill:#fee2e2,stroke:#dc2626,color:#7f1d1d,stroke-width:1px", mermaid);
        Assert.Contains("style state.done fill:#dcfce7,stroke:#15803d,color:#14532d,stroke-width:1px", mermaid);
        Assert.Contains("style state.ai stroke:#ea580c,stroke-width:3px", mermaid);
    }

    [Fact]
    public async Task MermaidVisualizer_UsesBranchColorForNonUserOwnedConditionBranch()
    {
        var mermaid = await new MermaidWorkflowInstanceVisualizer().VisualizeToStringAsync(CreateGenericBranchColorWorkflow());

        Assert.Contains("state.branch[\"❓ Branch\"]", mermaid);
        Assert.Contains("style state.branch fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px", mermaid);
    }

    [Fact]
    public async Task MermaidVisualizer_GroupsStatesIntoWorkflowPhaseSwimlanes()
    {
        var mermaid = await new MermaidWorkflowInstanceVisualizer().VisualizeToStringAsync(CreateWorkflowPhaseWorkflow());

        Assert.Contains("subgraph phase_intake[\"Intake\"]", mermaid);
        Assert.Contains("subgraph phase_planning[\"Planning\"]", mermaid);
        Assert.Contains("subgraph phase_review[\"Review\"]", mermaid);
        Assert.Contains("state.intake[\"📜 Intake\"]", mermaid);
        Assert.Contains("state.plan[\"🔎 Plan\"]", mermaid);
        Assert.Contains("state.review[\"✅ Review\"]", mermaid);
    }

    [Fact]
    public async Task MermaidVisualizer_UsesDistinctPhaseGroupIdsWhenPhaseNamesNormalizeToSameId()
    {
        var mermaid = await new MermaidWorkflowInstanceVisualizer().VisualizeToStringAsync(CreateWorkflowPhaseCollisionWorkflow());

        Assert.Contains("subgraph phase_plan_a[\"Plan A\"]", mermaid);
        Assert.Contains("subgraph phase_plan_a_1[\"Plan-A\"]", mermaid);
        Assert.Contains("subgraph phase_plan_a_2[\"Plan/A\"]", mermaid);
    }

    [Fact]
    public void SkillWorkflowAnalyzer_ReportsBranchesLoopsInputsOutputsAndSeams()
    {
        var report = new SkillWorkflowAnalyzer().Analyze(CreateAnalysisWorkflow());

        Assert.Equal(3, report.StateCount);
        Assert.Equal(3, report.TransitionCount);
        Assert.Contains(report.Branches, branch => branch.StateId == "state.start" && branch.IsSwitchLike);
        Assert.Contains(report.Loops, loop => loop.TransitionId == "transition.loop" && loop.IsSelfLoop);
        Assert.Contains("plan_confirmation", report.RequestedInputFields);
        Assert.Contains("workflow_json", report.PublishedOutputFamilies);
        Assert.Contains(report.UserSeams, seam => seam.TransitionId == "transition.ask");
        Assert.Contains(report.RuntimeSeams, seam => seam.TransitionId == "transition.wait");
        Assert.Contains("gate.workflow", report.GateIds);
        Assert.Contains("plan_confirmation", report.DeclaredUserOwnedFields);
        Assert.Contains("workflow_file", report.ReservedRuntimeOwnedFields);
        Assert.Contains(report.Branches, branch => branch.GuardExpressions.Contains("requiresUserChoice"));
        Assert.Contains(report.NodeArtifactMap, mapping => mapping.NodeId == "transition.ask" && mapping.OutputFamilies.Contains("workflow_json") && mapping.GateIds.Contains("gate.workflow"));
        Assert.True(report.HasTuringCompleteControlRisk);
    }

    [Fact]
    public async Task CliRun_CommandLineWorkflow_EmitsWrappedExecAndEscapesOutput()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-cli-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateEscapedCommandWorkflow()));

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{GetCliAssemblyPath()}\" run --workflow-file \"{workflowPath}\"",
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

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("<wrapped_exec>", stdout);
        Assert.Contains($"<commandline>{GetEscapedCommandPrefix()}", stdout);
        Assert.Contains("[stdout] &lt;danger&gt;", stdout);
        Assert.Contains("[stderr] error-line", stdout);
        Assert.Contains("<so_property>", stdout);
        Assert.DoesNotContain("<danger>", stdout);
        Assert.True(string.IsNullOrWhiteSpace(stderr));
    }

    [Fact]
    public async Task CliRun_InvalidCommand_EmitsStableErrorProperty()
    {
        var repoRoot = FindRepositoryRoot();
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{GetCliAssemblyPath()}\" nope",
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

        Assert.Equal(2, process.ExitCode);
        Assert.Contains("<so_property>", stdout);
        Assert.Contains("\"type\":\"error\"", stdout);
        Assert.DoesNotContain("Unhandled exception", stdout);
        Assert.DoesNotContain("Stack Trace", stdout);
        Assert.True(string.IsNullOrWhiteSpace(stderr));
    }

    [Fact]
    public async Task CliRun_NoProgressWorkflow_EmitsBoundaryInsteadOfResult()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-noprogress-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateNoProgressWorkflow()));

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{GetCliAssemblyPath()}\" run --workflow-file \"{workflowPath}\"",
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

        Assert.Equal(3, process.ExitCode);
        Assert.Contains("<so_property>", stdout);
        Assert.Contains("\"type\":\"boundary\"", stdout);
        Assert.DoesNotContain("\"type\":\"result\"", stdout);
        Assert.Contains("\"status\":\"blocked\"", stdout);
        Assert.True(string.IsNullOrWhiteSpace(stderr));
        using var boundaryEnvelope = ReadFinalSoEnvelope(stdout);
        var boundaryPayload = boundaryEnvelope.RootElement.GetProperty("payload");
        var boundaryMustShowFiles = boundaryPayload.GetProperty("must_show_to_user_files").EnumerateArray().Select(static item => item.GetString()).ToArray();
        Assert.Contains(boundaryMustShowFiles, static path => path is not null && path.EndsWith("workflow.mermaid.md", StringComparison.Ordinal));
        Assert.Contains(boundaryMustShowFiles, static path => path is not null && path.EndsWith("workflow.html", StringComparison.Ordinal));
        Assert.Contains(boundaryMustShowFiles, static path => path is not null && path.EndsWith("workflow.analysis.json", StringComparison.Ordinal));
        Assert.Contains("SO workflow is blocked", boundaryPayload.GetProperty("workflow_location_summary").GetString());

        var persistedWorkflow = await File.ReadAllTextAsync(workflowPath);
        Assert.Contains("\"status\": \"running\"", persistedWorkflow);

        var eventsPath = workflowPath + ".events.jsonl";
        Assert.True(File.Exists(eventsPath));
        var events = await File.ReadAllTextAsync(eventsPath);
        Assert.Contains("Start", events);
    }

    [Fact]
    public async Task CliResume_SnakeCaseEnvelope_WithNestedPayload_CompletesWorkflow()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-resume-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var firstRun = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\"");
        Assert.Equal(3, firstRun.ExitCode);
        Assert.Contains("\"type\":\"boundary\"", firstRun.StdOut);
        using var boundaryEnvelope = ReadFinalSoEnvelope(firstRun.StdOut);
        var boundaryFields = boundaryEnvelope.RootElement.GetProperty("payload").GetProperty("context_fields");
        var approvalField = Assert.Single(boundaryFields.EnumerateArray());
        Assert.Equal("review.approved", approvalField.GetProperty("path").GetString());
        Assert.Equal("boolean", approvalField.GetProperty("type").GetString());
        Assert.Equal("user", approvalField.GetProperty("owner").GetString());
        Assert.Equal(2, approvalField.GetProperty("suggested_values").GetArrayLength());
        Assert.Contains("transition:transition.check/succeedExpression", approvalField.GetProperty("consumer_expressions").EnumerateArray().Select(item => item.GetProperty("location").GetString()));
        Assert.Contains("user", approvalField.GetProperty("host_decision_guidance").GetString(), StringComparison.OrdinalIgnoreCase);

        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-resume-payload-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(resultFile, "{" +
            "\"transition_id\":\"transition.ask\"," +
            "\"correlation_key\":null," +
            "\"payload\":{\"review\":{\"approved\":true},\"unexpected\":\"must be filtered\"}" +
            "}");

        var resumeRun = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowPath}\" --result-file \"{resultFile}\"");
        Assert.True(resumeRun.ExitCode == 0, $"{resumeRun.StdOut}{Environment.NewLine}{resumeRun.StdErr}");
        Assert.Contains("\"type\":\"result\"", resumeRun.StdOut);
        Assert.Contains("\"status\":\"completed\"", resumeRun.StdOut);
        using var resultEnvelope = ReadFinalSoEnvelope(resumeRun.StdOut);
        var resultPayload = resultEnvelope.RootElement.GetProperty("payload");
        var contextFieldWarnings = resultPayload.GetProperty("context_field_warnings").EnumerateArray().Select(static warning => warning.GetString()).ToArray();
        Assert.Contains("Filtered undeclared resume payload field 'unexpected'.", contextFieldWarnings);
        Assert.False(resultPayload.GetProperty("context").TryGetProperty("unexpected", out _));
        var resultMustShowFiles = resultPayload.GetProperty("must_show_to_user_files").EnumerateArray().Select(static item => item.GetString()).ToArray();
        Assert.Contains(resultMustShowFiles, static path => path is not null && path.EndsWith("workflow.mermaid.md", StringComparison.Ordinal));
        Assert.Contains(resultMustShowFiles, static path => path is not null && path.EndsWith("workflow.html", StringComparison.Ordinal));
        Assert.Contains(resultMustShowFiles, static path => path is not null && path.EndsWith("workflow.analysis.json", StringComparison.Ordinal));
        Assert.Contains("SO workflow is completed", resultPayload.GetProperty("workflow_location_summary").GetString());
    }

    [Fact]
    public async Task CliStatus_ProjectsPersistedFanoutCancellationSignals()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-status-cancellation-{Guid.NewGuid():N}.json");
        var workflow = CreateResumeWorkflow();
        workflow.FanoutCancellationSignals.Add(new FanoutCancellationSignal(
            $"{workflow.InstanceId}:wait-cancelled",
            workflow.InstanceId,
            "group.parallel",
            "transition.slow",
            "wait-cancelled",
            true,
            true,
            "fanout_completed",
            DateTimeOffset.UtcNow));
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(workflow));

        var statusRun = await RunCliAsync(repoRoot, $"status --workflow-file \"{workflowPath}\"");
        Assert.Equal(0, statusRun.ExitCode);
        using var statusEnvelope = ReadFinalSoEnvelope(statusRun.StdOut);
        var signals = statusEnvelope.RootElement.GetProperty("payload").GetProperty("fanout_cancellation_signals");
        var signal = Assert.Single(signals.EnumerateArray().ToArray());
        Assert.Equal("wait-cancelled", signal.GetProperty("waitId").GetString());
        Assert.True(signal.GetProperty("lateResultsIgnored").GetBoolean());
    }

    [Fact]
    public async Task CliResume_MalformedEnvelope_PreservesWorkflowContextInErrorPayload()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-malformed-resume-{Guid.NewGuid():N}.json");
        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-malformed-resume-payload-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));
        await File.WriteAllTextAsync(resultFile, "{\"correlation_key\":\"abc\",\"payload\":{\"review\":{\"approved\":true}}}");

        var resumeRun = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowPath}\" --result-file \"{resultFile}\"");
        Assert.Equal(2, resumeRun.ExitCode);
        Assert.Contains("\"type\":\"error\"", resumeRun.StdOut);

        using var errorEnvelope = ReadFinalSoEnvelope(resumeRun.StdOut);
        var errorPayload = errorEnvelope.RootElement.GetProperty("payload");
        Assert.Equal(Path.GetFullPath(workflowPath), errorPayload.GetProperty("workflow_file").GetString());
        Assert.Equal(Path.GetFullPath(workflowPath) + ".events.jsonl", errorPayload.GetProperty("event_log_file").GetString());
        Assert.Equal("failed", errorPayload.GetProperty("status").GetString());
        Assert.Contains("resume", errorPayload.GetProperty("workflow_location_summary").GetString());
        var errorMustShowFiles = errorPayload.GetProperty("must_show_to_user_files").EnumerateArray().Select(static item => item.GetString()).ToArray();
        Assert.Contains(LocalFileUrl.FromPath(Path.GetFullPath(workflowPath)), errorMustShowFiles);
        Assert.Contains(LocalFileUrl.FromPath(Path.GetFullPath(resultFile)), errorMustShowFiles);
    }

    [Fact]
    public async Task CliRun_ContextFile_WithNestedObject_AllowsDottedPathEvaluation()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-context-{Guid.NewGuid():N}.json");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-context-payload-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateContextWorkflow()));
        await File.WriteAllTextAsync(contextFile, "{\"review\":{\"approved\":true}}");

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\" --context-file \"{contextFile}\"");
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("\"type\":\"result\"", run.StdOut);
        Assert.Contains("\"status\":\"completed\"", run.StdOut);
    }

    [Fact]
    public async Task CliRun_SelfLoopWorkflow_FailsInsteadOfHanging()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-self-loop-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateSelfLoopWorkflow()));

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\"");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("Execution step budget exceeded.", run.StdOut);
    }

    [Fact]
    public async Task CliPlanner_IsRejectedAsUnknownCommand()
    {
        var repoRoot = FindRepositoryRoot();
        var run = await RunCliAsync(repoRoot, "planner");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("Unknown command", run.StdOut);
        Assert.Contains("planner", run.StdOut);
    }

    [Fact]
    public async Task CliHelp_ListsDirectApphostCommands()
    {
        var repoRoot = FindRepositoryRoot();
        var noArgsRun = await RunCliAsync(repoRoot, string.Empty);
        Assert.Equal(1, noArgsRun.ExitCode);
        Assert.Contains("--max-bytes <n>", noArgsRun.StdErr);
        Assert.Contains("262,144 bytes (256 KiB)", noArgsRun.StdErr);
        Assert.Contains("--max-bytes 524288", noArgsRun.StdErr);

        var run = await RunCliAsync(repoRoot, "--help");
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Usage: so[.exe]", run.StdOut);
        Assert.Contains("--guide", run.StdOut);
        Assert.Contains("--help", run.StdOut);
        Assert.Contains("--max-bytes <n>", run.StdOut);
        Assert.Contains("inspect-contract-fragment --workflow-file <path> or --contract-file <path> --json-pointer <pointer> [--max-bytes <n>] [--max-array-items <n>] [--max-object-properties <n>] [--max-depth <n>]", run.StdOut);
        Assert.Contains("262,144 bytes (256 KiB)", run.StdOut);
        Assert.Contains("--max-bytes 524288", run.StdOut);
        Assert.Contains("mcp stdio", run.StdOut);
        Assert.Contains("--patch --patch-content-file <path>", run.StdOut);
        Assert.Contains("--patch-target <path>", run.StdOut);
        Assert.Contains("--from-line <n>", run.StdOut);
        Assert.Contains("--to-line <n>", run.StdOut);
        Assert.Contains("--schema-demo-output <directory>", run.StdOut);
        Assert.Contains("compile --workflow-file <path>", run.StdOut);
        Assert.Contains("run --workflow-file <path>", run.StdOut);
        Assert.Contains("resume --workflow-file <path>", run.StdOut);
        Assert.Contains("status --workflow-file <path>", run.StdOut);
        Assert.Contains("inspect-workflow --workflow-file <path>", run.StdOut);
        Assert.Contains("inspect-workflow-fragment --workflow-file <path>", run.StdOut);
        Assert.Contains("returns summary metadata", run.StdOut);
        Assert.Contains("bounded fragment", run.StdOut);
        Assert.Contains("inspect-events --workflow-file <path>", run.StdOut);
        Assert.Contains("ls <path>", run.StdOut);
        Assert.Contains("inspect-contract-fragment", run.StdOut);
        Assert.DoesNotContain("dotnet so.dll", run.StdOut);
        Assert.DoesNotContain("planner", run.StdOut);
    }

    [Theory]
    [InlineData("--patch", "--patch-content-file")]
    [InlineData("compile", "--workflow-file")]
    [InlineData("run", "--workflow-file")]
    [InlineData("resume", "--workflow-file")]
    [InlineData("status", "--workflow-file")]
    [InlineData("inspect-workflow", "--workflow-file")]
    [InlineData("inspect-workflow-fragment", "--workflow-file")]
    [InlineData("inspect-events", "--workflow-file")]
    public async Task CliRequiredDotnetSoDllParameters_MissingOptionsReturnStableError(string command, string requiredOption)
    {
        var repoRoot = FindRepositoryRoot();
        var run = await RunCliAsync(repoRoot, command);
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("<so_property>", run.StdOut);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("Missing required option", run.StdOut);
        Assert.Contains(requiredOption, run.StdOut);
    }

    [Fact]
    public async Task CliPatch_ReplacesRequestedLineRange()
    {
        var repoRoot = FindRepositoryRoot();
        var targetFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-patch-target-{Guid.NewGuid():N}.txt");
        var patchFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-patch-content-{Guid.NewGuid():N}.txt");

        await File.WriteAllTextAsync(targetFile, "line1\nline2\nline3\n");
        await File.WriteAllTextAsync(patchFile, "replacement\n");

        var run = await RunCliAsync(repoRoot, $"--patch --patch-content-file \"{patchFile}\" --patch-target \"{targetFile}\" --from-line 2 --to-line 9");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("\"applied_from_line\":2", run.StdOut);
        Assert.Contains("\"applied_to_line\":3", run.StdOut);
        Assert.Equal("line1\nreplacement\n", await File.ReadAllTextAsync(targetFile));
    }

    [Fact]
    public async Task CliPatch_InvalidIntegerOption_ReturnsStableErrorAndDoesNotModifyFile()
    {
        var repoRoot = FindRepositoryRoot();
        var targetFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-patch-invalid-target-{Guid.NewGuid():N}.txt");
        var patchFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-patch-invalid-content-{Guid.NewGuid():N}.txt");

        await File.WriteAllTextAsync(targetFile, "line1\nline2\n");
        await File.WriteAllTextAsync(patchFile, "replacement\n");

        var run = await RunCliAsync(repoRoot, $"--patch --patch-content-file \"{patchFile}\" --patch-target \"{targetFile}\" --from-line abc --to-line 2");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("must be a valid integer", run.StdOut);
        Assert.Equal("line1\nline2\n", await File.ReadAllTextAsync(targetFile));
    }

    [Fact]
    public async Task CliGuide_ReturnsInstalledEnglishBundlePaths()
    {
        var repoRoot = FindRepositoryRoot();
        var run = await RunCliAsync(repoRoot, "--guide");

        Assert.Equal(0, run.ExitCode);
        using var document = JsonDocument.Parse(run.StdOut);
        var payload = document.RootElement;
        var version = payload.GetProperty("version").GetString() ?? throw new InvalidOperationException("Guide JSON did not contain version.");
        var docsRoot = payload.GetProperty("docs_root").GetString() ?? throw new InvalidOperationException("Guide JSON did not contain docs_root.");
        var guidePath = payload.GetProperty("guide_path").GetString() ?? throw new InvalidOperationException("Guide JSON did not contain guide_path.");

        Assert.False(string.IsNullOrWhiteSpace(version));
        Assert.True(Path.IsPathFullyQualified(docsRoot));
        Assert.True(Path.IsPathFullyQualified(guidePath));
        Assert.True(Directory.Exists(docsRoot));
        Assert.True(File.Exists(guidePath));
        Assert.StartsWith(Path.GetFullPath(docsRoot), Path.GetFullPath(guidePath), StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(docsRoot, "zh-cn")));

        var guide = await File.ReadAllTextAsync(guidePath);
        Assert.Contains($"Version: {version}", guide);
        Assert.Contains($"Build: published package {version}", guide);
        Assert.InRange(guide.Split(["\r\n", "\n"], StringSplitOptions.None).Length, 1, 200);
        var flowPath = Path.Combine(docsRoot, "guides", "so-guide-flow.md");
        var referencePath = Path.Combine(docsRoot, "guides", "so-guide-reference.md");
        Assert.True(File.Exists(flowPath));
        Assert.True(File.Exists(referencePath));
        var reference = await File.ReadAllTextAsync(referencePath);
        var referenceContractsPath = Path.Combine(docsRoot, "guides", "so-guide-reference-contracts.md");
        Assert.True(File.Exists(referenceContractsPath));
        var referenceContracts = await File.ReadAllTextAsync(referenceContractsPath);
        Assert.Contains("direct SO apphost", referenceContracts);
        var behaviorPath = Path.Combine(docsRoot, "guides", "so-guide-reference-behavior.md");
        Assert.True(File.Exists(behaviorPath));
        Assert.Contains("same persisted runtime copy", await File.ReadAllTextAsync(behaviorPath));
    }

    [Fact]
    public async Task CliCompile_WorkflowFile_RejectsProjectionLiteralTypeMismatch()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-projection-type-mismatch-{Guid.NewGuid():N}.json");
        var instance = CreateContextWorkflow();
        instance.Nodes["transition.check"] = new CommandTransition
        {
            Id = "transition.check",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.StateUpdate,
            WorkflowPhase = "Evaluation",
            GuardExpression = new ExpressionDefinition { Kind = "predicate", Source = "true", ResultType = "bool" },
            SucceedExpression = new ExpressionDefinition { Kind = "predicate", Source = "true", ResultType = "bool" },
            Command = new CommandInvocation
            {
                Name = "state.update",
                Kind = CommandInvocationKind.NativeCode,
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["review.approved"] = "false" },
                },
            },
        };
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(instance));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("LOOM.CONTEXT.PROJECTION.VALUE_MISMATCH", run.StdOut, StringComparison.Ordinal);
        Assert.Contains("review.approved", run.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CliCompile_ExistingWorkflowFile_ValidatesWithoutRedrafting()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-audit-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("\"status\": \"readyToStart\"", await File.ReadAllTextAsync(workflowFile));
        Assert.DoesNotContain("\"status\": \"drafting\"", await File.ReadAllTextAsync(workflowFile));
        Assert.Contains("Validation artifacts:", run.StdErr);
        Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories).Single()));
        Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.html", SearchOption.AllDirectories).Single()));
        Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.json", SearchOption.AllDirectories).Single()));
    }

    [Theory]
    [InlineData("--guide --lang zh-cn")]
    [InlineData("--guide --help")]
    [InlineData("--guide --section Overview")]
    [InlineData("--guide --export guide.md")]
    public async Task CliGuide_LegacyArguments_AreRejected(string command)
    {
        var repoRoot = FindRepositoryRoot();
        var run = await RunCliAsync(repoRoot, command);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("accepts no additional arguments", run.StdOut);
    }
    [Fact]
    public async Task CliCompile_ReadOnlyWorkflowFile_SucceedsWithoutMutatingInput()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-readonly-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-readonly-audit-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));
        File.SetAttributes(workflowFile, FileAttributes.ReadOnly);

        try
        {
            var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
            Assert.Equal(0, run.ExitCode);
            Assert.Contains("Validation artifacts:", run.StdErr);
        }
        finally
        {
            File.SetAttributes(workflowFile, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task CliCompile_PreexistingAuditArtifacts_FailsWithoutOverwritingFiles()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-existing-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-existing-audit-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var firstRun = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(0, firstRun.ExitCode);

        var secondRun = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(2, secondRun.ExitCode);
        Assert.Contains("\"type\":\"error\"", secondRun.StdOut);
        Assert.Contains("Refusing to overwrite existing audit artifacts", secondRun.StdOut);
        Assert.Contains("workflow.html", secondRun.StdOut);
        Assert.Contains("Choose a different audit output root", secondRun.StdOut);
    }

    [Fact]
    public async Task CliCompile_DefaultAuditRoot_DoesNotCollideAcrossCliInvocations()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-default-audit-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var firstRun = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");
        var secondRun = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(0, firstRun.ExitCode);
        Assert.Equal(0, secondRun.ExitCode);
        Assert.DoesNotContain("Refusing to overwrite existing audit artifacts", secondRun.StdOut);
    }

    [Fact]
    public async Task CliCompile_AuditOutputInsideSkillFolder_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-skill-audit-{Guid.NewGuid():N}.json");
        var skillRoot = CreateSkillRoot();
        var auditDirectory = Path.Combine(skillRoot, "runtime-audit");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("skill-owned directory", run.StdOut);
        Assert.Contains("--audit-output", run.StdOut);
        Assert.False(Directory.Exists(auditDirectory));
    }

    [Fact]
    public async Task CliCompile_WithDescriptionFile_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var descriptionFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-description-{Guid.NewGuid():N}.md");
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-description-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(descriptionFile, "This should be rejected.");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --description-file \"{descriptionFile}\"");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("Option", run.StdOut);
        Assert.Contains("--description-file", run.StdOut);
        Assert.Contains("compile", run.StdOut);
    }

    [Fact]
    public async Task CliCompile_InvalidWorkflowStructure_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-invalid-compile-{Guid.NewGuid():N}.json");
        var invalid = CreateResumeWorkflow();
        invalid.StartNodeId = "state.missing";
        invalid.CurrentNodeId = "state.missing";
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(invalid));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("startNodeId", run.StdOut);
        Assert.Contains("state.missing", run.StdOut);
    }

    [Fact]
    public async Task CliCompile_WorkflowPayload_GeneratesConnectedMermaidGraph()
    {
        var repoRoot = FindRepositoryRoot();
        var sourceWorkflowFile = GetWorkflowPayloadPath(repoRoot);
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-payload-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-payload-audit-{Guid.NewGuid():N}");
        var payloadWorkflow = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(sourceWorkflowFile));
        EnsureWorkflowPhases(payloadWorkflow, "Payload");
        payloadWorkflow.ContractBinding = CreateTestContextContractBinding();
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(payloadWorkflow));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
        Assert.True(run.ExitCode == 0, $"{run.StdOut}{Environment.NewLine}{run.StdErr}");

        var mermaidFile = Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories).Single();
    AssertFileStartsWithMermaidFence(mermaidFile);
        var mermaid = await File.ReadAllTextAsync(mermaidFile);
        var instance = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile));

        Assert.StartsWith($"```mermaid{Environment.NewLine}{Environment.NewLine}", mermaid);
        Assert.Contains($"{Environment.NewLine}{Environment.NewLine}```{Environment.NewLine}{Environment.NewLine}## Workflow Business Summary / 工作流业务说明", mermaid);
        Assert.Contains("flowchart TD", mermaid);
        AssertMermaidStateGraphConnected(mermaid, instance);
    }

    [Fact]
    public async Task CliRun_StaleEventSidecarIsReplaced()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-stale-sidecar-{Guid.NewGuid():N}.json");
        var eventsPath = workflowPath + ".events.jsonl";
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));
        await File.WriteAllTextAsync(eventsPath, "{\"nodeId\":\"stale-instance\",\"nodeType\":\"state\",\"status\":\"started\"}" + Environment.NewLine);

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\"");

        Assert.Equal(3, run.ExitCode);
        var events = await File.ReadAllTextAsync(eventsPath);
        Assert.DoesNotContain("stale-instance", events, StringComparison.Ordinal);
        Assert.Contains("state.start", events, StringComparison.Ordinal);
    }
    [Fact]
    public async Task CliRun_WithAuditOutput_EmitsAuditArtifactLinks()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-audit-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-audit-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"techne loom so workspace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\" --audit-output \"{auditDirectory}\" --workspace-root \"{workspaceRoot}\"");
        Assert.Equal(3, run.ExitCode);
        using var envelope = ReadFinalSoEnvelope(run.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");
        var audit = payload.GetProperty("audit_artifacts");
        var delivery = audit.GetProperty("mermaid_delivery");
        Assert.Equal(Path.GetFullPath(auditDirectory), audit.GetProperty("output_root").GetString());
        Assert.Equal("workspace_mirror", delivery.GetProperty("status").GetString());
        Assert.Equal("fresh", delivery.GetProperty("generation_status").GetString());
        Assert.True(delivery.GetProperty("artifact_generated").GetBoolean());
        Assert.True(delivery.GetProperty("link_resolvable").GetBoolean());
        Assert.False(delivery.GetProperty("visual_preview_rendered").GetBoolean());
        Assert.False(delivery.GetProperty("card_display_available").GetBoolean());
        Assert.Equal(JsonValueKind.Null, delivery.GetProperty("card_fallback").ValueKind);
        var mermaidFileUrl = audit.GetProperty("mermaid_file").GetString()!;
        var htmlFileUrl = audit.GetProperty("html_file").GetString()!;
        var analysisFileUrl = audit.GetProperty("analysis_file").GetString()!;
        var dataflowFileUrl = audit.GetProperty("dataflow_file").GetString()!;
        var workspaceMermaidFileUrl = delivery.GetProperty("workspace_mermaid_file").GetString()!;
        var workspaceHtmlFileUrl = delivery.GetProperty("workspace_html_file").GetString()!;
        var workspaceAnalysisFileUrl = delivery.GetProperty("workspace_analysis_file").GetString()!;
        var workspaceDataflowFileUrl = delivery.GetProperty("workspace_dataflow_file").GetString()!;
        var mermaidFile = LocalFileUrl.ToPath(mermaidFileUrl);
        var htmlFile = LocalFileUrl.ToPath(htmlFileUrl);
        var analysisFile = LocalFileUrl.ToPath(analysisFileUrl);
        var dataflowFile = LocalFileUrl.ToPath(dataflowFileUrl);
        var workspaceMermaidFile = LocalFileUrl.ToPath(workspaceMermaidFileUrl);
        var workspaceHtmlFile = LocalFileUrl.ToPath(workspaceHtmlFileUrl);
        var workspaceAnalysisFile = LocalFileUrl.ToPath(workspaceAnalysisFileUrl);
        var workspaceDataflowFile = LocalFileUrl.ToPath(workspaceDataflowFileUrl);
        Assert.StartsWith("file:///", mermaidFileUrl, StringComparison.Ordinal);
        Assert.Contains("%20", workspaceMermaidFileUrl, StringComparison.Ordinal);
        Assert.True(File.Exists(mermaidFile));
        Assert.True(File.Exists(htmlFile));
        Assert.True(File.Exists(analysisFile));
        Assert.True(File.Exists(dataflowFile));
        Assert.True(File.Exists(audit.GetProperty("workflow_backup_file").GetString()));
        Assert.True(File.Exists(workspaceMermaidFile));
        Assert.True(File.Exists(workspaceHtmlFile));
        Assert.True(File.Exists(workspaceAnalysisFile));
        Assert.True(File.Exists(workspaceDataflowFile));
        Assert.Equal(Path.GetRelativePath(workspaceRoot, workspaceMermaidFile).Replace(Path.DirectorySeparatorChar, '/'), delivery.GetProperty("workspace_relative_mermaid_file").GetString());
        Assert.Equal(Path.GetRelativePath(workspaceRoot, workspaceHtmlFile).Replace(Path.DirectorySeparatorChar, '/'), delivery.GetProperty("workspace_relative_html_file").GetString());
        Assert.Equal(Path.GetRelativePath(workspaceRoot, workspaceAnalysisFile).Replace(Path.DirectorySeparatorChar, '/'), delivery.GetProperty("workspace_relative_analysis_file").GetString());
        Assert.Equal(Path.GetRelativePath(workspaceRoot, workspaceDataflowFile).Replace(Path.DirectorySeparatorChar, '/'), delivery.GetProperty("workspace_relative_dataflow_file").GetString());
        Assert.Equal(delivery.GetProperty("analysis_sha256").GetString(), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(analysisFile))).ToLowerInvariant());
        Assert.Equal(delivery.GetProperty("dataflow_sha256").GetString(), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(dataflowFile))).ToLowerInvariant());
        Assert.True((await File.ReadAllBytesAsync(mermaidFile)).SequenceEqual(await File.ReadAllBytesAsync(workspaceMermaidFile)));
        Assert.True((await File.ReadAllBytesAsync(htmlFile)).SequenceEqual(await File.ReadAllBytesAsync(workspaceHtmlFile)));
        Assert.True((await File.ReadAllBytesAsync(analysisFile)).SequenceEqual(await File.ReadAllBytesAsync(workspaceAnalysisFile)));
        Assert.True((await File.ReadAllBytesAsync(dataflowFile)).SequenceEqual(await File.ReadAllBytesAsync(workspaceDataflowFile)));
        var mustShowFiles = payload.GetProperty("must_show_to_user_files").EnumerateArray().Select(static item => item.GetString()).ToArray();
        Assert.Equal(workspaceMermaidFileUrl, mustShowFiles[0]);
        Assert.Equal(workspaceHtmlFileUrl, mustShowFiles[1]);
        Assert.Equal(workspaceAnalysisFileUrl, mustShowFiles[2]);
        Assert.Equal(workspaceDataflowFileUrl, mustShowFiles[3]);
    }

    [Fact]
    public async Task CliRun_WhenWorkspaceMirrorFails_UsesRuntimePathsForFourArtifacts()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-mirror-fallback-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-mirror-fallback-audit-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-so-mirror-fallback-workspace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "temp"), "not a directory");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\" --audit-output \"{auditDirectory}\" --workspace-root \"{workspaceRoot}\"");

        Assert.Equal(3, run.ExitCode);
        using var envelope = ReadFinalSoEnvelope(run.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");
        var audit = payload.GetProperty("audit_artifacts");
        var delivery = audit.GetProperty("mermaid_delivery");
        Assert.Equal("runtime_path_only", delivery.GetProperty("status").GetString());
        Assert.Equal("failed", delivery.GetProperty("mirror_status").GetString());
        Assert.False(delivery.GetProperty("link_resolvable").GetBoolean());
        Assert.True(delivery.GetProperty("artifact_generated").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(delivery.GetProperty("mirror_error").GetString()));
        var mermaidFileUrl = audit.GetProperty("mermaid_file").GetString()!;
        var htmlFileUrl = audit.GetProperty("html_file").GetString()!;
        var analysisFileUrl = audit.GetProperty("analysis_file").GetString()!;
        var dataflowFileUrl = audit.GetProperty("dataflow_file").GetString()!;
        var mermaidFile = LocalFileUrl.ToPath(mermaidFileUrl);
        var htmlFile = LocalFileUrl.ToPath(htmlFileUrl);
        var analysisFile = LocalFileUrl.ToPath(analysisFileUrl);
        var dataflowFile = LocalFileUrl.ToPath(dataflowFileUrl);
        Assert.True(File.Exists(mermaidFile));
        Assert.True(File.Exists(htmlFile));
        Assert.True(File.Exists(analysisFile));
        Assert.True(File.Exists(dataflowFile));
        Assert.Equal(JsonValueKind.Null, delivery.GetProperty("workspace_mermaid_file").ValueKind);
        Assert.Equal(JsonValueKind.Null, delivery.GetProperty("workspace_html_file").ValueKind);
        Assert.Equal(JsonValueKind.Null, delivery.GetProperty("workspace_analysis_file").ValueKind);
        Assert.Equal(JsonValueKind.Null, delivery.GetProperty("workspace_dataflow_file").ValueKind);
        var mustShowFiles = payload.GetProperty("must_show_to_user_files").EnumerateArray().Select(static item => item.GetString()).ToArray();
        Assert.Equal(mermaidFileUrl, mustShowFiles[0]);
        Assert.Equal(htmlFileUrl, mustShowFiles[1]);
        Assert.Equal(analysisFileUrl, mustShowFiles[2]);
        Assert.Equal(dataflowFileUrl, mustShowFiles[3]);
    }

    [Fact]
    public async Task CliRun_ProgressPayload_EmitsCurrentWorkflowRenderPaths()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-progress-{Guid.NewGuid():N}.json");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-progress-context-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-progress-audit-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateContextWorkflow()));
        await File.WriteAllTextAsync(contextFile, "{\"review\":{\"approved\":true}}");

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\" --context-file \"{contextFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("\"type\":\"progress\"", run.StdOut);
        using var progressEnvelope = ReadSoEnvelope(run.StdOut);
        var payload = progressEnvelope.RootElement.GetProperty("payload");
        var mustShowFiles = payload.GetProperty("must_show_to_user_files").EnumerateArray().Select(static item => item.GetString()).ToArray();
        Assert.Contains(mustShowFiles, static path => path is not null && path.EndsWith("workflow.mermaid.md", StringComparison.Ordinal));
        Assert.Contains(mustShowFiles, static path => path is not null && path.EndsWith("workflow.html", StringComparison.Ordinal));
        Assert.Contains(mustShowFiles, static path => path is not null && path.EndsWith("workflow.analysis.json", StringComparison.Ordinal));
        Assert.Contains("SO workflow is", payload.GetProperty("workflow_location_summary").GetString());
        Assert.Contains("workflow.mermaid.md", run.StdOut);
        Assert.Contains("workflow.html", run.StdOut);
        Assert.Contains("workflow.analysis.json", run.StdOut);
        Assert.True(Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories).Length > 0);
        Assert.True(Directory.GetFiles(auditDirectory, "workflow.html", SearchOption.AllDirectories).Length > 0);
        Assert.True(Directory.GetFiles(auditDirectory, "workflow.analysis.json", SearchOption.AllDirectories).Length > 0);
    }

    [Fact]
    public async Task CliRun_WorkflowFileInsideSkillFolder_IsRejectedWithoutWritingEvents()
    {
        var repoRoot = FindRepositoryRoot();
        var skillRoot = CreateSkillRoot();
        var workflowDirectory = Path.Combine(skillRoot, "assets", "so-workflow");
        Directory.CreateDirectory(workflowDirectory);
        var workflowPath = Path.Combine(workflowDirectory, "workflow.current.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\"");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("skill-owned directory", run.StdOut);
        Assert.Contains("--workflow-file", run.StdOut);
        Assert.False(File.Exists(workflowPath + ".events.jsonl"));
        Assert.Contains("\"status\": \"readyToStart\"", await File.ReadAllTextAsync(workflowPath));
    }

    [Fact]
    public async Task CliRun_BoundaryWithoutMemoryHints_DoesNotLeakWholeContext()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-memory-{Guid.NewGuid():N}.json");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-memory-context-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));
        await File.WriteAllTextAsync(contextFile, "{\"request_kind\":\"apiToken-secret\",\"mode\":\"largeBlob-secret\",\"review\":{\"approved\":true}}");

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\" --context-file \"{contextFile}\"");
        Assert.Equal(3, run.ExitCode);
        Assert.Contains("\"type\":\"boundary\"", run.StdOut);
        Assert.Contains("\"memory_for_next_step\":{}", run.StdOut);
        Assert.DoesNotContain("apiToken", run.StdOut);
        Assert.DoesNotContain("largeBlob", run.StdOut);
    }
}
