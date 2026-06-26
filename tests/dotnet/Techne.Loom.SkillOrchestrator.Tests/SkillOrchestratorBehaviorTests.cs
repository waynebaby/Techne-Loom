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
                    WorkflowPhase = "Intake",
                },
                ["transition.ask"] = new CommandTransition
                {
                    Id = "transition.ask",
                    Name = "Ask",
                    WorkflowPhase = "Intake",
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
        Assert.Equal("Intake", Assert.IsType<StateNode>(roundTrip.Nodes["state.start"]).WorkflowPhase);
        Assert.Equal("Intake", transition.WorkflowPhase);

        var updates = Assert.IsAssignableFrom<IDictionary<string, object?>>(transition.Command.Parameters["updates"]);
        Assert.Equal("ready", updates["review.summary"]);
    }

    [Fact]
    public void WorkflowInstanceCloner_PreservesWorkflowPhaseOnStateNodes()
    {
        var clone = WorkflowInstanceCloner.Clone(CreateWorkflowPhaseWorkflow());

        var intake = Assert.IsType<StateNode>(clone.Nodes["state.intake"]);
        Assert.Equal("Intake", intake.WorkflowPhase);
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
        var skillWorkflowRoot = Path.Combine(GetLoomSkillEnhancementRoot(repoRoot), "assets", "so-workflow");
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
        var mermaidFile = Assert.Single(Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories));
        var mermaid = await File.ReadAllTextAsync(mermaidFile);
        Assert.Contains("subgraph phase_01_channel_and_entry[\"01 Channel And Entry\"]", mermaid);
        Assert.Contains("subgraph phase_03_runtime_proof[\"03 Runtime Proof\"]", mermaid);
        Assert.Contains("subgraph phase_05_planning[\"05 Planning\"]", mermaid);
        Assert.Contains("subgraph phase_09_compile_review_completion[\"09 Compile Review Completion\"]", mermaid);
        Assert.Contains("subgraph phase_10_official_runtime[\"10 Official Runtime\"]", mermaid);
        Assert.Contains("gate.bootstrap_runtime_ready", analysisJson);
        Assert.Contains("gate.bootstrap_compile_review", analysisJson);
        Assert.Contains("gate.bootstrap_official_blocked", analysisJson);
        Assert.Contains("gate.bootstrap_official_done", analysisJson);
        Assert.Contains("transition.classify_governance", analysisJson);
        Assert.Contains("transition.inspect_existing_skill_markdown", analysisJson);
        Assert.Contains("transition.inspect_existing_package_lock", analysisJson);
        Assert.Contains("transition.inspect_existing_workflow_assets", analysisJson);
        Assert.Contains("transition.require_reenhancement_gap_review", analysisJson);
        Assert.Contains("transition.compare_skill_markdown_against_latest_guide", analysisJson);
        Assert.Contains("transition.compare_package_lock_against_latest_guide", analysisJson);
        Assert.Contains("transition.compare_workflow_governance_against_latest_guide", analysisJson);
        Assert.Contains("transition.analyze_scope", analysisJson);
        Assert.Contains("transition.analyze_route_gate_structure", analysisJson);
        Assert.Contains("transition.analyze_evidence_node_map", analysisJson);
        Assert.Contains("transition.review_weave_out_subagent_fit", analysisJson);
        Assert.Contains("transition.run_review_fix_loop", analysisJson);
        Assert.Contains("transition.accept_official_runnable", analysisJson);
        Assert.Contains("transition.route_official_runnable_after_review", analysisJson);
        Assert.Contains("transition.materialize_runtime_copy", analysisJson);
        Assert.Contains("transition.reacquire_runtime", analysisJson);
        Assert.Contains("transition.capture_guide", analysisJson);
        Assert.Contains("transition.compile_template", analysisJson);
        Assert.Contains("transition.wait_runtime", analysisJson);
        Assert.Contains("transition.finalize_lock", analysisJson);
        Assert.Contains("approval_decision", analysisJson);
    }

    [Fact]
    public void LoomSkillEnhancementSelfBootstrapTemplate_DeclaresGovernedBlockedRouteAndNodeMap()
    {
        var repoRoot = FindRepositoryRoot();
        var skillWorkflowRoot = Path.Combine(GetLoomSkillEnhancementRoot(repoRoot), "assets", "so-workflow");
        var workflowFile = Path.Combine(skillWorkflowRoot, "so-template.json");
        var nodeMapFile = Path.Combine(skillWorkflowRoot, "node-to-file-map.md");
        var skillPlanFile = Path.Combine(skillWorkflowRoot, "skill-plan.md");

        var workflow = WorkflowJsonSerializer.Deserialize(File.ReadAllText(workflowFile));

        Assert.Equal("so-governed-target-skill", workflow.TemplateKind);
    Assert.Equal(WorkflowStatus.ReadyToStart, workflow.Status);
        Assert.NotNull(workflow.Validation);
        Assert.Contains("gate.bootstrap_plan", workflow.Validation!.Gates.Keys);
        Assert.Contains("gate.bootstrap_runtime_ready", workflow.Validation.Gates.Keys);
        Assert.Contains("gate.bootstrap_runtime_guide", workflow.Validation.Gates.Keys);
        Assert.Contains("gate.bootstrap_compile_review", workflow.Validation.Gates.Keys);
        Assert.Contains("gate.bootstrap_official_blocked", workflow.Validation.Gates.Keys);
        Assert.Contains("gate.bootstrap_official_done", workflow.Validation.Gates.Keys);
        Assert.Equal(["gate.bootstrap_official_done"], workflow.Validation.Routes["official_runnable_route"].RequiredTerminalGateIds);
        Assert.Equal(["gate.bootstrap_official_blocked"], workflow.Validation.Routes["official_runnable_route"].RequiredBlockedGateIds);
        Assert.Equal(["guide_language", "target_skill_path", "approval_decision", "feedback_notes"], workflow.Validation.DeclaredUserOwnedFields);
        Assert.Contains("workflow_file", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains("analysis_file", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains("governance_state", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains("resolved_so_runtime", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains("resolved_guide_surface", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.classify_governance");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.reenhancement_context");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.inspect_package_lock");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.inspect_workflow_assets");
        Assert.DoesNotContain(workflow.Nodes.Keys, id => id == "transition.select_latest_channel");
        Assert.DoesNotContain(workflow.Nodes.Keys, id => id == "state.latest_channel");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.enter_reenhancement_context");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.use_bound_runtime_path");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.inspect_existing_skill_markdown");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.inspect_existing_package_lock");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.inspect_existing_workflow_assets");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.reacquire_runtime");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.capture_guide");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.post_guide_decision");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.reenhancement_gap_review");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.reenhancement_lock_gap_review");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.reenhancement_workflow_gap_review");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.require_reenhancement_gap_review");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.skip_reenhancement_gap_review");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.compare_skill_markdown_against_latest_guide");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.compare_package_lock_against_latest_guide");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.compare_workflow_governance_against_latest_guide");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.plan_route_gate_review");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.plan_evidence_review");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.analyze_route_gate_structure");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.analyze_evidence_node_map");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.weave_out_subagent_review");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.review_weave_out_subagent_fit");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.review_fix_loop");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.run_review_fix_loop");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.review_fix_decision");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.runtime_copy");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.accept_official_runnable");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.route_official_runnable_after_review");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.materialize_runtime_copy");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.wait_runtime");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.lock");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.finalize_lock");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.draft_template");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.compile_template");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.request_review");

        var classifyGovernance = Assert.IsType<CommandTransition>(workflow.Nodes["transition.classify_governance"]);
        Assert.Equal(WorkflowStepKind.StateUpdate, classifyGovernance.StepKind);
        var classifyUpdates = Assert.IsAssignableFrom<IDictionary<string, object?>>(classifyGovernance.Command.Parameters!["updates"]);
        Assert.Equal("already_so_enhanced", Convert.ToString(classifyUpdates["governance_state"]));

        var analyzeScope = Assert.IsType<CommandTransition>(workflow.Nodes["transition.analyze_scope"]);
        Assert.DoesNotContain("workflow_template_json", analyzeScope.PublishesOutputFamilies ?? []);
        Assert.Contains("resolved_guide_surface_ref", analyzeScope.PublishesOutputFamilies ?? []);
        Assert.Contains("package_index_links_ref", analyzeScope.PublishesOutputFamilies ?? []);
        Assert.Equal(WorkflowStepKind.SubagentCall, analyzeScope.StepKind);
        Assert.Equal("assets/agents/loom-skill-enhancement-scope-input-output-analysis.agent.md", Convert.ToString(analyzeScope.Command.Parameters!["subagentRelativePath"]));

        var analyzeRouteGateStructure = Assert.IsType<CommandTransition>(workflow.Nodes["transition.analyze_route_gate_structure"]);
        Assert.Equal(WorkflowStepKind.SubagentCall, analyzeRouteGateStructure.StepKind);
        Assert.Equal("assets/agents/loom-skill-enhancement-route-gate-analysis.agent.md", Convert.ToString(analyzeRouteGateStructure.Command.Parameters!["subagentRelativePath"]));
        var routeInputs = Assert.IsAssignableFrom<IEnumerable<object?>>(analyzeRouteGateStructure.Command.Parameters["requiredInputs"]);
        Assert.DoesNotContain("plan.route_gate_review", routeInputs.Select(Convert.ToString));

        var analyzeEvidenceNodeMap = Assert.IsType<CommandTransition>(workflow.Nodes["transition.analyze_evidence_node_map"]);
        Assert.Equal(WorkflowStepKind.SubagentCall, analyzeEvidenceNodeMap.StepKind);
        Assert.Equal("assets/agents/loom-skill-enhancement-evidence-node-map-analysis.agent.md", Convert.ToString(analyzeEvidenceNodeMap.Command.Parameters!["subagentRelativePath"]));
        var evidenceInputs = Assert.IsAssignableFrom<IEnumerable<object?>>(analyzeEvidenceNodeMap.Command.Parameters["requiredInputs"]);
        Assert.Contains("plan.route_gate_review", evidenceInputs.Select(Convert.ToString));
        Assert.DoesNotContain("plan.evidence_review", evidenceInputs.Select(Convert.ToString));

        var reviewWeaveOutSubagentFit = Assert.IsType<CommandTransition>(workflow.Nodes["transition.review_weave_out_subagent_fit"]);
        Assert.Equal(WorkflowStepKind.SubagentCall, reviewWeaveOutSubagentFit.StepKind);
        Assert.Equal("assets/agents/loom-skill-enhancement-weave-out-subagent-fit-review.agent.md", Convert.ToString(reviewWeaveOutSubagentFit.Command.Parameters!["subagentRelativePath"]));
        Assert.Contains("weave_out_subagent_review", reviewWeaveOutSubagentFit.PublishesOutputFamilies ?? []);
        Assert.Contains("target_skill_subagent_assets", reviewWeaveOutSubagentFit.PublishesOutputFamilies ?? []);
        Assert.Contains("target_skill_subagent_link_updates", reviewWeaveOutSubagentFit.PublishesOutputFamilies ?? []);
        var weaveOutInputs = Assert.IsAssignableFrom<IEnumerable<object?>>(reviewWeaveOutSubagentFit.Command.Parameters["requiredInputs"]);
        Assert.Contains("SKILL.md", weaveOutInputs.Select(Convert.ToString));
        Assert.Contains("assets/so-workflow/node-to-file-map.md", weaveOutInputs.Select(Convert.ToString));

        var runReviewFixLoop = Assert.IsType<CommandTransition>(workflow.Nodes["transition.run_review_fix_loop"]);
        Assert.Equal(WorkflowStepKind.SubagentCall, runReviewFixLoop.StepKind);
        Assert.Equal("assets/agents/loom-skill-enhancement-review-fix-loop.agent.md", Convert.ToString(runReviewFixLoop.Command.Parameters!["subagentRelativePath"]));
        Assert.Contains("review_fix_loop_evidence", runReviewFixLoop.PublishesOutputFamilies ?? []);
        Assert.Contains("commit_report_ready", runReviewFixLoop.PublishesOutputFamilies ?? []);
        var reviewFixInputs = Assert.IsAssignableFrom<IEnumerable<object?>>(runReviewFixLoop.Command.Parameters["requiredInputs"]);
        Assert.Contains("workflow_template_json", reviewFixInputs.Select(Convert.ToString));
        Assert.Contains("target_skill_subagent_link_updates", reviewFixInputs.Select(Convert.ToString));

        var draftTemplate = Assert.IsType<CommandTransition>(workflow.Nodes["transition.draft_template"]);
        Assert.Contains("workflow_template_json", draftTemplate.PublishesOutputFamilies ?? []);
        Assert.Contains("workflow_designer_dispatch_record", draftTemplate.PublishesOutputFamilies ?? []);
        Assert.Equal(WorkflowStepKind.SubagentCall, draftTemplate.StepKind);
        Assert.Equal("assets/agents/loom-skill-enhancement-workflow-designer.agent.md", Convert.ToString(draftTemplate.Command.Parameters!["subagentRelativePath"]));

        var enterReenhancementContext = Assert.IsType<ExpressionTransition>(workflow.Nodes["transition.enter_reenhancement_context"]);
        Assert.Equal(WorkflowStepKind.ConditionBranch, enterReenhancementContext.StepKind);
        Assert.Equal("governance_state == 'already_so_enhanced'", enterReenhancementContext.GuardExpression);

        var useBoundRuntimePath = Assert.IsType<ExpressionTransition>(workflow.Nodes["transition.use_bound_runtime_path"]);
        Assert.Equal(WorkflowStepKind.ConditionBranch, useBoundRuntimePath.StepKind);
        Assert.Equal("governance_state != 'already_so_enhanced'", useBoundRuntimePath.GuardExpression);

        var inspectExistingSkillMarkdown = Assert.IsType<CommandTransition>(workflow.Nodes["transition.inspect_existing_skill_markdown"]);
        Assert.Equal(WorkflowStepKind.MemoryRead, inspectExistingSkillMarkdown.StepKind);
        Assert.Equal("target_skill_path", Convert.ToString(inspectExistingSkillMarkdown.Command.Parameters!["assetRootInput"]));

        var inspectExistingPackageLock = Assert.IsType<CommandTransition>(workflow.Nodes["transition.inspect_existing_package_lock"]);
        Assert.Equal(WorkflowStepKind.MemoryRead, inspectExistingPackageLock.StepKind);
        Assert.Equal("target_skill_path", Convert.ToString(inspectExistingPackageLock.Command.Parameters!["assetRootInput"]));

        var inspectExistingWorkflowAssets = Assert.IsType<CommandTransition>(workflow.Nodes["transition.inspect_existing_workflow_assets"]);
        Assert.Equal(WorkflowStepKind.MemoryRead, inspectExistingWorkflowAssets.StepKind);
        Assert.Equal("target_skill_path", Convert.ToString(inspectExistingWorkflowAssets.Command.Parameters!["assetRootInput"]));

        var reacquireRuntime = Assert.IsType<CommandTransition>(workflow.Nodes["transition.reacquire_runtime"]);
        Assert.Equal(WorkflowStepKind.WaitResume, reacquireRuntime.StepKind);
        Assert.Equal(["gate.bootstrap_runtime_ready"], reacquireRuntime.SatisfiesGateIds);
        Assert.Contains("published_package_workflow_evidence", reacquireRuntime.PublishesOutputFamilies ?? []);
        Assert.Contains("runtime_preflight_result", reacquireRuntime.PublishesOutputFamilies ?? []);
        Assert.Contains("resolved_runtime_version_ref", reacquireRuntime.PublishesOutputFamilies ?? []);
        Assert.Contains("runtime_bundle_packages_ref", reacquireRuntime.PublishesOutputFamilies ?? []);
        Assert.Contains("unified_runtime_directory_ref", reacquireRuntime.PublishesOutputFamilies ?? []);

        var captureGuide = Assert.IsType<CommandTransition>(workflow.Nodes["transition.capture_guide"]);
    Assert.Equal(WorkflowStepKind.WaitResume, captureGuide.StepKind);
        Assert.Equal(["gate.bootstrap_runtime_guide"], captureGuide.SatisfiesGateIds);
        Assert.Contains("resolved_guide_surface_ref", captureGuide.PublishesOutputFamilies ?? []);

        var compareSkillMarkdownAgainstLatestGuide = Assert.IsType<CommandTransition>(workflow.Nodes["transition.compare_skill_markdown_against_latest_guide"]);
        Assert.Equal(WorkflowStepKind.SubagentCall, compareSkillMarkdownAgainstLatestGuide.StepKind);
        Assert.Equal("assets/agents/loom-skill-enhancement-skill-markdown-gap-review.agent.md", Convert.ToString(compareSkillMarkdownAgainstLatestGuide.Command.Parameters!["subagentRelativePath"]));

        var comparePackageLockAgainstLatestGuide = Assert.IsType<CommandTransition>(workflow.Nodes["transition.compare_package_lock_against_latest_guide"]);
        Assert.Equal(WorkflowStepKind.SubagentCall, comparePackageLockAgainstLatestGuide.StepKind);
        Assert.Equal("assets/agents/loom-skill-enhancement-package-lock-gap-review.agent.md", Convert.ToString(comparePackageLockAgainstLatestGuide.Command.Parameters!["subagentRelativePath"]));

        var compareWorkflowGovernanceAgainstLatestGuide = Assert.IsType<CommandTransition>(workflow.Nodes["transition.compare_workflow_governance_against_latest_guide"]);
        Assert.Equal(WorkflowStepKind.SubagentCall, compareWorkflowGovernanceAgainstLatestGuide.StepKind);
        Assert.Equal("assets/agents/loom-skill-enhancement-workflow-governance-gap-review.agent.md", Convert.ToString(compareWorkflowGovernanceAgainstLatestGuide.Command.Parameters!["subagentRelativePath"]));

        var compileTemplate = Assert.IsType<CommandTransition>(workflow.Nodes["transition.compile_template"]);
    Assert.Equal(WorkflowStepKind.WaitResume, compileTemplate.StepKind);
        Assert.Equal(["gate.bootstrap_compile_review"], compileTemplate.SatisfiesGateIds);

        var acceptOfficialRunnable = Assert.IsType<ExpressionTransition>(workflow.Nodes["transition.accept_official_runnable"]);
        Assert.Equal(WorkflowStepKind.ConditionBranch, acceptOfficialRunnable.StepKind);
        Assert.Equal("approval_decision == 'approve_official_runnable'", acceptOfficialRunnable.GuardExpression);

        var routeOfficialRunnableAfterReview = Assert.IsType<ExpressionTransition>(workflow.Nodes["transition.route_official_runnable_after_review"]);
        Assert.Equal(WorkflowStepKind.ConditionBranch, routeOfficialRunnableAfterReview.StepKind);
        Assert.Equal("review_fix_loop_evidence != null", routeOfficialRunnableAfterReview.GuardExpression);

        var materializeRuntimeCopy = Assert.IsType<CommandTransition>(workflow.Nodes["transition.materialize_runtime_copy"]);
        Assert.Equal(WorkflowStepKind.ToolCall, materializeRuntimeCopy.StepKind);
        Assert.Contains("workflow_runtime_copy_json", materializeRuntimeCopy.PublishesOutputFamilies ?? []);

        var waitRuntime = Assert.IsType<CommandTransition>(workflow.Nodes["transition.wait_runtime"]);
        Assert.Equal(["official_runnable_route"], waitRuntime.BlockedRoutes);
        Assert.Equal(["gate.bootstrap_official_blocked"], waitRuntime.SatisfiesGateIds);
        Assert.Contains("workflow_runtime_copy_json", waitRuntime.PublishesBlockedOutputFamilies ?? []);
        Assert.Contains("event_log_file", waitRuntime.PublishesBlockedOutputFamilies ?? []);
        var waitRuntimeParameters = Assert.IsAssignableFrom<IDictionary<string, object?>>(waitRuntime.Command.Parameters);
        var waitRuntimeMatchInputs = Assert.IsAssignableFrom<IEnumerable<object?>>(waitRuntimeParameters["mustMatchContextInputs"]);
        Assert.Contains("workflow_runtime_copy_json", waitRuntimeMatchInputs.Select(Convert.ToString));

        var finalizeLock = Assert.IsType<CommandTransition>(workflow.Nodes["transition.finalize_lock"]);
        Assert.Equal(WorkflowStepKind.ToolCall, finalizeLock.StepKind);
        Assert.Equal("write-file", finalizeLock.Command.Name);
        Assert.Equal(".tmp/loom-skill-enhancement-completion-manifest.md", Convert.ToString(finalizeLock.Command.Parameters!["path"]));
        Assert.Equal(["official_runnable_route"], finalizeLock.TerminalRoutes);
        Assert.Equal(["gate.bootstrap_official_done"], finalizeLock.SatisfiesGateIds);
        Assert.Contains("workflow_runtime_copy_json", finalizeLock.PublishesOutputFamilies ?? []);
        Assert.Contains("event_log_file", finalizeLock.PublishesOutputFamilies ?? []);
        Assert.Contains("checked_in_skill_markdown_asset", finalizeLock.PublishesOutputFamilies ?? []);
        Assert.Contains("checked_in_package_lock_asset", finalizeLock.PublishesOutputFamilies ?? []);
        Assert.Contains("node_to_file_map", finalizeLock.PublishesOutputFamilies ?? []);
        Assert.Contains("skill_plan_md", finalizeLock.PublishesOutputFamilies ?? []);
        Assert.Contains("governance_notes_md", finalizeLock.PublishesOutputFamilies ?? []);
        Assert.Contains("completion_manifest_reference", finalizeLock.PublishesOutputFamilies ?? []);
        Assert.Contains("completion_manifest_md", finalizeLock.PublishesOutputFamilies ?? []);

        var officialDoneGate = workflow.Validation.Gates["gate.bootstrap_official_done"];
        Assert.Contains("workflow_runtime_copy_json", officialDoneGate.RequiredOutputFamilies);
        Assert.Contains("event_log_file", officialDoneGate.RequiredOutputFamilies);
        Assert.Contains("review_fix_loop_evidence", officialDoneGate.RequiredOutputFamilies);
        Assert.Contains("commit_report_ready", officialDoneGate.RequiredOutputFamilies);
        Assert.Contains("skill_plan_md", officialDoneGate.RequiredOutputFamilies);
        Assert.Contains("governance_notes_md", officialDoneGate.RequiredOutputFamilies);
        Assert.Contains("checked_in_skill_markdown_asset", officialDoneGate.RequiredOutputFamilies);
        Assert.Contains("checked_in_package_lock_asset", officialDoneGate.RequiredOutputFamilies);
        Assert.Contains("node_to_file_map", officialDoneGate.RequiredOutputFamilies);
        Assert.Contains("completion_manifest_reference", officialDoneGate.RequiredOutputFamilies);
        Assert.Contains("completion_manifest_md", officialDoneGate.RequiredOutputFamilies);

        var nodeMap = File.ReadAllText(nodeMapFile);
        Assert.Contains("transition.classify_governance", nodeMap);
        Assert.Contains("transition.inspect_existing_skill_markdown", nodeMap);
        Assert.Contains("transition.inspect_existing_package_lock", nodeMap);
        Assert.Contains("transition.inspect_existing_workflow_assets", nodeMap);
        Assert.Contains("transition.enter_reenhancement_context", nodeMap);
        Assert.Contains("transition.use_bound_runtime_path", nodeMap);
        Assert.Contains("transition.reacquire_runtime", nodeMap);
        Assert.Contains("transition.capture_guide", nodeMap);
        Assert.Contains("transition.require_reenhancement_gap_review", nodeMap);
        Assert.Contains("transition.compare_skill_markdown_against_latest_guide", nodeMap);
        Assert.Contains("transition.compare_package_lock_against_latest_guide", nodeMap);
        Assert.Contains("transition.compare_workflow_governance_against_latest_guide", nodeMap);
        Assert.Contains("loom-skill-enhancement-skill-markdown-gap-review.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-package-lock-gap-review.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-workflow-governance-gap-review.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-weave-out-subagent-fit-review.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-review-fix-loop.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-scope-input-output-analysis.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-route-gate-analysis.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-evidence-node-map-analysis.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-workflow-designer.agent.md", nodeMap);
        Assert.Contains("transition.review_weave_out_subagent_fit", nodeMap);
        Assert.Contains("transition.run_review_fix_loop", nodeMap);
        Assert.Contains("transition.accept_official_runnable", nodeMap);
        Assert.Contains("transition.route_official_runnable_after_review", nodeMap);
        Assert.Contains("transition.materialize_runtime_copy", nodeMap);
        Assert.DoesNotContain("transition.select_latest_channel", nodeMap);
        Assert.DoesNotContain("transition.confirm_channel", nodeMap);
        Assert.Contains("transition.analyze_scope", nodeMap);
        Assert.Contains("transition.analyze_route_gate_structure", nodeMap);
        Assert.Contains("transition.analyze_evidence_node_map", nodeMap);
        Assert.Contains("transition.draft_template", nodeMap);
        Assert.Contains("transition.compile_template", nodeMap);
        Assert.Contains("transition.request_review", nodeMap);
        Assert.Contains("transition.wait_runtime", nodeMap);
        Assert.Contains("transition.finalize_lock", nodeMap);
        Assert.Contains("shared entry gate step 1", nodeMap);
        Assert.Contains("compile-review prerequisite stage", nodeMap);
        Assert.Contains("official runnable route", nodeMap);
        Assert.Contains("OS temp root", nodeMap);
        Assert.Contains("workflow.json", nodeMap);
        Assert.Contains("so-package-lock.json", nodeMap);
        Assert.Contains("resolved guide/package-index references", nodeMap);

        var skillPlan = File.ReadAllText(skillPlanFile);
        Assert.Contains("flowchart TD", skillPlan);
        Assert.Contains("Classify governance state", skillPlan);
        Assert.Contains("Inspect current SKILL.md governance wording", skillPlan);
        Assert.Contains("Inspect current checked-in package lock", skillPlan);
        Assert.Contains("Inspect current checked-in workflow assets", skillPlan);
        Assert.Contains("Reuse the exact SO package version already bound", skillPlan);
        Assert.Contains("Capture fresh dotnet so.dll --guide", skillPlan);
        Assert.Contains("Shared entry gate passed", skillPlan);
        Assert.Contains("Compile-review route", skillPlan);
        Assert.Contains("Official Runnable Route", skillPlan);
        Assert.Contains("Run skill-markdown gap-review subagent", skillPlan);
        Assert.Contains("Run package-lock gap-review subagent", skillPlan);
        Assert.Contains("Run workflow-governance gap-review subagent", skillPlan);
        Assert.Contains("Run scope input-output analysis subagent", skillPlan);
        Assert.Contains("Run route-gate analysis subagent", skillPlan);
        Assert.Contains("Run evidence-node-map analysis subagent", skillPlan);
        Assert.Contains("Run workflow-designer subagent and refresh workflow template", skillPlan);
        Assert.Contains("Before user approval, review every current weave-out", skillPlan);
        Assert.Contains("Approval decision", skillPlan);
        Assert.Contains("Present compile-review artifacts to user", skillPlan);
        Assert.Contains("Run explicit review-skill to fix-skill loop", skillPlan);
        Assert.Contains("approve official runnable", skillPlan);
        Assert.Contains("Materialize external runtime workflow copy", skillPlan);
        Assert.Contains("Run public dotnet so.dll run", skillPlan);
        Assert.Contains("Resume with matching public result envelope", skillPlan);
        Assert.Contains("workflow JSON backup", skillPlan);
        Assert.Contains("Write runtime-owned completion manifest", skillPlan);
        var skillMarkdown = File.ReadAllText(Path.Combine(GetLoomSkillEnhancementRoot(repoRoot), "SKILL.md"));
        Assert.Contains("checked-in lock reference target", skillMarkdown);
        Assert.Contains("runtime-owned completion-manifest reference", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-skill-markdown-gap-review.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-package-lock-gap-review.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-workflow-governance-gap-review.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-weave-out-subagent-fit-review.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-review-fix-loop.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-workflow-designer.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-scope-input-output-analysis.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-route-gate-analysis.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-evidence-node-map-analysis.agent.md", skillMarkdown);

        var contractJson = File.ReadAllText(Path.Combine(GetLoomSkillEnhancementRoot(repoRoot), "contract.json"));
        Assert.Contains("\"guide_language\"", contractJson);
        Assert.Contains("checked_in_package_lock_asset", contractJson);
        Assert.Contains("checked_in_skill_markdown_asset", contractJson);
        Assert.Contains("completion_manifest_reference", contractJson);
        Assert.Contains("completion_manifest_md", contractJson);
        Assert.Contains("workflow_designer_dispatch_record", contractJson);
        Assert.Contains("weave_out_subagent_review", contractJson);
        Assert.Contains("review_fix_loop_evidence", contractJson);
        Assert.Contains("commit_report_ready", contractJson);
    }

    [Fact]
    public void SimpleExpressionEvaluator_TreatsNullLiteralAsNullForEqualityGuards()
    {
        var evaluator = new SimpleExpressionEvaluator();
        var context = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["present_value"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["summary"] = "ok",
            },
        };

        Assert.False(evaluator.EvaluateBoolean("missing_value != null", context));
        Assert.True(evaluator.EvaluateBoolean("missing_value == null", context));
        Assert.True(evaluator.EvaluateBoolean("present_value != null", context));
        Assert.False(evaluator.EvaluateBoolean("present_value == null", context));
    }

    [Fact]
    public async Task ResumeAsync_ExternalStepWithRequiredInputs_RejectsMissingPayloadFields()
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
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.AskUser,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new List<object?> { "review.approved" },
                },
            },
        };

        var instance = new WorkflowInstance
        {
            InstanceId = $"resume-required-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [ask.Id] = ask,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var engine = new DefaultTaskTrackingEngine(store);
        var service = new DefaultWorkflowTaskTrackingService(engine);

        var first = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.True(first.Suspended);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(instance.InstanceId, "transition.ask", payload: new Dictionary<string, object?>(StringComparer.Ordinal)));
        Assert.Contains("missing required inputs", error.Message, StringComparison.Ordinal);
        Assert.Contains("review.approved", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResumeAsync_ExternalStepWithOutputPath_RejectsEmptyPayloadAndStoresResultAtOutputPath()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.subagent",
                    TransitionIds = ["transition.subagent"],
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

        var subagent = new CommandTransition
        {
            Id = "transition.subagent",
            Name = "Run subagent",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.SubagentCall,
            OutputPath = "review.subagent",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        var instance = new WorkflowInstance
        {
            InstanceId = $"resume-output-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [subagent.Id] = subagent,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var engine = new DefaultTaskTrackingEngine(store);
        var service = new DefaultWorkflowTaskTrackingService(engine);

        var first = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.True(first.Suspended);

        var emptyPayloadError = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(instance.InstanceId, "transition.subagent", payload: new Dictionary<string, object?>(StringComparer.Ordinal)));
        Assert.Contains("must provide a non-empty result", emptyPayloadError.Message, StringComparison.Ordinal);

        await service.ResumeAsync(
            instance.InstanceId,
            "transition.subagent",
            payload: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["summary"] = "done",
            });

        var second = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.Equal(WorkflowStatus.Succeeded, second.StatusProjection.Status);

        var saved = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(saved);
        var stored = Assert.IsAssignableFrom<IDictionary<string, object?>>(PathValueAccessor.GetValue(saved!.Context, "review.subagent"));
        Assert.Equal("done", Convert.ToString(stored["summary"]));
    }

    [Fact]
    public async Task ResumeAsync_ExternalStepWithResumeOutputKey_StoresOnlyNamedResultAtOutputPath()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.wait",
                    TransitionIds = ["transition.wait"],
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

        var wait = new CommandTransition
        {
            Id = "transition.wait",
            Name = "Wait for structured result",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.WaitResume,
            OutputPath = "resolved.runtime",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["resumeOutputKey"] = "resolved_runtime",
                    ["requiredInputs"] = new List<object?> { "resolved_runtime", "runtime_preflight_result" },
                    ["outputBindings"] = new ReadOnlyDictionary<string, object?>(
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["resolved.runtime_copy"] = "$result",
                            ["resolved.preflight"] = "$context:runtime_preflight_result",
                        }),
                },
            },
        };

        var instance = new WorkflowInstance
        {
            InstanceId = $"resume-output-key-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [wait.Id] = wait,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var engine = new DefaultTaskTrackingEngine(store);
        var service = new DefaultWorkflowTaskTrackingService(engine);

        var first = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.True(first.Suspended);

        await service.ResumeAsync(
            instance.InstanceId,
            "transition.wait",
            payload: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["runtime_preflight_result"] = "ok",
                ["resolved_runtime"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["version"] = "1.2.3",
                },
            });

        var second = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.Equal(WorkflowStatus.Succeeded, second.StatusProjection.Status);

        var saved = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(saved);
        var storedOutput = Assert.IsAssignableFrom<IDictionary<string, object?>>(PathValueAccessor.GetValue(saved!.Context, "resolved.runtime"));
        Assert.Equal("1.2.3", Convert.ToString(storedOutput["version"]));
        Assert.Equal("ok", Convert.ToString(saved.Context["runtime_preflight_result"]));
        Assert.False(storedOutput.TryGetValue("runtime_preflight_result", out _));

        var copiedOutput = Assert.IsAssignableFrom<IDictionary<string, object?>>(PathValueAccessor.GetValue(saved.Context, "resolved.runtime_copy"));
        Assert.Equal("1.2.3", Convert.ToString(copiedOutput["version"]));
        Assert.Equal("ok", Convert.ToString(PathValueAccessor.GetValue(saved.Context, "resolved.preflight")));
    }

    [Fact]
    public async Task StartOrAdvanceAsync_LoomSkillEnhancementOfficialRuntimeCompletion_PublishesDeclaredTerminalOutputs()
    {
        var repoRoot = FindRepositoryRoot();
        var targetSkillPath = GetLoomSkillEnhancementRoot(repoRoot);
        var workflowFile = Path.Combine(targetSkillPath, "assets", "so-workflow", "so-template.json");
        var instance = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile));
        var materializeRuntimeCopy = Assert.IsType<CommandTransition>(instance.Nodes["transition.materialize_runtime_copy"]);
        materializeRuntimeCopy.Command.Parameters!["sourceTemplatePath"] = workflowFile;

        instance.InstanceId = $"loom-skill-enhancement-official-done-{Guid.NewGuid():N}";
        instance.CurrentNodeId = "state.review_fix_decision";
        instance.Status = WorkflowStatus.Running;
        instance.Context = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["approval_decision"] = "approve_official_runnable",
            ["target_skill_path"] = targetSkillPath,
            ["workflow_file"] = workflowFile,
            ["workflow_template_json"] = workflowFile,
            ["workflow_designer_dispatch_record"] = "workflow-designer dispatched with relative-link context",
            ["workflow_mermaid_md"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.mermaid.md"),
            ["workflow_html"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.html"),
            ["workflow_analysis_json"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.analysis.json"),
            ["weave_out_subagent_review"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["summary"] = "weave-out suitability review complete",
            },
            ["target_skill_subagent_assets"] = new[] { "assets/target-skill-weave-out.agent.md" },
            ["target_skill_subagent_link_updates"] = new[] { "SKILL.md -> assets/target-skill-weave-out.agent.md" },
            ["review_fix_loop_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["summary"] = "review-fix loop complete",
            },
            ["commit_report_ready"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = "ready",
                ["summary"] = "commit report ready",
            },
            ["workflow_runtime_copy_json"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-runtime-{Guid.NewGuid():N}.json"),
            ["event_log_file"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-events-{Guid.NewGuid():N}.jsonl"),
            ["route_output_gate_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["official_runnable_route"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["done_gates_satisfied"] = new[] { "business_output_gate" },
                    ["blocked_gates_satisfied"] = Array.Empty<string>(),
                },
            },
            ["completion_manifest_reference"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-completion-{Guid.NewGuid():N}.md"),
            ["completion_manifest_md"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-completion-human-{Guid.NewGuid():N}.md"),
        };

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var engine = new DefaultTaskTrackingEngine(store);
        var service = new DefaultWorkflowTaskTrackingService(engine);

        WorkflowStatus interimStatus;
        bool canContinue;
        do
        {
            var interimTick = await service.StartOrAdvanceAsync(instance.InstanceId);
            interimStatus = interimTick.StatusProjection.Status;
            canContinue = interimTick.Progressed || interimTick.Moved;
        }
        while (interimStatus == WorkflowStatus.Running && canContinue);

        Assert.Equal(WorkflowStatus.WaitingExternal, interimStatus);

        var waitingInstance = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(waitingInstance);
        var runtimeCopyPath = Convert.ToString(waitingInstance!.Context["workflow_runtime_copy_json"]);
        Assert.False(string.IsNullOrWhiteSpace(runtimeCopyPath));

        var resumePayload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["workflow_runtime_copy_json"] = runtimeCopyPath,
            ["event_log_file"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-events-{Guid.NewGuid():N}.jsonl"),
            ["workflow_mermaid_md"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.mermaid.md"),
            ["workflow_html"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.html"),
            ["workflow_analysis_json"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.analysis.json"),
        };

        await service.ResumeAsync(instance.InstanceId, "transition.wait_runtime", null, resumePayload);

        var finalTick = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.Equal(WorkflowStatus.Succeeded, finalTick.StatusProjection.Status);

        var saved = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(saved);
        Assert.Equal("assets/so-workflow/skill-plan.md", Convert.ToString(saved!.Context["skill_plan_md"]));
        Assert.Equal("assets/so-workflow/governance-notes.md", Convert.ToString(saved.Context["governance_notes_md"]));
        Assert.Equal("SKILL.md", Convert.ToString(saved.Context["checked_in_skill_markdown_asset"]));
        Assert.Equal("assets/so-workflow/so-package-lock.json", Convert.ToString(saved.Context["checked_in_package_lock_asset"]));
        Assert.Equal("assets/so-workflow/node-to-file-map.md", Convert.ToString(saved.Context["node_to_file_map"]));
        Assert.Equal(Path.Combine(Path.GetTempPath(), ".tmp").TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(Convert.ToString(saved.Context["completion_manifest_reference"]))?.TrimEnd(Path.DirectorySeparatorChar), ignoreCase: true);
        Assert.Equal("assets/so-workflow/skill-plan.md", Convert.ToString(saved.Context["skill_plan_md"]));
        Assert.Equal("review-fix loop complete", Convert.ToString(((IDictionary<string, object?>)saved.Context["review_fix_loop_evidence"]!)["summary"]));
        Assert.Equal("ready", Convert.ToString(((IDictionary<string, object?>)saved.Context["commit_report_ready"]!)["status"]));
        Assert.Equal(runtimeCopyPath, Convert.ToString(saved.Context["workflow_runtime_copy_json"]));

        var completionManifestPath = Convert.ToString(saved.Context["completion_manifest_md"]);
        Assert.False(string.IsNullOrWhiteSpace(completionManifestPath));
        var completionManifest = await File.ReadAllTextAsync(completionManifestPath!);
        Assert.Contains("# Governance Verdict", completionManifest);
        Assert.Contains("Verdict rule: this manifest summarizes governed completion only when the mapped runtime-owned evidence families below already exist", completionManifest);
        Assert.DoesNotContain("workflow_location_summary", completionManifest);
        Assert.DoesNotContain("route_output_gate_evidence", completionManifest);
        Assert.Contains("completion_manifest_md", completionManifest);
    }

    [Fact]
    public async Task StartOrAdvanceAsync_LoomSkillEnhancementOfficialRuntimeCompletion_DoesNotAdvanceWithoutReviewFixEvidence()
    {
        var repoRoot = FindRepositoryRoot();
        var targetSkillPath = GetLoomSkillEnhancementRoot(repoRoot);
        var workflowFile = Path.Combine(targetSkillPath, "assets", "so-workflow", "so-template.json");
        var instance = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile));

        instance.InstanceId = $"loom-skill-enhancement-missing-review-fix-{Guid.NewGuid():N}";
        instance.CurrentNodeId = "state.review_fix_decision";
        instance.Status = WorkflowStatus.Running;
        instance.Context = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["approval_decision"] = "approve_official_runnable",
            ["target_skill_path"] = targetSkillPath,
            ["workflow_file"] = workflowFile,
            ["workflow_template_json"] = workflowFile,
        };

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var engine = new DefaultTaskTrackingEngine(store);
        var service = new DefaultWorkflowTaskTrackingService(engine);

        var tick = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.Equal(WorkflowStatus.Running, tick.StatusProjection.Status);
        Assert.Equal("state.review_fix_decision", tick.StatusProjection.CurrentNodeId);
        var saved = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(saved);
        Assert.False(saved!.Context.ContainsKey("workflow_runtime_copy_json"));
        Assert.False(saved.Context.ContainsKey("completion_manifest_md"));
    }

    [Fact]
    public async Task StartOrAdvanceAsync_LoomSkillEnhancementOfficialRuntimeCompletion_PreservesExistingRouteOutputGateEvidenceOutsideManifest()
    {
        var repoRoot = FindRepositoryRoot();
        var targetSkillPath = GetLoomSkillEnhancementRoot(repoRoot);
        var workflowFile = Path.Combine(targetSkillPath, "assets", "so-workflow", "so-template.json");
        var instance = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile));
        var materializeRuntimeCopy = Assert.IsType<CommandTransition>(instance.Nodes["transition.materialize_runtime_copy"]);
        materializeRuntimeCopy.Command.Parameters!["sourceTemplatePath"] = workflowFile;

        var expectedRouteEvidence = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["official_runnable_route"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["done_gates_satisfied"] = new[] { "gate.bootstrap_official_done" },
                ["blocked_gates_satisfied"] = Array.Empty<string>(),
            },
        };

        instance.InstanceId = $"loom-skill-enhancement-route-evidence-{Guid.NewGuid():N}";
        instance.CurrentNodeId = "state.review_fix_decision";
        instance.Status = WorkflowStatus.Running;
        instance.Context = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["approval_decision"] = "approve_official_runnable",
            ["target_skill_path"] = targetSkillPath,
            ["workflow_file"] = workflowFile,
            ["workflow_template_json"] = workflowFile,
            ["workflow_designer_dispatch_record"] = "workflow-designer dispatched with relative-link context",
            ["workflow_mermaid_md"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.mermaid.md"),
            ["workflow_html"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.html"),
            ["workflow_analysis_json"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.analysis.json"),
            ["weave_out_subagent_review"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["summary"] = "weave-out suitability review complete",
            },
            ["target_skill_subagent_assets"] = new[] { "assets/target-skill-weave-out.agent.md" },
            ["target_skill_subagent_link_updates"] = new[] { "SKILL.md -> assets/target-skill-weave-out.agent.md" },
            ["review_fix_loop_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["summary"] = "review-fix loop complete",
            },
            ["commit_report_ready"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = "ready",
                ["summary"] = "commit report ready",
            },
            ["workflow_runtime_copy_json"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-runtime-{Guid.NewGuid():N}.json"),
            ["event_log_file"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-events-{Guid.NewGuid():N}.jsonl"),
            ["route_output_gate_evidence"] = expectedRouteEvidence,
        };

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var engine = new DefaultTaskTrackingEngine(store);
        var service = new DefaultWorkflowTaskTrackingService(engine);

        WorkflowStatus interimStatus;
        bool canContinue;
        do
        {
            var interimTick = await service.StartOrAdvanceAsync(instance.InstanceId);
            interimStatus = interimTick.StatusProjection.Status;
            canContinue = interimTick.Progressed || interimTick.Moved;
        }
        while (interimStatus == WorkflowStatus.Running && canContinue);

        Assert.Equal(WorkflowStatus.WaitingExternal, interimStatus);

        var waitingInstance = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(waitingInstance);
        var runtimeCopyPath = Convert.ToString(waitingInstance!.Context["workflow_runtime_copy_json"]);
        Assert.False(string.IsNullOrWhiteSpace(runtimeCopyPath));

        var resumePayload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["workflow_runtime_copy_json"] = runtimeCopyPath,
            ["event_log_file"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-events-{Guid.NewGuid():N}.jsonl"),
            ["workflow_mermaid_md"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.mermaid.md"),
            ["workflow_html"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.html"),
            ["workflow_analysis_json"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.analysis.json"),
        };

        await service.ResumeAsync(instance.InstanceId, "transition.wait_runtime", null, resumePayload);

        var finalTick = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.Equal(WorkflowStatus.Succeeded, finalTick.StatusProjection.Status);

        var saved = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(saved);
        var savedRouteEvidence = Assert.IsAssignableFrom<IDictionary<string, object?>>(saved!.Context["route_output_gate_evidence"]);
        Assert.Equal(JsonSerializer.Serialize(expectedRouteEvidence), JsonSerializer.Serialize(savedRouteEvidence));

        var completionManifestPath = Convert.ToString(saved.Context["completion_manifest_md"]);
        Assert.False(string.IsNullOrWhiteSpace(completionManifestPath));
        var completionManifest = await File.ReadAllTextAsync(completionManifestPath!);
        Assert.DoesNotContain("route_output_gate_evidence", completionManifest);
        Assert.Contains("It does not replace those evidence families", completionManifest);
    }

    [Fact]
    public void LoomEnhancedResearchDemoAssets_DistinguishNormativeSkillSurfaceFromHistoricalTimelineSurface()
    {
        var repoRoot = FindRepositoryRoot();

        var bornGovernancedSkillMarkdown = File.ReadAllText(Path.Combine(repoRoot, "demos", "loom-enhanced-research", "3. Born-governanced", "loom-enhanced-research", "SKILL.md"));
        Assert.Contains("This skill is Loom-governanced under Loom Skill Orchestrator.", bornGovernancedSkillMarkdown);
        Assert.Contains("Historical demo timelines for this skill may record earlier compile-ready or blocked states, but those records do not redefine the current completion criteria.", bornGovernancedSkillMarkdown);
        Assert.DoesNotContain("compile-ready Loom-governanced target-skill integration with official run evidence still pending", bornGovernancedSkillMarkdown);

        var enhancedBornGovernancedSkillMarkdown = File.ReadAllText(Path.Combine(repoRoot, "demos", "loom-enhanced-research", "3.1 Enhance from Born-governanced", "loom-enhanced-research", "SKILL.md"));
        Assert.Contains("This skill is Loom-governanced under Loom Skill Orchestrator.", enhancedBornGovernancedSkillMarkdown);
        Assert.Contains("Historical demo timelines for this skill may record earlier compile-ready or blocked states, but those records do not redefine the current completion criteria.", enhancedBornGovernancedSkillMarkdown);
        Assert.DoesNotContain("compile-ready Loom-governanced target-skill integration with official run evidence still pending", enhancedBornGovernancedSkillMarkdown);

        var bornGovernancedReadme = File.ReadAllText(Path.Combine(repoRoot, "demos", "loom-enhanced-research", "3. Born-governanced", "Readme.md"));
        Assert.Contains("This timeline is a historical slice record.", bornGovernancedReadme);

        var bornGovernancedReadmeZh = File.ReadAllText(Path.Combine(repoRoot, "demos", "loom-enhanced-research", "3. Born-governanced", "Readme.zh-CN.md"));
        Assert.Contains("这是一份历史切片记录。", bornGovernancedReadmeZh);

        var enhancedBornGovernancedReadme = File.ReadAllText(Path.Combine(repoRoot, "demos", "loom-enhanced-research", "3.1 Enhance from Born-governanced", "Readme.md"));
        Assert.Contains("This timeline is a historical slice record.", enhancedBornGovernancedReadme);

        var enhancedBornGovernancedReadmeZh = File.ReadAllText(Path.Combine(repoRoot, "demos", "loom-enhanced-research", "3.1 Enhance from Born-governanced", "Readme.zh-CN.md"));
        Assert.Contains("这是一份历史切片记录。", enhancedBornGovernancedReadmeZh);

        var bornGovernancedContract = File.ReadAllText(Path.Combine(repoRoot, "demos", "loom-enhanced-research", "3. Born-governanced", "loom-enhanced-research", "contract.json"));
        Assert.Contains("historical_slice_note", bornGovernancedContract);
        Assert.Contains("Historical baseline asset for the first born-governanced slice", bornGovernancedContract);
        Assert.Contains("anchors the final governed completion verdict surface", bornGovernancedContract);
        Assert.Contains("fixed completion verdict and evidence checklist surface", bornGovernancedContract);

        var enhancedBornGovernancedContract = File.ReadAllText(Path.Combine(repoRoot, "demos", "loom-enhanced-research", "3.1 Enhance from Born-governanced", "loom-enhanced-research", "contract.json"));
        Assert.DoesNotContain("historical_slice_note", enhancedBornGovernancedContract);
        Assert.Contains("anchors the final governed completion verdict surface", enhancedBornGovernancedContract);
        Assert.Contains("fixed completion verdict and evidence checklist surface", enhancedBornGovernancedContract);

        var bornGovernancedTemplate = File.ReadAllText(Path.Combine(repoRoot, "demos", "loom-enhanced-research", "3. Born-governanced", "loom-enhanced-research", "assets", "so-workflow", "so-template.json"));
        Assert.Contains("historicalSliceNote", bornGovernancedTemplate);
        Assert.Contains("Historical baseline workflow snapshot for the first born-governanced slice", bornGovernancedTemplate);

        var enhancedBornGovernancedTemplate = File.ReadAllText(Path.Combine(repoRoot, "demos", "loom-enhanced-research", "3.1 Enhance from Born-governanced", "loom-enhanced-research", "assets", "so-workflow", "so-template.json"));
        Assert.DoesNotContain("historicalSliceNote", enhancedBornGovernancedTemplate);
        Assert.Contains("workaround_runtime_compile_ref", enhancedBornGovernancedTemplate);
    }

    [Fact]
    public async Task CliRun_LoomSkillEnhancementSelfBootstrapTemplate_AdvancesAcrossPublicRunResumePath()
    {
        var repoRoot = FindRepositoryRoot();
        var skillRoot = GetLoomSkillEnhancementRoot(repoRoot);
        var sourceWorkflowFile = Path.Combine(skillRoot, "assets", "so-workflow", "so-template.json");
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-runtime-{Guid.NewGuid():N}.json");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-context-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-audit-{Guid.NewGuid():N}");

        await File.WriteAllTextAsync(workflowPath, await File.ReadAllTextAsync(sourceWorkflowFile));
        await File.WriteAllTextAsync(
            contextFile,
            JsonSerializer.Serialize(
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["target_skill_path"] = skillRoot,
                    ["guide_language"] = "en",
                },
                WorkflowJsonSerializer.CreateDefaultOptions(indented: false)));

        async Task<JsonDocument> ResumeAndReadEnvelopeAsync(string transitionId, Dictionary<string, object?> payload, int expectedExitCode = 3)
        {
            var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-resume-{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(
                resultFile,
                JsonSerializer.Serialize(
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["transition_id"] = transitionId,
                        ["correlation_key"] = null,
                        ["payload"] = payload,
                    },
                    WorkflowJsonSerializer.CreateDefaultOptions(indented: false)));

            var run = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowPath}\" --result-file \"{resultFile}\" --audit-output \"{auditDirectory}\"");
            Assert.Equal(expectedExitCode, run.ExitCode);
            return ReadFinalSoEnvelope(run.StdOut);
        }

        static string[] ReadRequiredInputs(JsonElement payload)
            => payload.GetProperty("required_inputs").EnumerateArray().Select(static item => item.GetString() ?? string.Empty).ToArray();

        var firstRun = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\" --context-file \"{contextFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(3, firstRun.ExitCode);

        using (var firstBoundary = ReadFinalSoEnvelope(firstRun.StdOut))
        {
            var payload = firstBoundary.RootElement.GetProperty("payload");
            Assert.Equal("boundary", firstBoundary.RootElement.GetProperty("type").GetString());
            Assert.Equal("WaitResume", payload.GetProperty("current_step_kind").GetString());
            Assert.DoesNotContain("package_channel", ReadRequiredInputs(payload));
            Assert.Contains("runtime_preflight_result", ReadRequiredInputs(payload));
        }

        using (var thirdBoundary = await ResumeAndReadEnvelopeAsync(
                   "transition.reacquire_runtime",
                   new Dictionary<string, object?>(StringComparer.Ordinal)
                   {
                       ["published_package_workflow_evidence"] = "published-runtime-restored",
                       ["runtime_preflight_result"] = "preflight-ok",
                       ["resolved_runtime_version_ref"] = "1.2.3",
                       ["runtime_bundle_packages_ref"] = new[] { "Techne.Loom.SkillOrchestrator", "Techne.Loom.Common", "Techne.Loom.Abstractions" },
                       ["unified_runtime_directory_ref"] = Path.Combine(Path.GetTempPath(), $"techne-loom-runtime-{Guid.NewGuid():N}"),
                       ["resolved_so_runtime"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                       {
                           ["resolved_runtime_version"] = "1.2.3",
                           ["runtime_bundle_packages"] = new[] { "Techne.Loom.SkillOrchestrator", "Techne.Loom.Common", "Techne.Loom.Abstractions" },
                       },
                   }))
        {
            var payload = thirdBoundary.RootElement.GetProperty("payload");
            Assert.Equal("WaitResume", payload.GetProperty("current_step_kind").GetString());
            Assert.Contains("resolved_guide_surface_ref", ReadRequiredInputs(payload));
        }

        using (var fourthBoundary = await ResumeAndReadEnvelopeAsync(
                   "transition.capture_guide",
                   new Dictionary<string, object?>(StringComparer.Ordinal)
                   {
                       ["resolved_guide_surface_ref"] = "guide://so/en/latest",
                       ["resolved_guide_surface"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                       {
                           ["language"] = "en",
                           ["command"] = "dotnet so.dll --guide",
                       },
                   }))
        {
            var payload = fourthBoundary.RootElement.GetProperty("payload");
            Assert.Equal("SubagentCall", payload.GetProperty("current_step_kind").GetString());
            Assert.Contains("existing_skill_markdown_review", ReadRequiredInputs(payload));
        }

        using var fifthBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.compare_skill_markdown_against_latest_guide",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["summary"] = "skill markdown gap review complete",
            });
        Assert.Equal("SubagentCall", fifthBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var sixthBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.compare_package_lock_against_latest_guide",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["summary"] = "package lock gap review complete",
            });
        Assert.Equal("SubagentCall", sixthBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var seventhBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.compare_workflow_governance_against_latest_guide",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["summary"] = "workflow governance gap review complete",
            });
        Assert.Equal("SubagentCall", seventhBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var eighthBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.analyze_scope",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["content"] = "# Skill plan\n",
            });
        Assert.Equal("SubagentCall", eighthBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var ninthBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.analyze_route_gate_structure",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["summary"] = "route gate review complete",
            });
        Assert.Equal("SubagentCall", ninthBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var tenthBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.analyze_evidence_node_map",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["summary"] = "evidence node map review complete",
            });
        Assert.Equal("SubagentCall", tenthBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var eleventhBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.draft_template",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["workflow_template_json"] = sourceWorkflowFile,
                ["workflow_designer_dispatch_record"] = "workflow-designer dispatched with relative-link context",
            });
        Assert.Equal("WaitResume", eleventhBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var twelfthBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.compile_template",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["workflow_mermaid_md"] = Path.Combine(auditDirectory, "workflow.mermaid.md"),
                ["workflow_html"] = Path.Combine(auditDirectory, "workflow.html"),
                ["workflow_analysis_json"] = Path.Combine(auditDirectory, "workflow.analysis.json"),
                ["workflow_json_backup"] = Path.Combine(auditDirectory, "workflow.json"),
            });
        Assert.Equal("SubagentCall", twelfthBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var thirteenthBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.review_weave_out_subagent_fit",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["weave_out_subagent_review"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["summary"] = "weave-out suitability review complete",
                },
                ["target_skill_subagent_assets"] = new[] { "assets/target-skill-weave-out.agent.md" },
                ["target_skill_subagent_link_updates"] = new[] { "SKILL.md -> assets/target-skill-weave-out.agent.md" },
                ["SKILL"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["md"] = "# Target skill",
                },
                ["assets/so-workflow/node-to-file-map"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["md"] = "# Node map",
                },
                ["assets/so-workflow/so-template"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["json"] = sourceWorkflowFile,
                },
            });
        Assert.Equal("AskUser", thirteenthBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var fifteenthBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.request_review",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["approval_decision"] = "approve_official_runnable",
                ["feedback_notes"] = string.Empty,
            });
        Assert.Equal("SubagentCall", fifteenthBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var sixteenthBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.run_review_fix_loop",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["review_fix_loop_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["summary"] = "review-fix loop complete",
                },
                ["commit_report_ready"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "ready",
                    ["summary"] = "commit report ready",
                },
            });
        Assert.Equal("WaitResume", sixteenthBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());
        Assert.Contains("workflow_runtime_copy_json", ReadRequiredInputs(sixteenthBoundary.RootElement.GetProperty("payload")));
        Assert.Contains("event_log_file", ReadRequiredInputs(sixteenthBoundary.RootElement.GetProperty("payload")));

        var savedRuntime = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowPath));
        var runtimeCopyPath = Convert.ToString(savedRuntime.Context["workflow_runtime_copy_json"]);
        Assert.False(string.IsNullOrWhiteSpace(runtimeCopyPath));
        Assert.NotEqual(workflowPath, runtimeCopyPath);

        var mismatchResultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-resume-mismatch-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            mismatchResultFile,
            JsonSerializer.Serialize(
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["transition_id"] = "transition.wait_runtime",
                    ["correlation_key"] = null,
                    ["payload"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["workflow_runtime_copy_json"] = workflowPath,
                        ["event_log_file"] = Path.Combine(auditDirectory, "event-mismatch.log"),
                        ["workflow_mermaid_md"] = Path.Combine(auditDirectory, "workflow.mermaid.md"),
                        ["workflow_html"] = Path.Combine(auditDirectory, "workflow.html"),
                        ["workflow_analysis_json"] = Path.Combine(auditDirectory, "workflow.analysis.json"),
                    },
                },
                WorkflowJsonSerializer.CreateDefaultOptions(indented: false)));

        var mismatchRun = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowPath}\" --result-file \"{mismatchResultFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(2, mismatchRun.ExitCode);
        using (var mismatchEnvelope = ReadFinalSoEnvelope(mismatchRun.StdOut))
        {
            Assert.Equal("error", mismatchEnvelope.RootElement.GetProperty("type").GetString());
            var mismatchPayload = mismatchEnvelope.RootElement.GetProperty("payload");
            Assert.Contains("workflow_runtime_copy_json", mismatchPayload.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.Contains("existing runtime context", mismatchPayload.GetProperty("message").GetString(), StringComparison.Ordinal);
        }

        using var completed = await ResumeAndReadEnvelopeAsync(
            "transition.wait_runtime",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["workflow_runtime_copy_json"] = runtimeCopyPath,
                ["event_log_file"] = Path.Combine(auditDirectory, "event.log"),
                ["workflow_mermaid_md"] = Path.Combine(auditDirectory, "workflow.mermaid.md"),
                ["workflow_html"] = Path.Combine(auditDirectory, "workflow.html"),
                ["workflow_analysis_json"] = Path.Combine(auditDirectory, "workflow.analysis.json"),
            },
            expectedExitCode: 0);
        Assert.Equal("result", completed.RootElement.GetProperty("type").GetString());
        var completedPayload = completed.RootElement.GetProperty("payload");
        Assert.Equal("completed", completedPayload.GetProperty("status").GetString());
        var completedContext = completedPayload.GetProperty("context");
        Assert.Equal(sourceWorkflowFile, completedContext.GetProperty("workflow_template_json").GetString());
        Assert.Equal("workflow-designer dispatched with relative-link context", completedContext.GetProperty("workflow_designer_dispatch_record").GetString());
        Assert.Equal("weave-out suitability review complete", completedContext.GetProperty("weave_out_subagent_review").GetProperty("summary").GetString());
        Assert.Equal("assets/target-skill-weave-out.agent.md", completedContext.GetProperty("target_skill_subagent_assets")[0].GetString());
        Assert.Equal("SKILL.md -> assets/target-skill-weave-out.agent.md", completedContext.GetProperty("target_skill_subagent_link_updates")[0].GetString());
        Assert.Equal("review-fix loop complete", completedContext.GetProperty("review_fix_loop_evidence").GetProperty("summary").GetString());
        Assert.Equal("ready", completedContext.GetProperty("commit_report_ready").GetProperty("status").GetString());
        Assert.Equal("assets/so-workflow/skill-plan.md", completedContext.GetProperty("skill_plan_md").GetString());
        Assert.Equal("assets/so-workflow/governance-notes.md", completedContext.GetProperty("governance_notes_md").GetString());
        Assert.Equal(runtimeCopyPath, completedContext.GetProperty("workflow_runtime_copy_json").GetString());
        Assert.Equal(Path.Combine(auditDirectory, "event.log"), completedContext.GetProperty("event_log_file").GetString());
        Assert.Equal("SKILL.md", completedContext.GetProperty("checked_in_skill_markdown_asset").GetString());
        Assert.Equal("assets/so-workflow/so-package-lock.json", completedContext.GetProperty("checked_in_package_lock_asset").GetString());
        Assert.Equal("assets/so-workflow/node-to-file-map.md", completedContext.GetProperty("node_to_file_map").GetString());
        var completionManifestPath = completedContext.GetProperty("completion_manifest_reference").GetString();
        Assert.False(string.IsNullOrWhiteSpace(completionManifestPath));
        Assert.True(File.Exists(completionManifestPath));
        Assert.Equal(completionManifestPath, completedContext.GetProperty("completion_manifest_md").GetString());
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
    public async Task CliCompile_MissingWorkflowPhase_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-missing-phase-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateWorkflowMissingPhase()));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("SO1000", run.StdOut);
        Assert.Contains("workflowPhase", run.StdOut);
        Assert.Contains("state.start", run.StdOut);
        Assert.Contains("overall workflow stage", run.StdOut);
        Assert.Contains("01 Intake", run.StdOut);
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
        Assert.True(first.Progressed);
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
        Assert.Contains("style legend_ai fill:#dcfce7,stroke:#16a34a,stroke-width:1px", mermaid);
        Assert.Contains("style legend_tool fill:#dbeafe,stroke:#2563eb,stroke-width:1px", mermaid);
        Assert.Contains("style legend_branch fill:#fef3c7,stroke:#a16207,stroke-width:1px", mermaid);
        Assert.Contains("style legend_optional fill:#fef3c7,stroke:#d97706,stroke-width:1px", mermaid);
        Assert.Contains("style legend_required fill:#fee2e2,stroke:#dc2626,stroke-width:1px", mermaid);
        Assert.Contains("style legend_gate fill:#f8fafc,stroke:#94a3b8,stroke-width:1px", mermaid);
        Assert.Contains("state.ai[\"🔎 AI\"]", mermaid);
        Assert.Contains("state.tool[\"⚙️ Tool\"]", mermaid);
        Assert.Contains("state.optional[\"💬 Optional\"]", mermaid);
        Assert.Contains("state.required[\"🚧 Required\"]", mermaid);
        Assert.Contains("state.done[\"📜 Done\"]", mermaid);
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

        Assert.Contains("state.branch[\"❓ Branch\"]", mermaid);
        Assert.Contains("style state.branch fill:#fef3c7,stroke:#a16207,stroke-width:1px", mermaid);
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
        Assert.Contains("state.review[\"📜 Review\"]", mermaid);
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
        using var resultEnvelope = ReadFinalSoEnvelope(resumeRun.StdOut);
        var resultPayload = resultEnvelope.RootElement.GetProperty("payload");
        var resultMustShowFiles = resultPayload.GetProperty("must_show_to_user_files").EnumerateArray().Select(static item => item.GetString()).ToArray();
        Assert.Contains(resultMustShowFiles, static path => path is not null && path.EndsWith("workflow.mermaid.md", StringComparison.Ordinal));
        Assert.Contains(resultMustShowFiles, static path => path is not null && path.EndsWith("workflow.html", StringComparison.Ordinal));
        Assert.Contains(resultMustShowFiles, static path => path is not null && path.EndsWith("workflow.analysis.json", StringComparison.Ordinal));
        Assert.Contains("SO workflow is completed", resultPayload.GetProperty("workflow_location_summary").GetString());
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
        Assert.Contains(Path.GetFullPath(workflowPath), errorMustShowFiles);
        Assert.Contains(Path.GetFullPath(resultFile), errorMustShowFiles);
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
        Assert.Contains("dotnet so.dll --patch", run.StdOut);
        Assert.Contains("--patch-content-file <path>", run.StdOut);
        Assert.Contains("--patch-target <path>", run.StdOut);
        Assert.Contains("--from-line <n>", run.StdOut);
        Assert.Contains("--to-line <n>", run.StdOut);
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
    [InlineData("--patch", "--patch-content-file")]
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
    public async Task CliGuide_ExportedGuide_DescribesPatchUsagePositioning()
    {
        var repoRoot = FindRepositoryRoot();
        var exportFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-guide-{Guid.NewGuid():N}.md");

        var run = await RunCliAsync(repoRoot, $"--guide --export \"{exportFile}\"");

        Assert.Equal(0, run.ExitCode);
        var guide = await File.ReadAllTextAsync(exportFile);
        Assert.Contains("GitHub Copilot", guide);
        Assert.Contains("direct line-range patch path", guide);
        Assert.Contains("fallback", guide);
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
        await AssertGuideExportedWorkflowJsonWorkaroundSemanticsAsync(
            language: null,
            noDirectEditText: "do not directly edit checked-in workflow JSON as a normal maintenance path",
            blockedWorkaroundText: "fully blocked and the user explicitly approves a narrow workaround",
            immediateReturnText: "immediately return to the Loom-governanced path",
            runFreshCopyText: "before a new official `run`",
            persistedCopyText: "same persisted runtime copy",
            resumeSameCopyText: "Resume continues against the same external runtime copy");
    }

    [Fact]
    public async Task CliGuide_ExportedGuide_ZhCn_DescribesBlockedOnlyWorkflowJsonWorkarounds()
    {
        await AssertGuideExportedWorkflowJsonWorkaroundSemanticsAsync(
            language: "zh-cn",
            noDirectEditText: "不要把直接修改 checked-in workflow JSON 当作常规维护路径",
            blockedWorkaroundText: "当前 `dotnet so.dll` 路径已经完全 blocked",
            immediateReturnText: "随后必须立刻回到 Loom 治理路径",
            runFreshCopyText: "每次启动新的正式 `run` 前",
            persistedCopyText: "同一份已持久化的 runtime copy",
            resumeSameCopyText: "Resume 持续作用于同一个外部 runtime copy");
    }

    private async Task AssertGuideExportedWorkflowJsonWorkaroundSemanticsAsync(
        string? language,
        string noDirectEditText,
        string blockedWorkaroundText,
        string immediateReturnText,
        string runFreshCopyText,
        string persistedCopyText,
        string resumeSameCopyText)
    {
        var repoRoot = FindRepositoryRoot();
        var exportDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-guide-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(exportDirectory);
        var exportFile = Path.Combine(exportDirectory, "so-guide.md");

        var command = string.IsNullOrWhiteSpace(language)
            ? $"--guide --export \"{exportFile}\""
            : $"--guide --lang {language} --export \"{exportFile}\"";

        var run = await RunCliAsync(repoRoot, command);

        Assert.Equal(0, run.ExitCode);
        var guide = await File.ReadAllTextAsync(exportFile);
        Assert.Contains(noDirectEditText, guide);
        Assert.Contains(blockedWorkaroundText, guide);
        Assert.Contains(immediateReturnText, guide);
        Assert.Contains(runFreshCopyText, guide);
        Assert.Contains(persistedCopyText, guide);
        Assert.Contains(resumeSameCopyText, guide);
        Assert.DoesNotContain("for every official `run` or `resume` attempt, clone the checked-in source workflow again", guide);
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
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(payloadWorkflow));

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
            WorkflowPhase = "Intake",
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
            WorkflowPhase = "Execution",
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
            WorkflowPhase = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var first = new CommandTransition
        {
            Id = "transition.first",
            Name = "First",
            WorkflowPhase = "Intake",
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
            WorkflowPhase = "Execution",
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
                WorkflowPhase = "Detached",
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
            new StateNode { Id = "state.ai", Name = "AI", WorkflowPhase = "Planning", Groups = [new TransitionGroup { Id = "group.ai", TransitionIds = ["transition.ai"] }] },
            new StateNode { Id = "state.tool", Name = "Tool", WorkflowPhase = "Execution", Groups = [new TransitionGroup { Id = "group.tool", TransitionIds = ["transition.tool"] }] },
            new StateNode { Id = "state.optional", Name = "Optional", WorkflowPhase = "Decision", Groups = [new TransitionGroup { Id = "group.optional", TransitionIds = ["transition.optional"] }] },
            new StateNode { Id = "state.required", Name = "Required", WorkflowPhase = "Review", Groups = [new TransitionGroup { Id = "group.required", TransitionIds = ["transition.required"] }] },
            new StateNode { Id = "state.done", Name = "Done", WorkflowPhase = "Done", Groups = [] },
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
            WorkflowPhase = "Decision",
            Groups = [new TransitionGroup { Id = "group.branch", TransitionIds = ["transition.branch"] }],
        };
        var done = new StateNode { Id = "state.done", Name = "Done", WorkflowPhase = "Done", Groups = [] };
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

    private static WorkflowInstance CreateWorkflowPhaseWorkflow()
    {
        var intake = new StateNode
        {
            Id = "state.intake",
            Name = "Intake",
            WorkflowPhase = "Intake",
            Groups = [new TransitionGroup { Id = "group.intake", TransitionIds = ["transition.intake"] }],
        };
        var plan = new StateNode
        {
            Id = "state.plan",
            Name = "Plan",
            WorkflowPhase = "Planning",
            Groups = [new TransitionGroup { Id = "group.plan", TransitionIds = ["transition.plan"] }],
        };
        var review = new StateNode
        {
            Id = "state.review",
            Name = "Review",
            WorkflowPhase = "Review",
            Groups = [],
        };

        var first = CreateCommandTransition("transition.intake", "Intake step", plan.Id, WorkflowStepKind.StateUpdate) with { WorkflowPhase = "Intake" };
        var second = CreateCommandTransition("transition.plan", "Plan step", review.Id, WorkflowStepKind.ModelThink) with { WorkflowPhase = "Planning" };

        return new WorkflowInstance
        {
            InstanceId = "phase-sample",
            StartNodeId = intake.Id,
            CurrentNodeId = intake.Id,
            EndNodeId = review.Id,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [intake.Id] = intake,
                [plan.Id] = plan,
                [review.Id] = review,
                [first.Id] = first,
                [second.Id] = second,
            },
        };
    }

    private static WorkflowInstance CreateWorkflowPhaseCollisionWorkflow()
    {
        var first = new StateNode
        {
            Id = "state.first",
            Name = "First",
            WorkflowPhase = "Plan A",
            Groups = [new TransitionGroup { Id = "group.first", TransitionIds = ["transition.first"] }],
        };
        var second = new StateNode
        {
            Id = "state.second",
            Name = "Second",
            WorkflowPhase = "Plan-A",
            Groups = [new TransitionGroup { Id = "group.second", TransitionIds = ["transition.second"] }],
        };
        var third = new StateNode
        {
            Id = "state.third",
            Name = "Third",
            WorkflowPhase = "Plan/A",
            Groups = [],
        };

        var firstTransition = CreateCommandTransition("transition.first", "First step", second.Id, WorkflowStepKind.StateUpdate) with { WorkflowPhase = "Plan A" };
        var secondTransition = CreateCommandTransition("transition.second", "Second step", third.Id, WorkflowStepKind.ModelThink) with { WorkflowPhase = "Plan-A" };

        return new WorkflowInstance
        {
            InstanceId = "phase-collision-sample",
            StartNodeId = first.Id,
            CurrentNodeId = first.Id,
            EndNodeId = third.Id,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [first.Id] = first,
                [second.Id] = second,
                [third.Id] = third,
                [firstTransition.Id] = firstTransition,
                [secondTransition.Id] = secondTransition,
            },
        };
    }

    private static WorkflowInstance CreateCheckedInAssetMemoryReadWorkflow(
        string? assetRootInput = "target_skill_path",
        string? assetRootPath = null,
        IReadOnlyList<object?>? checkedInAssets = null)
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups = [new TransitionGroup { Id = "group.inspect", TransitionIds = ["transition.inspect"] }],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
        };

        var inspect = CreateCommandTransition("transition.inspect", "Inspect assets", done.Id, WorkflowStepKind.MemoryRead) with
        {
            OutputPath = "inspection",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "Inspect assets",
                Parameters = BuildCheckedInAssetParameters(assetRootInput, assetRootPath, checkedInAssets),
            },
        };

        return new WorkflowInstance
        {
            InstanceId = "memory-read-assets",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [inspect.Id] = inspect,
            },
        };
    }

    private static Dictionary<string, object?> BuildCheckedInAssetParameters(
        string? assetRootInput,
        string? assetRootPath,
        IReadOnlyList<object?>? checkedInAssets)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["checkedInAssets"] = checkedInAssets?.ToList() ?? new List<object?> { "SKILL.md" },
        };

        if (assetRootInput is not null)
        {
            parameters["assetRootInput"] = assetRootInput;
        }

        if (assetRootPath is not null)
        {
            parameters["assetRootPath"] = assetRootPath;
        }

        return parameters;
    }

    private static WorkflowInstance CreateInvalidOutputBindingsWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups = [new TransitionGroup { Id = "group.run", TransitionIds = ["transition.run"] }],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var run = CreateCommandTransition("transition.run", "Run tool", done.Id, WorkflowStepKind.ToolCall) with
        {
            OutputPath = "tool.result",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "echo",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["message"] = "ok",
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["tool.bound"] = "$unknown",
                    },
                },
            },
        };

        return new WorkflowInstance
        {
            InstanceId = $"invalid-output-bindings-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [run.Id] = run,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static WorkflowInstance CreateSelfReferentialOutputBindingsWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups = [new TransitionGroup { Id = "group.run", TransitionIds = ["transition.run"] }],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var run = CreateCommandTransition("transition.run", "Run tool", done.Id, WorkflowStepKind.ToolCall) with
        {
            OutputPath = "tool.result",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "echo",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["message"] = "ok",
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["tool.result.copy"] = "$result",
                    },
                },
            },
        };

        return new WorkflowInstance
        {
            InstanceId = $"self-ref-output-bindings-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [run.Id] = run,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
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
            WorkflowPhase = "Execution",
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
            WorkflowPhase = "Done",
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
            WorkflowPhase = "Execution",
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
            WorkflowPhase = "Evaluation",
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
            WorkflowPhase = "Evaluation",
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
            WorkflowPhase = "Intake",
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
            WorkflowPhase = "Review",
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
            WorkflowPhase = "Done",
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
            WorkflowPhase = "Intake",
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
            WorkflowPhase = "Review",
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
            WorkflowPhase = "Assessment",
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
            WorkflowPhase = "Done",
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

    private static WorkflowInstance CreateWorkflowMissingPhase()
    {
        var workflow = CreateGovernedWorkflow();
        var start = Assert.IsType<StateNode>(workflow.Nodes["state.start"]);
        start.WorkflowPhase = null;
        workflow.Nodes[start.Id] = start;
        return workflow;
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
            WorkflowPhase = "Evaluation",
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
            WorkflowPhase = "Done",
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
            WorkflowPhase = "Evaluation",
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
            WorkflowPhase = "Loop",
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
            WorkflowPhase = "Loop",
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

    private static void EnsureWorkflowPhases(WorkflowInstance workflow, string fallbackPhase)
    {
        foreach (var state in workflow.GetStateNodes().Values)
        {
            if (!string.IsNullOrWhiteSpace(state.WorkflowPhase))
            {
                continue;
            }

            state.WorkflowPhase = string.Equals(state.Id, workflow.EndNodeId, StringComparison.Ordinal)
                ? "Done"
                : fallbackPhase;
        }
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

    private static string GetLoomSkillEnhancementRoot(string repoRoot)
    {
        var agentPath = Path.Combine(repoRoot, ".agents", "skills", "loom-skill-enhancement");
        if (Directory.Exists(agentPath))
        {
            return agentPath;
        }

        var githubPath = Path.Combine(repoRoot, ".github", "skills", "loom-skill-enhancement");
        if (Directory.Exists(githubPath))
        {
            return githubPath;
        }

        throw new DirectoryNotFoundException("Could not locate loom-skill-enhancement skill root in .agents/skills or .github/skills.");
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