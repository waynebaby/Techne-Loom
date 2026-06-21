using System.Diagnostics;
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

public sealed class SkillOrchestratorBehaviorTests
{
    [Fact]
    public void WorkflowJsonSerializer_NormalizesObjectContainers()
    {
        var instance = new WorkflowInstance
        {
            InstanceId = "wf-json",
            TemplateKind = "explicit-workflow-graph",
            Validation = new WorkflowValidationContract
            {
                DeclaredUserOwnedFields = ["filePath", "content"],
                ReservedRuntimeOwnedFields = ["workflow_file"],
                Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
                {
                    ["gate.summary"] = new WorkflowValidationGate
                    {
                        RequiredOutputFamilies = ["summary_json", "summary_md"],
                    },
                },
            },
            StartNodeId = "state.start",
            CurrentNodeId = "state.start",
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["state.start"] = new StateNode
                {
                    Id = "state.start",
                    Name = "Start",
                },
                ["transition.ask"] = new CommandTransition
                {
                    Id = "transition.ask",
                    Name = "Ask",
                    StepKind = WorkflowStepKind.AskUser,
                    OwnedInputMode = "user",
                    TerminalRoutes = ["evaluation_only"],
                    SatisfiesGateIds = ["gate.summary"],
                    PublishesOutputFamilies = ["summary_json", "summary_md"],
                    Command = new CommandInvocation
                    {
                        Kind = CommandInvocationKind.Tool,
                        Name = "noop",
                        Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["requiredInputs"] = new List<object?> { "filePath", "content" },
                            ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["review.summary"] = "ready",
                            },
                        },
                    },
                },
            },
        };

        var roundTrip = WorkflowJsonSerializer.Deserialize(WorkflowJsonSerializer.Serialize(instance));
        var transition = Assert.IsType<CommandTransition>(roundTrip.Nodes["transition.ask"]);
        var requiredInputs = Assert.IsAssignableFrom<IEnumerable<object?>>(transition.Command.Parameters!["requiredInputs"]);
        Assert.Equal(["filePath", "content"], requiredInputs.Select(Convert.ToString));
        Assert.Equal("explicit-workflow-graph", roundTrip.TemplateKind);
        Assert.NotNull(roundTrip.Validation);
        Assert.Equal(["evaluation_only"], transition.TerminalRoutes);
        Assert.Equal(["gate.summary"], transition.SatisfiesGateIds);

        var updates = Assert.IsAssignableFrom<IDictionary<string, object?>>(transition.Command.Parameters["updates"]);
        Assert.Equal("ready", updates["review.summary"]);
    }

    [Fact]
    public async Task CliCompile_GovernedWorkflowWithBusinessGate_Succeeds()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-governed-valid-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-governed-valid-audit-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateGovernedWorkflow()));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Validation artifacts:", run.StdErr);
        var analysisFile = Assert.Single(Directory.GetFiles(auditDirectory, "workflow.analysis.json", SearchOption.AllDirectories));
        var analysisJson = await File.ReadAllTextAsync(analysisFile);
        Assert.Contains("gate.assessment", analysisJson);
    }

    [Fact]
    public async Task CliCompile_LoomSkillEnhancementSelfBootstrapTemplate_Succeeds()
    {
        var repoRoot = FindRepositoryRoot();
        var skillWorkflowRoot = Path.Combine(repoRoot, ".github", "skills", "loom-skill-enhancement", "assets", "so-workflow");
        var workflowFile = Path.Combine(skillWorkflowRoot, "so-template.json");
        var skillPlanFile = Path.Combine(skillWorkflowRoot, "skill-plan.md");
        var lockFile = Path.Combine(skillWorkflowRoot, "so-package-lock.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-self-bootstrap-audit-{Guid.NewGuid():N}");

        Assert.True(File.Exists(skillPlanFile));
        Assert.True(File.Exists(lockFile));
        Assert.True(File.Exists(workflowFile));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Validation artifacts:", run.StdErr);
        var analysisFile = Assert.Single(Directory.GetFiles(auditDirectory, "workflow.analysis.json", SearchOption.AllDirectories));
        var analysisJson = await File.ReadAllTextAsync(analysisFile);
        Assert.Contains("gate.bootstrap_compile_review", analysisJson);
        Assert.Contains("gate.bootstrap_blocked_governance", analysisJson);
        Assert.Contains("gate.bootstrap_done", analysisJson);
        Assert.Contains("transition.classify_governance", analysisJson);
        Assert.Contains("transition.reacquire_runtime", analysisJson);
        Assert.Contains("transition.capture_guide", analysisJson);
        Assert.Contains("transition.compile_template", analysisJson);
        Assert.Contains("transition.wait_runtime", analysisJson);
        Assert.Contains("approval_decision", analysisJson);
    }

    [Fact]
    public void LoomSkillEnhancementSelfBootstrapTemplate_DeclaresGovernedBlockedRouteAndNodeMap()
    {
        var repoRoot = FindRepositoryRoot();
        var skillWorkflowRoot = Path.Combine(repoRoot, ".github", "skills", "loom-skill-enhancement", "assets", "so-workflow");
        var workflowFile = Path.Combine(skillWorkflowRoot, "so-template.json");
        var nodeMapFile = Path.Combine(skillWorkflowRoot, "node-to-file-map.md");
        var skillPlanFile = Path.Combine(skillWorkflowRoot, "skill-plan.md");

        var workflow = WorkflowJsonSerializer.Deserialize(File.ReadAllText(workflowFile));

        Assert.Equal("so-governed-target-skill", workflow.TemplateKind);
        Assert.NotNull(workflow.Validation);
        Assert.Contains("gate.bootstrap_plan", workflow.Validation!.Gates.Keys);
        Assert.Contains("gate.bootstrap_compile_review", workflow.Validation.Gates.Keys);
        Assert.Contains("gate.bootstrap_blocked_governance", workflow.Validation.Gates.Keys);
        Assert.Contains("gate.bootstrap_done", workflow.Validation.Gates.Keys);
        Assert.Equal(["gate.bootstrap_done"], workflow.Validation.Routes["bootstrap_route"].RequiredTerminalGateIds);
        Assert.Equal(["gate.bootstrap_blocked_governance"], workflow.Validation.Routes["bootstrap_route"].RequiredBlockedGateIds);
        Assert.Equal(["package_channel", "guide_language", "target_skill_path", "approval_decision", "feedback_notes"], workflow.Validation.DeclaredUserOwnedFields);
        Assert.Contains("workflow_file", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains("analysis_file", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains("governance_state", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains("resolved_so_runtime", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains("resolved_guide_surface", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.classify_governance");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.select_latest_channel");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.reacquire_runtime");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.capture_guide");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.wait_runtime");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.finalize_lock");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.draft_template");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.compile_template");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.request_review");

        var analyzeScope = Assert.IsType<CommandTransition>(workflow.Nodes["transition.analyze_scope"]);
        Assert.DoesNotContain("workflow_template_json", analyzeScope.PublishesOutputFamilies ?? []);

        var draftTemplate = Assert.IsType<CommandTransition>(workflow.Nodes["transition.draft_template"]);
        Assert.Contains("workflow_template_json", draftTemplate.PublishesOutputFamilies ?? []);

        var selectLatestChannel = Assert.IsType<CommandTransition>(workflow.Nodes["transition.select_latest_channel"]);
        var choices = Assert.IsAssignableFrom<IEnumerable<object?>>(selectLatestChannel.Command.Parameters!["choices"]);
        Assert.Equal(["released", "beta"], choices.Select(Convert.ToString));
        Assert.Equal("exactlyTwoChoices", Convert.ToString(selectLatestChannel.Command.Parameters["questionMode"]));

        var compileTemplate = Assert.IsType<CommandTransition>(workflow.Nodes["transition.compile_template"]);
        Assert.Equal(["gate.bootstrap_compile_review"], compileTemplate.SatisfiesGateIds);

        var waitRuntime = Assert.IsType<CommandTransition>(workflow.Nodes["transition.wait_runtime"]);
        Assert.Equal(["gate.bootstrap_blocked_governance"], waitRuntime.SatisfiesGateIds);
        Assert.Contains("workflow_runtime_copy_json", waitRuntime.PublishesBlockedOutputFamilies ?? []);

        var finalizeLock = Assert.IsType<CommandTransition>(workflow.Nodes["transition.finalize_lock"]);
        Assert.Equal(WorkflowStepKind.ToolCall, finalizeLock.StepKind);
        Assert.Equal("write-file", finalizeLock.Command.Name);
        Assert.Equal(".tmp/loom-skill-enhancement-completion-manifest.md", Convert.ToString(finalizeLock.Command.Parameters!["path"]));
        Assert.Contains("checked_in_so_package_lock_json", finalizeLock.PublishesOutputFamilies ?? []);

        var nodeMap = File.ReadAllText(nodeMapFile);
        Assert.Contains("transition.classify_governance", nodeMap);
        Assert.Contains("transition.select_latest_channel", nodeMap);
        Assert.Contains("transition.reacquire_runtime", nodeMap);
        Assert.Contains("transition.capture_guide", nodeMap);
        Assert.Contains("transition.confirm_channel", nodeMap);
        Assert.Contains("transition.analyze_scope", nodeMap);
        Assert.Contains("transition.draft_template", nodeMap);
        Assert.Contains("transition.compile_template", nodeMap);
        Assert.Contains("transition.request_review", nodeMap);
        Assert.Contains("transition.wait_runtime", nodeMap);
        Assert.Contains("transition.finalize_lock", nodeMap);
        Assert.Contains("OS temp root", nodeMap);
        Assert.Contains("workflow.json", nodeMap);
        Assert.Contains("so-package-lock.json", nodeMap);

        var skillPlan = File.ReadAllText(skillPlanFile);
        Assert.Contains("flowchart TD", skillPlan);
        Assert.Contains("Classify governance state", skillPlan);
        Assert.Contains("Ask latest released or latest beta", skillPlan);
        Assert.Contains("Capture fresh dotnet so.dll --guide", skillPlan);
        Assert.Contains("Approve?", skillPlan);
        Assert.Contains("Present compiled audit artifacts to user", skillPlan);
        Assert.Contains("workflow JSON backup", skillPlan);
        Assert.Contains("Publish blocked runtime outputs", skillPlan);
    }

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

        Assert.Contains("style state.ai fill:#dcfce7,stroke:#16a34a,stroke-width:1px", mermaid);
        Assert.Contains("style state.tool fill:#dbeafe,stroke:#2563eb,stroke-width:1px", mermaid);
        Assert.Contains("style state.optional fill:#fef3c7,stroke:#d97706,stroke-width:1px", mermaid);
        Assert.Contains("style state.required fill:#fee2e2,stroke:#dc2626,stroke-width:1px", mermaid);
        Assert.Contains("style state.done fill:#f8fafc,stroke:#94a3b8,stroke-width:1px", mermaid);
        Assert.Contains("style state.ai stroke:#ea580c,stroke-width:3px", mermaid);
    }

    [Fact]
    public async Task MermaidVisualizer_UsesBranchColorForNonUserOwnedConditionBranch()
    {
        var mermaid = await new MermaidWorkflowInstanceVisualizer().VisualizeToStringAsync(CreateGenericBranchColorWorkflow());

        Assert.Contains("style state.branch fill:#fef3c7,stroke:#a16207,stroke-width:1px", mermaid);
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

        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-resume-payload-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(resultFile, "{" +
            "\"transition_id\":\"transition.ask\"," +
            "\"correlation_key\":null," +
            "\"payload\":{\"review\":{\"approved\":true}}" +
            "}");

        var resumeRun = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowPath}\" --result-file \"{resultFile}\"");
        Assert.Equal(0, resumeRun.ExitCode);
        Assert.Contains("\"type\":\"result\"", resumeRun.StdOut);
        Assert.Contains("\"status\":\"completed\"", resumeRun.StdOut);
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
    public async Task CliHelp_ListsExpectedDotnetSoDllParameters()
    {
        var repoRoot = FindRepositoryRoot();
        var run = await RunCliAsync(repoRoot, "--help");
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("dotnet so.dll --guide", run.StdOut);
        Assert.Contains("dotnet so.dll --help", run.StdOut);
        Assert.Contains("dotnet so.dll compile", run.StdOut);
        Assert.Contains("dotnet so.dll run", run.StdOut);
        Assert.Contains("dotnet so.dll resume", run.StdOut);
        Assert.Contains("dotnet so.dll status", run.StdOut);
        Assert.Contains("dotnet so.dll inspect-workflow", run.StdOut);
        Assert.Contains("dotnet so.dll inspect-events", run.StdOut);
        Assert.Contains("dotnet so.dll ls", run.StdOut);
        Assert.Contains("workflow analysis validation artifacts", run.StdOut);
        Assert.DoesNotContain("dotnet so.dll planner", run.StdOut);
    }

    [Theory]
    [InlineData("compile", "--workflow-file")]
    [InlineData("run", "--workflow-file")]
    [InlineData("resume", "--workflow-file")]
    [InlineData("status", "--workflow-file")]
    [InlineData("inspect-workflow", "--workflow-file")]
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

    [Fact]
    public async Task CliGuide_ExportInsideSkillFolder_IsRejectedWithoutWritingFile()
    {
        var repoRoot = FindRepositoryRoot();
        var skillRoot = CreateSkillRoot();
        var exportFile = Path.Combine(skillRoot, "guide-export", "so-guide.md");

        var run = await RunCliAsync(repoRoot, $"--guide --export \"{exportFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("skill-owned directory", run.StdOut);
        Assert.Contains("--export", run.StdOut);
        Assert.False(File.Exists(exportFile));
    }

    [Fact]
    public async Task CliGuide_ExportedGuide_DescribesBlockedOnlyWorkflowJsonWorkarounds()
    {
        var repoRoot = FindRepositoryRoot();
        var exportDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-guide-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(exportDirectory);
        var exportFile = Path.Combine(exportDirectory, "so-guide.md");

        var run = await RunCliAsync(repoRoot, $"--guide --export \"{exportFile}\"");

        Assert.Equal(0, run.ExitCode);
        var guide = await File.ReadAllTextAsync(exportFile);
        Assert.Contains("do not directly edit checked-in workflow JSON as a normal maintenance path", guide);
        Assert.Contains("fully blocked and the user explicitly approves a narrow workaround", guide);
        Assert.Contains("immediately return to the SO-governed path", guide);
        Assert.Contains("for every official `run` or `resume` attempt", guide);
        Assert.Contains("clone the checked-in source workflow again", guide);
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
        await File.WriteAllTextAsync(workflowFile, await File.ReadAllTextAsync(sourceWorkflowFile));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(0, run.ExitCode);

        var mermaidFile = Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories).Single();
    AssertFileStartsWithMermaidFence(mermaidFile);
        var mermaid = await File.ReadAllTextAsync(mermaidFile);
        var instance = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile));

        Assert.StartsWith($"```mermaid{Environment.NewLine}{Environment.NewLine}", mermaid);
        Assert.EndsWith($"{Environment.NewLine}{Environment.NewLine}```{Environment.NewLine}{Environment.NewLine}", mermaid);
        Assert.Contains("flowchart TD", mermaid);
        AssertMermaidStateGraphConnected(mermaid, instance);
    }

    [Fact]
    public async Task CliRun_WithAuditOutput_EmitsAuditArtifactLinks()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-audit-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-audit-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        using var envelope = ReadFinalSoEnvelope(run.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");
        var audit = payload.GetProperty("audit_artifacts");
        Assert.Equal(Path.GetFullPath(auditDirectory), audit.GetProperty("output_root").GetString());
        Assert.True(File.Exists(audit.GetProperty("mermaid_file").GetString()));
        Assert.True(File.Exists(audit.GetProperty("html_file").GetString()));
        Assert.True(File.Exists(audit.GetProperty("workflow_backup_file").GetString()));
        Assert.True(File.Exists(audit.GetProperty("analysis_file").GetString()));
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
        await File.WriteAllTextAsync(contextFile, "{\"apiToken\":\"secret\",\"largeBlob\":\"very-large\",\"review\":{\"summary\":\"ok\"}}");

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\" --context-file \"{contextFile}\"");
        Assert.Equal(3, run.ExitCode);
        Assert.Contains("\"type\":\"boundary\"", run.StdOut);
        Assert.Contains("\"memory_for_next_step\":{}", run.StdOut);
        Assert.DoesNotContain("apiToken", run.StdOut);
        Assert.DoesNotContain("largeBlob", run.StdOut);
    }

    private static WorkflowInstance CreateImmediateTimeoutWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.ask",
                    Strategy = ConcurrencyStrategy.FirstSuccess,
                    GroupTimeout = TimeSpan.Zero,
                    TimeoutTargetStateId = "state.timeout",
                    TransitionIds = ["transition.ask"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var timeout = new StateNode
        {
            Id = "state.timeout",
            Name = "Timed Out",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var ask = new CommandTransition
        {
            Id = "transition.ask",
            Name = "Ask user",
            Description = "Need external input",
            TargetNodeId = timeout.Id,
            StepKind = WorkflowStepKind.AskUser,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        return new WorkflowInstance
        {
            InstanceId = "timeout-wf",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = timeout.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [timeout.Id] = timeout,
                [ask.Id] = ask,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static WorkflowInstance CreateChainedWorkflow(bool includeUnownedTransition = false)
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.start",
                    TransitionIds = ["transition.first"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var mid = new StateNode
        {
            Id = "state.mid",
            Name = "Mid",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.mid",
                    TransitionIds = ["transition.second"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var first = new CommandTransition
        {
            Id = "transition.first",
            Name = "First",
            TargetNodeId = mid.Id,
            StepKind = WorkflowStepKind.StateUpdate,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "sample.first",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        var second = new CommandTransition
        {
            Id = "transition.second",
            Name = "Second",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.StateUpdate,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "sample.second",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        var nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
        {
            [start.Id] = start,
            [mid.Id] = mid,
            [done.Id] = done,
            [first.Id] = first,
            [second.Id] = second,
        };

        if (includeUnownedTransition)
        {
            nodes["transition.detached"] = new CommandTransition
            {
                Id = "transition.detached",
                Name = "Detached",
                TargetNodeId = done.Id,
                StepKind = WorkflowStepKind.StateUpdate,
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.Tool,
                    Name = "sample.detached",
                    Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
                },
            };
        }

        return new WorkflowInstance
        {
            InstanceId = "chainsample",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = nodes,
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static WorkflowInstance CreateStepKindColorWorkflow()
    {
        var states = new[]
        {
            new StateNode { Id = "state.ai", Name = "AI", Groups = [new TransitionGroup { Id = "group.ai", TransitionIds = ["transition.ai"] }] },
            new StateNode { Id = "state.tool", Name = "Tool", Groups = [new TransitionGroup { Id = "group.tool", TransitionIds = ["transition.tool"] }] },
            new StateNode { Id = "state.optional", Name = "Optional", Groups = [new TransitionGroup { Id = "group.optional", TransitionIds = ["transition.optional"] }] },
            new StateNode { Id = "state.required", Name = "Required", Groups = [new TransitionGroup { Id = "group.required", TransitionIds = ["transition.required"] }] },
            new StateNode { Id = "state.done", Name = "Done", Groups = [] },
        };
        var transitions = new TransitionBase[]
        {
            CreateCommandTransition("transition.ai", "AI work", "state.tool", WorkflowStepKind.ModelThink),
            CreateCommandTransition("transition.tool", "Tool work", "state.optional", WorkflowStepKind.ToolCall),
            CreateCommandTransition("transition.optional", "Optional branch", "state.required", WorkflowStepKind.ConditionBranch, ownedInputMode: "user"),
            CreateCommandTransition("transition.required", "Required input", "state.done", WorkflowStepKind.AskUser),
        };

        return new WorkflowInstance
        {
            InstanceId = "color-sample",
            StartNodeId = "state.ai",
            CurrentNodeId = "state.ai",
            EndNodeId = "state.done",
            Nodes = states.Cast<ITaskNode>().Concat(transitions).ToDictionary(static node => node.Id, static node => node, StringComparer.Ordinal),
        };
    }

    private static WorkflowInstance CreateGenericBranchColorWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.branch",
            Name = "Branch",
            Groups = [new TransitionGroup { Id = "group.branch", TransitionIds = ["transition.branch"] }],
        };
        var done = new StateNode { Id = "state.done", Name = "Done", Groups = [] };
        var branch = CreateCommandTransition("transition.branch", "Branch", done.Id, WorkflowStepKind.ConditionBranch);

        return new WorkflowInstance
        {
            InstanceId = "generic-branch-sample",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [branch.Id] = branch,
            },
        };
    }

    private static WorkflowInstance CreateAnalysisWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.switch",
                    TransitionIds = ["transition.ask", "transition.wait", "transition.loop"],
                },
            ],
        };
        var wait = new StateNode { Id = "state.wait", Name = "Wait", Groups = [] };
        var done = new StateNode { Id = "state.done", Name = "Done", Groups = [] };
        var ask = CreateCommandTransition("transition.ask", "Ask", done.Id, WorkflowStepKind.AskUser) with
        {
            OwnedInputMode = "user",
            GuardExpression = "requiresUserChoice",
            SatisfiesGateIds = ["gate.workflow"],
            PublishesOutputFamilies = ["workflow_json"],
        };
        ask.Command.Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["requiredInputs"] = new List<object?> { "plan_confirmation" },
        };
        var runtimeWait = CreateCommandTransition("transition.wait", "Runtime wait", wait.Id, WorkflowStepKind.WaitResume) with
        {
            OwnedInputMode = "runtime",
        };
        var loop = new ExpressionTransition
        {
            Id = "transition.loop",
            Name = "Loop",
            TargetNodeId = start.Id,
            StepKind = WorkflowStepKind.ConditionBranch,
        };

        return new WorkflowInstance
        {
            InstanceId = "analysis-sample",
            TemplateKind = "so-governed-target-skill",
            Validation = new WorkflowValidationContract
            {
                DeclaredUserOwnedFields = ["plan_confirmation"],
                ReservedRuntimeOwnedFields = ["workflow_file"],
                Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
                {
                    ["gate.workflow"] = new WorkflowValidationGate { RequiredOutputFamilies = ["workflow_json"] },
                },
            },
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [wait.Id] = wait,
                [done.Id] = done,
                [ask.Id] = ask,
                [runtimeWait.Id] = runtimeWait,
                [loop.Id] = loop,
            },
        };
    }

    private static CommandTransition CreateCommandTransition(string id, string name, string targetNodeId, WorkflowStepKind stepKind, string? ownedInputMode = null)
    {
        return new CommandTransition
        {
            Id = id,
            Name = name,
            TargetNodeId = targetNodeId,
            StepKind = stepKind,
            OwnedInputMode = ownedInputMode,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = name,
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };
    }

    private static WorkflowInstance CreateEscapedCommandWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.cmd",
                    TransitionIds = ["transition.cmd"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var end = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var command = new CommandTransition
        {
            Id = "transition.cmd",
            Name = "Danger echo",
            Description = "Emit stdout and stderr",
            TargetNodeId = end.Id,
            OutputPath = "echoOutput",
            StepKind = WorkflowStepKind.ToolCall,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.CommandLine,
                Name = GetEscapedCommandName(),
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["args"] = GetEscapedCommandArguments(),
                },
            },
        };

        return new WorkflowInstance
        {
            InstanceId = $"cli-wf-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = end.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [end.Id] = end,
                [command.Id] = command,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static WorkflowInstance CreateNoProgressWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.never",
                    TransitionIds = ["transition.never"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var transition = new ExpressionTransition
        {
            Id = "transition.never",
            Name = "Never",
            GuardExpression = "false",
            SucceedExpression = "false",
            StepKind = WorkflowStepKind.ConditionBranch,
        };

        return new WorkflowInstance
        {
            InstanceId = $"no-progress-wf-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [transition.Id] = transition,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static WorkflowInstance CreateResumeWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.ask",
                    TransitionIds = ["transition.ask"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var review = new StateNode
        {
            Id = "state.review",
            Name = "Review",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.review",
                    TransitionIds = ["transition.check"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var ask = new CommandTransition
        {
            Id = "transition.ask",
            Name = "Ask user",
            Description = "Need structured result",
            TargetNodeId = review.Id,
            StepKind = WorkflowStepKind.AskUser,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        var check = new ExpressionTransition
        {
            Id = "transition.check",
            Name = "Check review",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.ConditionBranch,
            SucceedExpression = "review.approved == true",
            GuardExpression = "true",
        };

        return new WorkflowInstance
        {
            InstanceId = $"resume-wf-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [review.Id] = review,
                [done.Id] = done,
                [ask.Id] = ask,
                [check.Id] = check,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static WorkflowInstance CreateGovernedWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.emit",
                    TransitionIds = ["transition.emit_assessment"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var emit = new CommandTransition
        {
            Id = "transition.emit_assessment",
            Name = "Emit assessment",
            Description = "Publish machine-readable and human-reviewable assessment outputs.",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.ArtifactEmit,
            TerminalRoutes = ["evaluation_only"],
            SatisfiesGateIds = ["gate.assessment"],
            PublishesOutputFamilies = ["assessment_summary_json", "assessment_report_md"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "workflow.emitAssessment",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        return new WorkflowInstance
        {
            InstanceId = $"governed-valid-{Guid.NewGuid():N}",
            TemplateKind = "so-governed-target-skill",
            Validation = CreateGovernedValidationContract(),
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [emit.Id] = emit,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static WorkflowInstance CreateGovernanceOnlyDoneWorkflow()
    {
        var workflow = CreateGovernedWorkflow();
        var emit = Assert.IsType<CommandTransition>(workflow.Nodes["transition.emit_assessment"]);
        workflow.Nodes[emit.Id] = emit with
        {
            SatisfiesGateIds = [],
            PublishesOutputFamilies = [],
        };

        return workflow;
    }

    private static WorkflowInstance CreateAskUserRuntimeOwnedFieldWorkflow()
    {
        var workflow = CreateResumeWorkflow();
        workflow.TemplateKind = "so-governed-target-skill";
        workflow.Validation = CreateGovernedValidationContract();
        var ask = Assert.IsType<CommandTransition>(workflow.Nodes["transition.ask"]);
        ask = ask with
        {
            OwnedInputMode = "user",
        };
        ask.Command.Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["requiredInputs"] = new List<object?> { "workflow_file" },
        };
        workflow.Nodes[ask.Id] = ask;
        return workflow;
    }

    private static WorkflowInstance CreateBlockedRouteWorkflowMissingOutputs()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.blocked",
                    TransitionIds = ["transition.wait_for_fix"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var wait = new StateNode
        {
            Id = "state.wait",
            Name = "Wait",
            Groups = [],
            WaitBehavior = WaitBehavior.WaitForSignal,
        };

        var blocked = new CommandTransition
        {
            Id = "transition.wait_for_fix",
            Name = "Wait for fix",
            Description = "Pause after publishing strongest-earned blocked artifacts.",
            TargetNodeId = wait.Id,
            StepKind = WorkflowStepKind.WaitResume,
            BlockedRoutes = ["layout_candidate"],
            SatisfiesGateIds = ["gate.blocked_candidate"],
            PublishesBlockedOutputFamilies = ["layout_artifact"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "workflow.waitForFix",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        return new WorkflowInstance
        {
            InstanceId = $"governed-blocked-{Guid.NewGuid():N}",
            TemplateKind = "explicit-workflow-graph",
            Validation = new WorkflowValidationContract
            {
                Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
                {
                    ["gate.blocked_candidate"] = new WorkflowValidationGate
                    {
                        RequiredOutputFamilies = ["layout_artifact", "validator_output"],
                    },
                },
                Routes = new Dictionary<string, WorkflowRouteValidationProfile>(StringComparer.Ordinal)
                {
                    ["layout_candidate"] = new WorkflowRouteValidationProfile
                    {
                        RequiredBlockedGateIds = ["gate.blocked_candidate"],
                    },
                },
            },
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [wait.Id] = wait,
                [blocked.Id] = blocked,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static WorkflowInstance CreateBlockedRouteWorkflowReusingCompileGate()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.start",
                    TransitionIds = ["transition.compile_candidate"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var review = new StateNode
        {
            Id = "state.review",
            Name = "Review",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.review",
                    TransitionIds = ["transition.wait_for_fix"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var wait = new StateNode
        {
            Id = "state.wait",
            Name = "Wait",
            Groups = [],
            WaitBehavior = WaitBehavior.WaitForSignal,
        };

        var compile = new CommandTransition
        {
            Id = "transition.compile_candidate",
            Name = "Compile candidate",
            Description = "Compile review artifacts before the blocked boundary.",
            TargetNodeId = review.Id,
            StepKind = WorkflowStepKind.ToolCall,
            SatisfiesGateIds = ["gate.blocked_candidate"],
            PublishesOutputFamilies = ["layout_artifact", "validator_output"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "workflow.compileCandidate",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        var blocked = new CommandTransition
        {
            Id = "transition.wait_for_fix",
            Name = "Wait for fix",
            Description = "Pause after publishing strongest-earned blocked artifacts.",
            TargetNodeId = wait.Id,
            StepKind = WorkflowStepKind.WaitResume,
            BlockedRoutes = ["layout_candidate"],
            SatisfiesGateIds = ["gate.blocked_candidate"],
            PublishesBlockedOutputFamilies = ["layout_artifact", "validator_output"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "workflow.waitForFix",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        return new WorkflowInstance
        {
            InstanceId = $"governed-blocked-reuse-{Guid.NewGuid():N}",
            TemplateKind = "explicit-workflow-graph",
            Validation = new WorkflowValidationContract
            {
                Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
                {
                    ["gate.blocked_candidate"] = new WorkflowValidationGate
                    {
                        RequiredOutputFamilies = ["layout_artifact", "validator_output"],
                    },
                },
                Routes = new Dictionary<string, WorkflowRouteValidationProfile>(StringComparer.Ordinal)
                {
                    ["layout_candidate"] = new WorkflowRouteValidationProfile
                    {
                        RequiredBlockedGateIds = ["gate.blocked_candidate"],
                    },
                },
            },
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [review.Id] = review,
                [wait.Id] = wait,
                [compile.Id] = compile,
                [blocked.Id] = blocked,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static WorkflowValidationContract CreateGovernedValidationContract()
    {
        return new WorkflowValidationContract
        {
            DeclaredUserOwnedFields = ["review.approved", "approval_decision", "approval_notes"],
            Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
            {
                ["gate.assessment"] = new WorkflowValidationGate
                {
                    Description = "Assessment deliverables gate.",
                    RequiredOutputFamilies = ["assessment_summary_json", "assessment_report_md"],
                    RequiredMachineReadableOutputFamilies = ["assessment_summary_json"],
                    RequiredHumanReviewableOutputFamilies = ["assessment_report_md"],
                },
            },
            Routes = new Dictionary<string, WorkflowRouteValidationProfile>(StringComparer.Ordinal)
            {
                ["evaluation_only"] = new WorkflowRouteValidationProfile
                {
                    RequiredTerminalGateIds = ["gate.assessment"],
                },
            },
        };
    }

    private static WorkflowInstance CreateContextWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.check",
                    TransitionIds = ["transition.check"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var check = new ExpressionTransition
        {
            Id = "transition.check",
            Name = "Check context",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.ConditionBranch,
            SucceedExpression = "review.approved == true",
            GuardExpression = "true",
        };

        return new WorkflowInstance
        {
            InstanceId = $"context-wf-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [check.Id] = check,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static WorkflowInstance CreateSelfLoopWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.loop",
            Name = "Loop",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.loop",
                    TransitionIds = ["transition.loop"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var loop = new ExpressionTransition
        {
            Id = "transition.loop",
            Name = "Loop forever",
            TargetNodeId = start.Id,
            StepKind = WorkflowStepKind.ConditionBranch,
            GuardExpression = "true",
            SucceedExpression = "true",
        };

        return new WorkflowInstance
        {
            InstanceId = $"self-loop-wf-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [loop.Id] = loop,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string repoRoot, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{GetCliAssemblyPath()}\" {arguments}",
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

    private static string GetWorkflowPayloadPath(string repoRoot)
    {
        return Path.Combine(repoRoot, "tests", "dotnet", "Techne.Loom.SkillOrchestrator.Tests", "workflow.payload.json");
    }

    private static string CreateSkillRoot()
    {
        var skillRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-so-skill-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(skillRoot);
        File.WriteAllText(Path.Combine(skillRoot, "SKILL.md"), "# Temp skill\n");
        return skillRoot;
    }

    private static string GetCliAssemblyPath()
    {
        return typeof(DefaultWorkflowTaskTrackingService).Assembly.Location;
    }

    private static string GetEscapedCommandName()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : "bash";

    private static string GetEscapedCommandArguments()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "/c (echo ^<danger^> & echo error-line 1>&2)"
            : "-lc \"printf '<danger>\\n'; printf 'error-line\\n' >&2\"";

    private static string GetEscapedCommandPrefix()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd /c (echo" : "bash -lc";

    private static JsonDocument ReadSoEnvelope(string stdout)
    {
        return ReadSoEnvelopeByType(stdout, null);
    }

    private static JsonDocument ReadFinalSoEnvelope(string stdout)
    {
        return ReadSoEnvelopeByType(stdout, type => !string.Equals(type, "progress", StringComparison.Ordinal));
    }

    private static JsonDocument ReadSoEnvelopeByType(string stdout, Func<string, bool>? predicate)
    {
        const string startTag = "<so_property>";
        const string endTag = "</so_property>";
        var index = 0;
        JsonDocument? fallback = null;

        while (true)
        {
            var startIndex = stdout.IndexOf(startTag, index, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                break;
            }

            var endIndex = stdout.IndexOf(endTag, startIndex, StringComparison.Ordinal);
            if (endIndex <= startIndex)
            {
                break;
            }

            var json = stdout.Substring(startIndex + startTag.Length, endIndex - startIndex - startTag.Length).Trim();
            var document = JsonDocument.Parse(json);
            fallback?.Dispose();
            fallback = document;

            var type = document.RootElement.GetProperty("type").GetString() ?? string.Empty;
            if (predicate is null || predicate(type))
            {
                return document;
            }

            index = endIndex + endTag.Length;
        }

        if (fallback is not null)
        {
            return fallback;
        }

        throw new InvalidOperationException("SO CLI output did not contain a so_property block.");
    }

    private static void AssertMermaidStateGraphConnected(string mermaid, WorkflowInstance instance)
    {
        var stateIds = instance.Nodes.Values
            .OfType<StateNode>()
            .Select(static state => state.Id)
            .ToArray();

        Assert.NotEmpty(stateIds);

        var adjacency = stateIds.ToDictionary(
            static stateId => stateId,
            static _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var line in mermaid.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = Regex.Match(line, @"^(?<from>\S+)\s+-->\|.*\|\s+(?<to>\S+)$", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                continue;
            }

            var from = match.Groups["from"].Value;
            var to = match.Groups["to"].Value;
            if (!adjacency.ContainsKey(from) || !adjacency.ContainsKey(to))
            {
                continue;
            }

            adjacency[from].Add(to);
            adjacency[to].Add(from);
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(instance.StartNodeId);
        visited.Add(instance.StartNodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in adjacency[current])
            {
                if (visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        var disconnectedStates = stateIds.Where(stateId => !visited.Contains(stateId)).ToArray();
        Assert.True(disconnectedStates.Length == 0, $"Mermaid graph contains disconnected states: {string.Join(", ", disconnectedStates)}");
    }

    private static void AssertFileStartsWithMermaidFence(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        Assert.True(bytes.Length >= 3, $"Expected {filePath} to contain at least three bytes.");
        Assert.Equal((byte)'`', bytes[0]);
        Assert.Equal((byte)'`', bytes[1]);
        Assert.Equal((byte)'`', bytes[2]);
    }
}