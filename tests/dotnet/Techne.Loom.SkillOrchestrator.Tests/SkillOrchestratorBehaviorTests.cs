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
        var feedbackFile = Assert.Single(Directory.GetFiles(auditDirectory, "workflow.compile-feedback.json", SearchOption.AllDirectories));
        using var feedbackDocument = JsonDocument.Parse(await File.ReadAllTextAsync(feedbackFile));
        Assert.Equal("workflow.compile-feedback.v1", feedbackDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("succeeded", feedbackDocument.RootElement.GetProperty("status").GetString());
        Assert.True((await File.ReadAllLinesAsync(feedbackFile)).Length > 1);
        Assert.True(Path.IsPathFullyQualified(feedbackFile));
        var analysisFile = Assert.Single(Directory.GetFiles(auditDirectory, "workflow.analysis.json", SearchOption.AllDirectories));
        var analysisJson = await File.ReadAllTextAsync(analysisFile);
        Assert.Contains("gate.assessment", analysisJson);
    }

    [Fact]
    public async Task CliCompile_GovernedBlockedPublisherWithoutGate_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-ungated-blocked-publisher-{Guid.NewGuid():N}.json");
        var workflow = CreateGovernedWorkflow();
        workflow.Nodes["transition.ungated_wait"] = new CommandTransition
        {
            Id = "transition.ungated_wait",
            Name = "Ungated blocked wait",
            StepKind = WorkflowStepKind.WaitResume,
            PublishesBlockedOutputFamilies = ["untracked_blocked_output"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "workflow.wait",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(workflow));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("ungated output publisher", run.StdOut);
        Assert.Contains("transition.ungated_wait", run.StdOut);
    }


    [Fact]
    public async Task AoGovernedTemplate_RequiresTruthyTerminalEvidenceGuards()
    {
        var workflowFile = Path.Combine(FindRepositoryRoot(), ".agents", "skills", "loom-plan-execution", "assets", "so-workflow", "so-template.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(workflowFile));
        var nodes = document.RootElement.GetProperty("nodes");

        var runTerminal = nodes.GetProperty("transition.route_run_terminal");
        Assert.Contains("context.Get<bool>(\"runResult.terminal_evidence\")", runTerminal.GetProperty("guardExpression").GetString());
        Assert.DoesNotContain("terminal_evidence != null", runTerminal.GetProperty("guardExpression").GetString());

        var runInvalid = nodes.GetProperty("transition.route_run_invalid");
        Assert.Contains("!context.Get<bool>(\"runResult.terminal_evidence\")", runInvalid.GetProperty("guardExpression").GetString());

        var resumeTerminal = nodes.GetProperty("transition.route_resume_terminal");
        Assert.Contains("context.Get<bool>(\"resumeResult.terminal_evidence\")", resumeTerminal.GetProperty("guardExpression").GetString());
        Assert.DoesNotContain("terminal_evidence != null", resumeTerminal.GetProperty("guardExpression").GetString());
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

        Assert.False(File.Exists(skillPlanFile));
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
        Assert.Contains("subgraph phase_04_shared_review_context[\"04 Shared Review Context\"]", mermaid);
        Assert.Contains("subgraph phase_05_reenhancement_review[\"05 Reenhancement Review\"]", mermaid);
        Assert.Contains("subgraph phase_06_planning[\"06 Planning\"]", mermaid);
        Assert.Contains("subgraph phase_09_review_and_repair[\"09 Review And Repair\"]", mermaid);
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
        Assert.Contains("transition.build_shared_review_context", analysisJson);
        Assert.Contains("transition.aggregate_reenhancement_findings", analysisJson);
        Assert.Contains("transition.aggregate_plan_findings", analysisJson);
        Assert.Contains("transition.aggregate_review_findings", analysisJson);
        Assert.Contains("transition.apply_batch_repair", analysisJson);
        Assert.Contains("transition.aggregate_post_fix_validation", analysisJson);
        Assert.Contains("transition.run_serial_validation", analysisJson);
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
        Assert.Contains("gate.bootstrap_shared_context", workflow.Validation.Gates.Keys);
        Assert.Contains("gate.bootstrap_reenhancement_batch", workflow.Validation.Gates.Keys);
        Assert.Contains("gate.bootstrap_plan_batch", workflow.Validation.Gates.Keys);
        Assert.Contains("gate.bootstrap_review_batch", workflow.Validation.Gates.Keys);
        Assert.Contains("gate.bootstrap_post_fix_validation", workflow.Validation.Gates.Keys);
        Assert.Contains("gate.bootstrap_serial_validation", workflow.Validation.Gates.Keys);
        Assert.Contains("gate.bootstrap_compile_review", workflow.Validation.Gates.Keys);
        Assert.Contains("gate.bootstrap_official_blocked", workflow.Validation.Gates.Keys);
        Assert.Contains("gate.bootstrap_official_done", workflow.Validation.Gates.Keys);
        Assert.Equal(["gate.bootstrap_official_done"], workflow.Validation.Routes["official_runnable_route"].RequiredTerminalGateIds);
        Assert.Equal(["gate.bootstrap_official_blocked"], workflow.Validation.Routes["official_runnable_route"].RequiredBlockedGateIds);
        Assert.Equal(["target_skill_path", "approval_decision", "feedback_notes"], workflow.Validation.DeclaredUserOwnedFields);
        Assert.Contains("workflow_file", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains("analysis_file", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains("governance_state", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains("resolved_so_runtime", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains("resolved_guide_surface", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains("reenhancement_template_strategy_review", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains("reenhancement_template_change_strategy", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains("reenhancement_template_change_evidence", workflow.Validation.ReservedRuntimeOwnedFields);
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.classify_governance");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.reenhancement_context");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.inspect_package_lock");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.inspect_workflow_assets");
        Assert.DoesNotContain(workflow.Nodes.Keys, id => id == "transition.select_latest_channel");
        Assert.DoesNotContain(workflow.Nodes.Keys, id => id == "state.latest_channel");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.enter_reenhancement_context");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.enter_shared_review_context");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.build_shared_review_context");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.aggregate_reenhancement_findings");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.aggregate_plan_findings");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.use_bound_runtime_path");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.inspect_existing_skill_markdown");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.inspect_existing_package_lock");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.inspect_existing_workflow_assets");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.reacquire_runtime");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.capture_guide");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.post_guide_decision");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.shared_review_context");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.post_shared_context_decision");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.reenhancement_gap_review");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.reenhancement_gap_aggregate");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.plan_aggregate");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.reenhancement_strategy_review");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.require_reenhancement_gap_review");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.skip_reenhancement_gap_review");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.compare_skill_markdown_against_latest_guide");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.compare_package_lock_against_latest_guide");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.compare_workflow_governance_against_latest_guide");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.judge_reenhancement_template_strategy");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.analyze_route_gate_structure");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.analyze_evidence_node_map");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.weave_out_subagent_review");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.review_weave_out_subagent_fit");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.aggregate_review_findings");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.apply_batch_repair");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.aggregate_post_fix_validation");
        Assert.Contains(workflow.Nodes.Keys, id => id == "transition.run_serial_validation");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.review_fix_loop");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.review_findings_aggregate");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.batch_repair");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.post_fix_validation");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.post_fix_validation_aggregate");
        Assert.Contains(workflow.Nodes.Keys, id => id == "state.serial_validation");
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
        Assert.Contains("shared_review_context", routeInputs.Select(Convert.ToString));
        Assert.DoesNotContain("plan.route_gate_review", routeInputs.Select(Convert.ToString));

        var analyzeEvidenceNodeMap = Assert.IsType<CommandTransition>(workflow.Nodes["transition.analyze_evidence_node_map"]);
        Assert.Equal(WorkflowStepKind.SubagentCall, analyzeEvidenceNodeMap.StepKind);
        Assert.Equal("assets/agents/loom-skill-enhancement-evidence-node-map-analysis.agent.md", Convert.ToString(analyzeEvidenceNodeMap.Command.Parameters!["subagentRelativePath"]));
        var evidenceInputs = Assert.IsAssignableFrom<IEnumerable<object?>>(analyzeEvidenceNodeMap.Command.Parameters["requiredInputs"]);
        Assert.Contains("shared_review_context", evidenceInputs.Select(Convert.ToString));
        Assert.DoesNotContain("plan.evidence_review", evidenceInputs.Select(Convert.ToString));
        Assert.DoesNotContain("plan.route_gate_review", evidenceInputs.Select(Convert.ToString));

        var reviewWeaveOutSubagentFit = Assert.IsType<CommandTransition>(workflow.Nodes["transition.review_weave_out_subagent_fit"]);
        Assert.Equal(WorkflowStepKind.SubagentCall, reviewWeaveOutSubagentFit.StepKind);
        Assert.Equal("assets/agents/loom-skill-enhancement-weave-out-subagent-fit-review.agent.md", Convert.ToString(reviewWeaveOutSubagentFit.Command.Parameters!["subagentRelativePath"]));
        Assert.Contains("weave_out_subagent_review", reviewWeaveOutSubagentFit.PublishesOutputFamilies ?? []);
        Assert.Contains("target_skill_subagent_assets", reviewWeaveOutSubagentFit.PublishesOutputFamilies ?? []);
        Assert.Contains("target_skill_subagent_link_updates", reviewWeaveOutSubagentFit.PublishesOutputFamilies ?? []);
        var weaveOutInputs = Assert.IsAssignableFrom<IEnumerable<object?>>(reviewWeaveOutSubagentFit.Command.Parameters["requiredInputs"]);
        Assert.Contains("SKILL.md", weaveOutInputs.Select(Convert.ToString));
        Assert.Contains("assets/so-workflow/node-to-file-map.md", weaveOutInputs.Select(Convert.ToString));

        var reviewGroup = Assert.Single(Assert.IsType<StateNode>(workflow.Nodes["state.review_fix_loop"]).Groups);
        Assert.Equal(ConcurrencyStrategy.All, reviewGroup.Strategy);
        Assert.Equal(4, reviewGroup.TransitionIds.Count);
        var aggregateReviewFindings = Assert.IsType<CommandTransition>(workflow.Nodes["transition.aggregate_review_findings"]);
        Assert.Equal("aggregated_review_findings", aggregateReviewFindings.OutputPath);
        Assert.Equal("assets/agents/loom-skill-enhancement-review-findings-aggregator.agent.md", Convert.ToString(aggregateReviewFindings.Command.Parameters!["subagentRelativePath"]));
        var batchRepair = Assert.IsType<CommandTransition>(workflow.Nodes["transition.apply_batch_repair"]);
        Assert.Equal("batch_repair_evidence", batchRepair.OutputPath);
        Assert.Equal("assets/agents/loom-skill-enhancement-review-fix-loop.agent.md", Convert.ToString(batchRepair.Command.Parameters!["subagentRelativePath"]));
        var serialValidation = Assert.IsType<CommandTransition>(workflow.Nodes["transition.run_serial_validation"]);
        Assert.Equal(WorkflowStepKind.WaitResume, serialValidation.StepKind);
        Assert.Contains("serial_validation_evidence", serialValidation.PublishesOutputFamilies ?? []);
        Assert.Contains("review_fix_loop_evidence", serialValidation.PublishesOutputFamilies ?? []);
        Assert.Contains("commit_report_ready", serialValidation.PublishesOutputFamilies ?? []);        var draftTemplate = Assert.IsType<CommandTransition>(workflow.Nodes["transition.draft_template"]);
        Assert.Contains("workflow_template_json", draftTemplate.PublishesOutputFamilies ?? []);
        Assert.Contains("workflow_designer_dispatch_record", draftTemplate.PublishesOutputFamilies ?? []);
        Assert.Contains("workflow_design_evidence", draftTemplate.PublishesOutputFamilies ?? []);
        Assert.Contains("reference_manifest", draftTemplate.PublishesOutputFamilies ?? []);
        Assert.Contains("static_contract_review", draftTemplate.PublishesOutputFamilies ?? []);
        Assert.Contains("semantic_probe_report", draftTemplate.PublishesOutputFamilies ?? []);
        var draftParameters = Assert.IsAssignableFrom<IDictionary<string, object?>>(draftTemplate.Command.Parameters);
        var draftBindings = Assert.IsAssignableFrom<IDictionary<string, object?>>(draftParameters["outputBindings"]);
        Assert.Equal("$context:workflow_design_evidence", Convert.ToString(draftBindings["workflow_design_evidence"]));
        Assert.Equal("$context:reference_manifest", Convert.ToString(draftBindings["reference_manifest"]));
        Assert.Equal("$context:static_contract_review", Convert.ToString(draftBindings["static_contract_review"]));
        Assert.Equal("$context:semantic_probe_report", Convert.ToString(draftBindings["semantic_probe_report"]));
        Assert.Equal(WorkflowStepKind.SubagentCall, draftTemplate.StepKind);
        Assert.Equal("assets/agents/loom-skill-enhancement-workflow-designer.agent.md", Convert.ToString(draftTemplate.Command.Parameters!["subagentRelativePath"]));

        var enterReenhancementContext = Assert.IsType<ExpressionTransition>(workflow.Nodes["transition.enter_reenhancement_context"]);
        Assert.Equal(WorkflowStepKind.ConditionBranch, enterReenhancementContext.StepKind);
        Assert.Equal("context.Get<string>(\"governance_state\") == \"already_so_enhanced\"", enterReenhancementContext.GuardExpression.Source);

        var useBoundRuntimePath = Assert.IsType<ExpressionTransition>(workflow.Nodes["transition.use_bound_runtime_path"]);
        Assert.Equal(WorkflowStepKind.ConditionBranch, useBoundRuntimePath.StepKind);
        Assert.Equal("context.Get<string>(\"governance_state\") != \"already_so_enhanced\"", useBoundRuntimePath.GuardExpression.Source);

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
        Assert.Equal("state.reenhancement_gap_aggregate", compareWorkflowGovernanceAgainstLatestGuide.TargetNodeId);
        var reenhancementGroup = Assert.Single(Assert.IsType<StateNode>(workflow.Nodes["state.reenhancement_gap_review"]).Groups);
        Assert.Equal(ConcurrencyStrategy.All, reenhancementGroup.Strategy);
        Assert.Equal(3, reenhancementGroup.TransitionIds.Count);

        var judgeReenhancementTemplateStrategy = Assert.IsType<CommandTransition>(workflow.Nodes["transition.judge_reenhancement_template_strategy"]);
        Assert.Equal(WorkflowStepKind.SubagentCall, judgeReenhancementTemplateStrategy.StepKind);
        Assert.Equal("reenhancement_template_strategy_review", judgeReenhancementTemplateStrategy.OutputPath);
        Assert.Equal("assets/agents/loom-skill-enhancement-reenhancement-conflict-judgment.agent.md", Convert.ToString(judgeReenhancementTemplateStrategy.Command.Parameters!["subagentRelativePath"]));
        Assert.Contains("reenhancement_template_change_strategy", judgeReenhancementTemplateStrategy.PublishesOutputFamilies ?? []);
        Assert.Contains("reenhancement_template_change_evidence", judgeReenhancementTemplateStrategy.PublishesOutputFamilies ?? []);
        var strategyInputs = Assert.IsAssignableFrom<IEnumerable<object?>>(judgeReenhancementTemplateStrategy.Command.Parameters!["requiredInputs"]);
        Assert.Contains("aggregated_reenhancement_findings", strategyInputs.Select(Convert.ToString));
        Assert.Contains("shared_review_context", strategyInputs.Select(Convert.ToString));
        Assert.Contains("requested_target_skill_changes", strategyInputs.Select(Convert.ToString));
        var strategySourceDocuments = Assert.IsAssignableFrom<IEnumerable<object?>>(judgeReenhancementTemplateStrategy.Command.Parameters!["source_documents"]);
        Assert.Contains("assets/so-workflow/so-template.json", strategySourceDocuments.Select(Convert.ToString));
        Assert.Contains("reference/so-skill-reference.md", strategySourceDocuments.Select(Convert.ToString));
        Assert.Contains("contract.json", strategySourceDocuments.Select(Convert.ToString));

        var compileTemplate = Assert.IsType<CommandTransition>(workflow.Nodes["transition.compile_template"]);
        Assert.Equal(WorkflowStepKind.WaitResume, compileTemplate.StepKind);
        Assert.Equal(["gate.bootstrap_compile_review"], compileTemplate.SatisfiesGateIds);

        var compileTemplateParameters = Assert.IsAssignableFrom<IDictionary<string, object?>>(compileTemplate.Command.Parameters);
        var compileTemplateInputs = Assert.IsAssignableFrom<IEnumerable<object?>>(compileTemplateParameters["requiredInputs"]);
        Assert.Contains("mermaid_delivery", compileTemplateInputs.Select(Convert.ToString));
        var compileTemplateBindings = Assert.IsAssignableFrom<IDictionary<string, object?>>(compileTemplateParameters["outputBindings"]);
        Assert.Equal("$context:mermaid_delivery", Convert.ToString(compileTemplateBindings["mermaid_delivery"]));
    Assert.Equal(WorkflowStepKind.WaitResume, compileTemplate.StepKind);
        Assert.Equal(["gate.bootstrap_compile_review"], compileTemplate.SatisfiesGateIds);

        var acceptOfficialRunnable = Assert.IsType<ExpressionTransition>(workflow.Nodes["transition.accept_official_runnable"]);
        Assert.Equal(WorkflowStepKind.ConditionBranch, acceptOfficialRunnable.StepKind);
        Assert.Equal("context.Get<string>(\"approval_decision\") == \"approve_official_runnable\"", acceptOfficialRunnable.GuardExpression.Source);

        var routeOfficialRunnableAfterReview = Assert.IsType<ExpressionTransition>(workflow.Nodes["transition.route_official_runnable_after_review"]);
        Assert.Equal(WorkflowStepKind.ConditionBranch, routeOfficialRunnableAfterReview.StepKind);
        Assert.Contains("commit_report_ready.status", routeOfficialRunnableAfterReview.GuardExpression.Source);
        Assert.Contains("serial_validation_evidence", routeOfficialRunnableAfterReview.GuardExpression.Source);

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
        Assert.Contains("mermaid_delivery", waitRuntime.PublishesBlockedOutputFamilies ?? []);
        var waitRuntimeRequiredInputs = Assert.IsAssignableFrom<IEnumerable<object?>>(waitRuntimeParameters["requiredInputs"]);
        Assert.Contains("mermaid_delivery", waitRuntimeRequiredInputs.Select(Convert.ToString));
        var waitRuntimeArtifactFamilies = Assert.IsAssignableFrom<IEnumerable<object?>>(waitRuntimeParameters["auditArtifactFamilies"]);
        Assert.Contains("mermaid_delivery", waitRuntimeArtifactFamilies.Select(Convert.ToString));
        var waitRuntimeBindings = Assert.IsAssignableFrom<IDictionary<string, object?>>(waitRuntimeParameters["outputBindings"]);
        Assert.Equal("$context:mermaid_delivery", Convert.ToString(waitRuntimeBindings["mermaid_delivery"]));

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
        Assert.Contains("mermaid_delivery", finalizeLock.PublishesOutputFamilies ?? []);
        var finalizeBindings = Assert.IsAssignableFrom<IDictionary<string, object?>>(finalizeLock.Command.Parameters!["outputBindings"]);
        Assert.Equal("$context:mermaid_delivery", Convert.ToString(finalizeBindings["mermaid_delivery"]));

        var officialDoneGate = workflow.Validation.Gates["gate.bootstrap_official_done"];
        Assert.Contains("mermaid_delivery", workflow.Validation.Gates["gate.bootstrap_official_blocked"].RequiredOutputFamilies);
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
        Assert.Contains("transition.judge_reenhancement_template_strategy", nodeMap);
        Assert.Contains("loom-skill-enhancement-reenhancement-conflict-judgment.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-skill-markdown-gap-review.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-package-lock-gap-review.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-workflow-governance-gap-review.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-weave-out-subagent-fit-review.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-review-fix-loop.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-review-findings-aggregator.agent.md", nodeMap);
        Assert.Contains("transition.build_shared_review_context", nodeMap);
        Assert.Contains("transition.apply_batch_repair", nodeMap);
        Assert.Contains("transition.run_serial_validation", nodeMap);
        Assert.Contains("loom-skill-enhancement-scope-input-output-analysis.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-route-gate-analysis.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-evidence-node-map-analysis.agent.md", nodeMap);
        Assert.Contains("loom-skill-enhancement-workflow-designer.agent.md", nodeMap);
        Assert.Contains("transition.review_weave_out_subagent_fit", nodeMap);
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
        Assert.Contains("shared context", nodeMap);

        Assert.DoesNotContain("assets/so-workflow/skill-plan.md", nodeMap);
        Assert.Contains("<execution-output-root>/plan/skill-plan.md", File.ReadAllText(Path.Combine(GetLoomSkillEnhancementRoot(repoRoot), "contract.json")));

        var skillMarkdown = File.ReadAllText(Path.Combine(GetLoomSkillEnhancementRoot(repoRoot), "SKILL.md"));
        Assert.Contains("checked-in lock reference target", skillMarkdown);
        Assert.Contains("runtime-owned completion-manifest reference", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-skill-markdown-gap-review.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-package-lock-gap-review.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-workflow-governance-gap-review.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-weave-out-subagent-fit-review.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-review-fix-loop.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-review-findings-aggregator.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-workflow-designer.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-scope-input-output-analysis.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-route-gate-analysis.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-evidence-node-map-analysis.agent.md", skillMarkdown);
        Assert.Contains("loom-skill-enhancement-reenhancement-conflict-judgment.agent.md", skillMarkdown);

        var contractJson = File.ReadAllText(Path.Combine(GetLoomSkillEnhancementRoot(repoRoot), "contract.json"));
        Assert.DoesNotContain("\"guide_language\"", contractJson);
        Assert.Contains("English-only", contractJson);
        Assert.Contains("checked_in_package_lock_asset", contractJson);
        Assert.Contains("checked_in_skill_markdown_asset", contractJson);
        Assert.Contains("completion_manifest_reference", contractJson);
        Assert.Contains("completion_manifest_md", contractJson);
        Assert.Contains("workflow_designer_dispatch_record", contractJson);
        Assert.Contains("weave_out_subagent_review", contractJson);
        Assert.Contains("review_fix_loop_evidence", contractJson);
        Assert.Contains("shared_review_context", contractJson);
        Assert.Contains("aggregated_review_findings", contractJson);
        Assert.Contains("batch_repair_evidence", contractJson);
        Assert.Contains("serial_validation_evidence", contractJson);
        Assert.Contains("commit_report_ready", contractJson);
        Assert.Contains("reenhancement_template_strategy_policy", contractJson);
        Assert.Contains("reenhancement_template_change_strategy", contractJson);
        Assert.Contains("reenhancement_template_change_evidence", contractJson);
    }

    [Fact]
    public void WorkflowInstanceCloner_PreservesValidationContract()
    {
        var workflow = CreateGovernedWorkflow();
        var clone = WorkflowInstanceCloner.Clone(workflow);

        Assert.Equal(workflow.TemplateKind, clone.TemplateKind);
        Assert.NotNull(clone.Validation);
        Assert.Contains("gate.assessment", clone.Validation!.Gates.Keys);
        Assert.Contains("gate.bootstrap_mcp_ready", clone.Validation.Gates.Keys);
        Assert.Equal(workflow.Validation!.GovernanceEntry!.EvidenceFamily, clone.Validation.GovernanceEntry!.EvidenceFamily);
        Assert.Equal(workflow.Validation!.GovernanceEntry!.RuntimeLaunchDescriptorField, clone.Validation.GovernanceEntry.RuntimeLaunchDescriptorField);
        Assert.Equal("context.Has(\"assessment_summary_json\") && context.Has(\"assessment_report_md\")", clone.Validation.Gates["gate.assessment"].PassExpression!.Source);
        Assert.Equal(workflow.Validation!.Routes.Keys, clone.Validation.Routes.Keys);
    }

    [Fact]
    public async Task StartOrAdvanceAsync_EvaluatesGatePredicateAndRequiredOutputs()
    {
        static WorkflowInstance CreateInstance(string instanceId, bool publishOutput)
        {
            var start = new StateNode
            {
                Id = "state.start",
                Name = "Start",
                WorkflowPhase = "Gate Test",
                Groups =
                [
                    new TransitionGroup
                    {
                        Id = "group.emit",
                        TransitionIds = ["transition.emit"],
                    },
                ],
            };
            var done = new StateNode
            {
                Id = "state.done",
                Name = "Done",
                WorkflowPhase = "Done",
                Groups = [],
            };
            var emit = new CommandTransition
            {
                Id = "transition.emit",
                Name = "Emit gated output",
                TargetNodeId = done.Id,
                StepKind = WorkflowStepKind.ToolCall,
                SucceedExpression = "true",
                OutputPath = "artifact",
                SatisfiesGateIds = ["gate.output"],
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.Tool,
                    Name = publishOutput ? "echo" : "noop",
                    Parameters = publishOutput
                        ? new Dictionary<string, object?>(StringComparer.Ordinal) { ["message"] = "ready" }
                        : new Dictionary<string, object?>(StringComparer.Ordinal),
                },
            };

            return new WorkflowInstance
            {
                InstanceId = instanceId,
                TemplateKind = "explicit-workflow-graph",
                Validation = new WorkflowValidationContract
                {
                    Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
                    {
                        ["gate.output"] = new WorkflowValidationGate
                        {
                            PassExpression = "context.Get<bool>(\"gate_outputs_present\")",
                            RequiredOutputFamilies = [],
                            RequiredMachineReadableOutputFamilies = ["artifact"],
                            RequiredHumanReviewableOutputFamilies = ["artifact"],
                        },
                    },
                },
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
            };
        }

        static async Task<WorkflowStatus> RunAsync(WorkflowInstance instance)
        {
            var store = new InMemoryInstanceStore();
            await store.SaveNewAsync(instance);
            var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));
            var result = await service.StartOrAdvanceAsync(instance.InstanceId);
            return result.StatusProjection.Status;
        }

        Assert.Equal(WorkflowStatus.Failed, await RunAsync(CreateInstance("gate-fail", publishOutput: false)));
        Assert.Equal(WorkflowStatus.Succeeded, await RunAsync(CreateInstance("gate-pass", publishOutput: true)));
    }
    [Fact]
    public async Task RuntimeGateFallback_UsesCommandGateIdsWhenTopLevelListIsEmpty()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Gate fallback",
            Groups = [new TransitionGroup { Id = "group.emit", TransitionIds = ["transition.emit"] }],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "Done",
            Groups = [],
        };
        var emit = new CommandTransition
        {
            Id = "transition.emit",
            Name = "Emit",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.ToolCall,
            OutputPath = "artifact",
            SucceedExpression = "true",
            SatisfiesGateIds = [],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["satisfiesGateIds"] = new List<object?> { "gate.output" },
                },
            },
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "gate-fallback",
            TemplateKind = "explicit-workflow-graph",
            Validation = new WorkflowValidationContract
            {
                Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
                {
                    ["gate.output"] = new WorkflowValidationGate
                    {
                        PassExpression = "context.Get<bool>(\"gate_outputs_present\")",
                        RequiredOutputFamilies = ["artifact"],
                    },
                },
            },
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
        };

        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var service = new DefaultWorkflowTaskTrackingService(new DefaultTaskTrackingEngine(store));
        var result = await service.StartOrAdvanceAsync(instance.InstanceId);

        Assert.Equal(WorkflowStatus.Failed, result.StatusProjection.Status);
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
            ["skill_plan_md"] = "output/exec-test/plan/skill-plan.md",
            ["workflow_file"] = workflowFile,
            ["workflow_template_json"] = workflowFile,
            ["workflow_designer_dispatch_record"] = "workflow-designer dispatched with relative-link context",
            ["workflow_mermaid_md"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.mermaid.md"),
            ["workflow_html"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.html"),
            ["workflow_analysis_json"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.analysis.json"),
            ["workflow_dataflow_json"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.dataflow.json"),
            ["weave_out_subagent_review"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["summary"] = "weave-out suitability review complete",
            },
            ["target_skill_subagent_assets"] = new[] { "assets/target-skill-weave-out.agent.md" },
            ["target_skill_subagent_link_updates"] = new[] { "SKILL.md -> assets/target-skill-weave-out.agent.md" },
            ["internal_document_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["source"] = "target-local document inspection",
            },
            ["review_fix_loop_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["summary"] = "parallel review, aggregate, batch repair, parallel validation, and serial validation complete",
            },
            ["batch_repair_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = "complete",
            },
            ["aggregated_post_fix_validation"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = "passed",
            },
                        ["serial_validation_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = "passed",
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
            ["workflow_dataflow_json"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.dataflow.json"),
            ["mermaid_delivery"] = CreateMermaidDeliveryEvidence(),
        };

        await service.ResumeAsync(instance.InstanceId, "transition.wait_runtime", null, resumePayload);

        var finalTick = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.Equal(WorkflowStatus.Succeeded, finalTick.StatusProjection.Status);

        var saved = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(saved);
        Assert.Equal("output/exec-test/plan/skill-plan.md", Convert.ToString(saved!.Context["skill_plan_md"]));
        Assert.Equal("assets/so-workflow/governance-notes.md", Convert.ToString(saved.Context["governance_notes_md"]));
        Assert.Equal("SKILL.md", Convert.ToString(saved.Context["checked_in_skill_markdown_asset"]));
        Assert.Equal("assets/so-workflow/so-package-lock.json", Convert.ToString(saved.Context["checked_in_package_lock_asset"]));
        Assert.Equal("assets/so-workflow/node-to-file-map.md", Convert.ToString(saved.Context["node_to_file_map"]));
        Assert.Equal(Path.Combine(Path.GetTempPath(), ".tmp").TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(Convert.ToString(saved.Context["completion_manifest_reference"]))?.TrimEnd(Path.DirectorySeparatorChar), ignoreCase: true);
        Assert.Equal("output/exec-test/plan/skill-plan.md", Convert.ToString(saved.Context["skill_plan_md"]));
        Assert.Equal("parallel review, aggregate, batch repair, parallel validation, and serial validation complete", Convert.ToString(((IDictionary<string, object?>)saved.Context["review_fix_loop_evidence"]!)["summary"]));
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
            ["skill_plan_md"] = "output/exec-test/plan/skill-plan.md",
            ["workflow_file"] = workflowFile,
            ["workflow_template_json"] = workflowFile,
            ["workflow_designer_dispatch_record"] = "workflow-designer dispatched with relative-link context",
            ["workflow_mermaid_md"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.mermaid.md"),
            ["workflow_html"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.html"),
            ["workflow_analysis_json"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.analysis.json"),
            ["workflow_dataflow_json"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.dataflow.json"),
            ["weave_out_subagent_review"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["summary"] = "weave-out suitability review complete",
            },
            ["target_skill_subagent_assets"] = new[] { "assets/target-skill-weave-out.agent.md" },
            ["target_skill_subagent_link_updates"] = new[] { "SKILL.md -> assets/target-skill-weave-out.agent.md" },
            ["internal_document_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["source"] = "target-local document inspection",
            },
            ["review_fix_loop_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["summary"] = "parallel review, aggregate, batch repair, parallel validation, and serial validation complete",
            },
            ["batch_repair_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = "complete",
            },
            ["aggregated_post_fix_validation"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = "passed",
            },
                        ["serial_validation_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = "passed",
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
            ["workflow_dataflow_json"] = Path.Combine(Path.GetTempPath(), $"techne-loom-self-bootstrap-{Guid.NewGuid():N}.dataflow.json"),
            ["mermaid_delivery"] = CreateMermaidDeliveryEvidence(),
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
    public void LoomSkillEnhancementTemplateGateExpressionsRejectFailedMermaidDelivery()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(GetLoomSkillEnhancementRoot(repoRoot), "assets", "so-workflow", "so-template.json");
        var workflow = WorkflowJsonSerializer.Deserialize(File.ReadAllText(workflowFile));
        Assert.NotNull(workflow.Validation);
        var validation = workflow.Validation!;
        var compiler = new CSharpExpressionCompiler();
        var gateIds = new[]
        {
            "gate.bootstrap_compile_review",
            "gate.bootstrap_official_blocked",
            "gate.bootstrap_official_done",
        };

        foreach (var gateId in gateIds)
        {
            var gate = validation.Gates[gateId];
            Assert.NotNull(gate.PassExpression);
            var gateExpression = gate.PassExpression!;
            var compiled = compiler.Compile(workflow.ExpressionBinding, gateExpression, $"validation.gates.{gateId}/passExpression");
            Assert.True(compiled.IsSuccess, compiled.Feedback.Message);

            foreach (var scenario in new[]
            {
                (Status: "workspace_mirror", ArtifactGenerated: true, Expected: true),
                (Status: "runtime_path_only", ArtifactGenerated: true, Expected: true),
                (Status: "delivery_failed", ArtifactGenerated: false, Expected: false),
                (Status: "workspace_mirror", ArtifactGenerated: false, Expected: false),
                (Status: "unknown", ArtifactGenerated: true, Expected: false),
            })
            {
                var context = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var family in gate.RequiredOutputFamilies.Concat(gate.RequiredMachineReadableOutputFamilies).Concat(gate.RequiredHumanReviewableOutputFamilies).Distinct(StringComparer.Ordinal))
                {
                    context[family] = family == "workflow_compile_feedback"
                        ? new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = "succeeded" }
                        : gate.ValueSemantics.TryGetValue(family, out var semantic) && semantic == "nonEmptyObject"
                            ? new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = "evidence" }
                            : true;
                }

                context["mermaid_delivery"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = scenario.Status,
                    ["artifact_generated"] = scenario.ArtifactGenerated,
                    ["link_resolvable"] = scenario.Status == "workspace_mirror" && scenario.ArtifactGenerated,
                };

                Assert.Equal(scenario.Expected, compiled.Execute!(new ExpressionRuntimeContext(context)));
            }
        }
    }

    [Fact]
    public void LoomEnhancedResearchReleasedDemo_Uses03282BusinessWorkflowAssets()
    {
        var repoRoot = FindRepositoryRoot();
        var demoRoot = Path.Combine(repoRoot, "demos", "loom-enhanced-research", "4. Released-0.3.282", "loom-enhanced-research");
        var skillFile = Path.Combine(demoRoot, "SKILL.md");
        var contractFile = Path.Combine(demoRoot, "contract.json");
        var lockFile = Path.Combine(demoRoot, "assets", "so-workflow", "so-package-lock.json");
        var templateFile = Path.Combine(demoRoot, "assets", "so-workflow", "so-template.json");
        var manifestFile = Path.Combine(demoRoot, "assets", "so-workflow", "reference", "document-copy-manifest.json");

        Assert.True(File.Exists(skillFile));
        Assert.True(File.Exists(contractFile));
        Assert.True(File.Exists(lockFile));
        Assert.True(File.Exists(templateFile));
        Assert.True(File.Exists(manifestFile));
        Assert.Contains("0.3.282", File.ReadAllText(skillFile));
        Assert.DoesNotContain("0.2.118", File.ReadAllText(skillFile));

        using var lockDocument = JsonDocument.Parse(File.ReadAllText(lockFile));
        Assert.Equal("0.3.282", lockDocument.RootElement.GetProperty("resolved_version").GetString());
        Assert.False(lockDocument.RootElement.TryGetProperty("package_id", out _));
        Assert.False(lockDocument.RootElement.TryGetProperty("channel", out _));
        Assert.False(lockDocument.RootElement.TryGetProperty("runtime_bundle", out _));

        var workflow = WorkflowJsonSerializer.Deserialize(File.ReadAllText(templateFile));
        Assert.Equal("research_generation", workflow.TaskType);
        Assert.Equal("target_skill_business", workflow.WorkflowKind);
        Assert.Equal("state.start", workflow.CurrentNodeId);
        var mcp = Assert.IsType<CommandTransition>(workflow.Nodes["transition.start_mcp"]);
        Assert.Equal(WorkflowStepKind.McpCall, mcp.StepKind);
        Assert.Equal("mcp_startup_evidence", mcp.OutputPath);
        Assert.Equal("so_inspect_workflow_fragment", mcp.Command.Name);
        Assert.Equal("0.3.282", File.ReadAllText(Path.Combine(demoRoot, "assets", "so-workflow", "reference", "runtime-semantic-migration.md")).Contains("0.3.282") ? "0.3.282" : null);
        Assert.DoesNotContain("governance_entry_evidence", File.ReadAllText(templateFile));

        foreach (var script in new[]
        {
            "convert-noop-to-stateupdate.js",
            "strip-result-bindings.js",
            "audit-output-family-producers.js",
            "verify-migration-idempotence.js",
        })
        {
            Assert.True(File.Exists(Path.Combine(demoRoot, "assets", "so-workflow", "scripts", script)), script);
        }

        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(manifestFile));
        Assert.Equal("0.3.282", manifestDocument.RootElement.GetProperty("target_bound_version").GetString());
        Assert.Equal("released", manifestDocument.RootElement.GetProperty("target_bound_channel").GetString());
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

        var manifestPath = Path.Combine(skillRoot, "assets", "so-workflow", "reference", "document-copy-manifest.json");
        using var manifestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        var manifestRoot = manifestDocument.RootElement;
        var packageVersion = manifestRoot.GetProperty("target_bound_version").GetString()
            ?? throw new InvalidOperationException("Document-copy manifest did not contain target_bound_version.");
        var packageRid = manifestRoot.GetProperty("documents")[0].GetProperty("source_package_rid").GetString()
            ?? throw new InvalidOperationException("Document-copy manifest did not contain source_package_rid.");
        var packageRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-so-runtime-package-{Guid.NewGuid():N}");
        var runtimeRoot = Path.Combine(packageRoot, "tools", packageRid);
        var packageGuideRoot = Path.Combine(runtimeRoot, "docs", "en", "guides");
        Directory.CreateDirectory(packageGuideRoot);
        File.WriteAllText(
            Path.Combine(packageGuideRoot, "so-guide-reference-contracts.md"),
            ReadPackageGuideBody(
                Path.Combine(skillRoot, "assets", "so-workflow", "reference", "so", "runtime-contracts.md"),
                "This target-local file is the complete SO contracts page extracted from the exact published runtime package. It supports this skill but does not replace the fresh SO guide returned by `dotnet so.dll --guide`."));
        File.WriteAllText(
            Path.Combine(packageGuideRoot, "so-guide-reference-governance.md"),
            ReadPackageGuideBody(
                Path.Combine(skillRoot, "assets", "so-workflow", "reference", "so", "runtime-governance.md"),
                "This target-local file is the complete SO governance page extracted from the exact published runtime package. It is supporting context, not a replacement for the fresh SO guide."));
        await File.WriteAllTextAsync(
            Path.Combine(runtimeRoot, "runtime.json"),
            JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["schema"] = "techne-loom-runtime-v1",
                ["product"] = "so",
                ["package_id"] = $"Techne.Loom.SkillOrchestrator.Runtime.{packageRid}",
                ["version"] = packageVersion,
                ["rid"] = packageRid,
                ["docs_root"] = $"tools/{packageRid}/docs/en",
            }));

        var workflow = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(sourceWorkflowFile));
        var workflowAssetInspection = Assert.IsType<CommandTransition>(workflow.Nodes["transition.inspect_existing_workflow_assets"]);
        workflowAssetInspection.Command.Parameters!["documentCopySourceRootPath"] = packageRoot;
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(workflow));
        await File.WriteAllTextAsync(
            contextFile,
            JsonSerializer.Serialize(
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["target_skill_path"] = skillRoot,
                    ["requested_target_skill_changes"] = "refresh the self-bootstrap workflow from the current requirements and guide",
                },
                WorkflowJsonSerializer.CreateDefaultOptions(indented: false)));

        async Task<JsonDocument> ResumeAndReadEnvelopeAsync(string transitionId, Dictionary<string, object?> payload, int expectedExitCode = 3)
        {
            payload.TryAdd("mermaid_delivery", CreateMermaidDeliveryEvidence());
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
            if (run.ExitCode != expectedExitCode)
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), $"techne-loom-clirun-resume-{transitionId}-dump.txt"), $"exit={run.ExitCode}\nSTDOUT:\n{run.StdOut}\nSTDERR:\n{run.StdErr}");
            }
            Assert.Equal(expectedExitCode, run.ExitCode);
            return ReadFinalSoEnvelope(run.StdOut);
        }

        static string[] ReadRequiredInputs(JsonElement payload)
            => payload.GetProperty("required_inputs").EnumerateArray().Select(static item => item.GetString() ?? string.Empty).ToArray();

        static string ReadPackageGuideBody(string targetPath, string intro)
        {
            const string endMarker = "<!-- loom-document-copy:end -->";
            var text = File.ReadAllText(targetPath);
            var prefix = $"{endMarker}\n\n{intro}\n\n";
            var bodyStart = text.IndexOf(prefix, StringComparison.Ordinal);
            if (bodyStart < 0)
            {
                throw new InvalidOperationException($"Target-local package copy '{targetPath}' did not contain its provenance header and intro.");
            }

            return text[(bodyStart + prefix.Length)..];
        }

        var firstRun = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\" --context-file \"{contextFile}\" --audit-output \"{auditDirectory}\"");
        if (firstRun.ExitCode != 3)
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "techne-loom-clirun-first-run-dump.txt"), $"exit={firstRun.ExitCode}\nSTDOUT:\n{firstRun.StdOut}\nSTDERR:\n{firstRun.StdErr}");
        }
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
                       ["governance_entry_transport"] = "mcp_stdio",
                       ["mcp_registration_attempt_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                       {
                           ["status"] = "ready",
                           ["mcp_attempted"] = true,
                           ["config_attempted"] = true,
                       },
                       ["runtime_launch_descriptor_ref"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                       {
                           ["launch_file"] = "so.dll",
                           ["host"] = "dotnet",
                           ["exact_version"] = "1.2.3",
                       },
                   }))
        {
            var payload = thirdBoundary.RootElement.GetProperty("payload");
            Assert.Equal("McpCall", payload.GetProperty("current_step_kind").GetString());
            Assert.Contains("mcp_startup_evidence", ReadRequiredInputs(payload));
         }

        using (var mcpBoundary = await ResumeAndReadEnvelopeAsync(
                   "transition.start_mcp",
                   new Dictionary<string, object?>(StringComparer.Ordinal)
                   {
                       ["mcp_startup_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                       {
                           ["transport"] = "stdio",
                           ["initialized"] = true,
                           ["tool_called"] = true,
                           ["tool_name"] = "so_inspect_workflow_fragment",
                           ["workflow_file"] = workflowPath,
                           ["fragment_bounded"] = true,
                       },
                   }))
                 {
                    var payload = mcpBoundary.RootElement.GetProperty("payload");
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

        using var reenhancementAggregateBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.aggregate_reenhancement_findings",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "complete",
                    ["reenhancement_skill_markdown_gap_review"] = "skill markdown gap review complete",
                    ["reenhancement_package_lock_gap_review"] = "package lock gap review complete",
                    ["reenhancement_workflow_gap_review"] = "workflow governance gap review complete",
                    ["findings"] = new[] { "all re-enhancement findings preserved" },
                },
            });
        Assert.Equal("SubagentCall", reenhancementAggregateBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var strategyBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.judge_reenhancement_template_strategy",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["strategy"] = "full_regeneration",
                    ["summary"] = "the old template conflicts with the requested workflow changes",
                    ["impact_scope"] = "holistic",
                    ["baseline_inputs"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["old_template"] = sourceWorkflowFile,
                        ["current_requirements"] = "requested_target_skill_changes",
                        ["concept_documents"] = new[] { "contract.json", "reference/so-skill-reference.md" },
                        ["latest_guide"] = "guide://so/en/latest",
                    },
                    ["evidence_references"] = new[] { "assets/so-workflow/so-template.json", "contract.json" },
                },
            });
        Assert.Equal("SubagentCall", strategyBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var eighthBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.analyze_scope",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["skill_plan_md"] = "output/exec-test/plan/skill-plan.md",
                    ["review_plan_md"] = "# Review plan\n",
                    ["resolved_guide_surface_ref"] = "guide://so/en/latest",
                    ["package_index_links_ref"] = "packages.released.md",
                },
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

        using var planAggregateBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.aggregate_plan_findings",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "complete",
                    ["scope_analysis"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["summary"] = "scope analysis complete",
                    },
                    ["plan"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["route_gate_review"] = "route gate review complete",
                        ["evidence_review"] = "evidence node map review complete",
                    },
                    ["skill_plan_md"] = "output/exec-test/plan/skill-plan.md",
                    ["review_plan_md"] = "# Review plan\n",
                    ["package_index_links_ref"] = "packages.released.md",
                    ["resolved_guide_surface_ref"] = "guide://so/en/latest",
                    ["findings"] = new[] { "all planning findings preserved" },
                },
            });
        Assert.Equal("SubagentCall", planAggregateBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());
        using var eleventhBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.draft_template",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["reference_pack_manifest"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["schemaVersion"] = "workflow-designer.reference-manifest.v1",
                    ["runtimeVersion"] = "0.3.258-beta",
                    ["generationSetId"] = "test-generation-set",
                    ["entries"] = Array.Empty<object?>(),
                },
                ["schema_demo_input"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["runtime"] = "so",
                    ["runtimeBinding"] = "dotnet-so",
                    ["runtimeVersion"] = "0.3.258-beta",
                    ["generationSetId"] = "test-generation-set",
                    ["schemaFile"] = "workflow.schema.json",
                    ["demoFile"] = "workflow.demo.json",
                    ["demoCompileAudit"] = "workflow.demo.compile.audit.json",
                    ["schemaSha256"] = new string('d', 64),
                    ["demoSha256"] = new string('e', 64),
                },
                ["workflow_design_output_root"] = Path.Combine(auditDirectory, "workflow-design"),
                ["workflow_template_json"] = sourceWorkflowFile,
                ["workflow_designer_dispatch_record"] = "workflow-designer dispatched with relative-link context",
                ["gate_failure_guidance_review"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "verified",
                    ["gates"] = new[] { "gate.bootstrap_plan", "gate.bootstrap_runtime_ready", "gate.bootstrap_runtime_guide", "gate.bootstrap_reenhancement_strategy", "gate.bootstrap_compile_review", "gate.bootstrap_official_blocked", "gate.bootstrap_official_done" },
                },
                ["workflow_design_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["reference_manifest"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["path"] = Path.Combine(auditDirectory, "workflow-design", "reference-manifest.json"),
                        ["sha256"] = "reference-hash",
                        ["schemaVersion"] = "workflow-designer.reference-manifest.v1",
                        ["verdict"] = "passed",
                        ["runtimeVersion"] = "0.3.258-beta",
                        ["generationSetId"] = "test-generation-set",
                    },
                    ["static_contract_review"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["path"] = Path.Combine(auditDirectory, "workflow-design", "static-contract-review.json"),
                        ["sha256"] = "static-hash",
                        ["schemaVersion"] = "workflow-designer.static-contract-review.v1",
                        ["verdict"] = "passed",
                        ["runtimeVersion"] = "0.3.258-beta",
                    },
                    ["semantic_probe_report"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["path"] = Path.Combine(auditDirectory, "workflow-design", "semantic-probe-report.json"),
                        ["sha256"] = "probe-hash",
                        ["schemaVersion"] = "workflow-designer.semantic-probe-report.v1",
                        ["verdict"] = "passed",
                        ["runtimeVersion"] = "0.3.258-beta",
                    },
                },
                ["reference_manifest"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = Path.Combine(auditDirectory, "workflow-design", "reference-manifest.json"),
                    ["sha256"] = "reference-hash",
                    ["schemaVersion"] = "workflow-designer.reference-manifest.v1",
                    ["verdict"] = "passed",
                    ["runtimeVersion"] = "0.3.258-beta",
                    ["generationSetId"] = "test-generation-set",
                },
                ["static_contract_review"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = Path.Combine(auditDirectory, "workflow-design", "static-contract-review.json"),
                    ["sha256"] = "static-hash",
                    ["schemaVersion"] = "workflow-designer.static-contract-review.v1",
                    ["verdict"] = "passed",
                    ["runtimeVersion"] = "0.3.258-beta",
                },
                ["semantic_probe_report"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = Path.Combine(auditDirectory, "workflow-design", "semantic-probe-report.json"),
                    ["sha256"] = "probe-hash",
                    ["schemaVersion"] = "workflow-designer.semantic-probe-report.v1",
                    ["verdict"] = "passed",
                    ["runtimeVersion"] = "0.3.258-beta",
                },
                ["reference_authority_decision"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = "passed" },
                ["layered_static_validation"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = "passed" },
                ["expression_audit"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = "passed" },
                ["projection_matrix"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = "passed" },
                ["gate_producer_route_matrix"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = "passed" },
                ["previous_runnable_reference_disposition"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = "not_applicable" },
            });
        Assert.Equal("WaitResume", eleventhBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var twelfthBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.compile_template",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["workflow_mermaid_md"] = Path.Combine(auditDirectory, "workflow.mermaid.md"),
                ["workflow_html"] = Path.Combine(auditDirectory, "workflow.html"),
                ["workflow_analysis_json"] = Path.Combine(auditDirectory, "workflow.analysis.json"),
                ["workflow_dataflow_json"] = Path.Combine(auditDirectory, "workflow.dataflow.json"),

                ["workflow_compile_feedback"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = "succeeded" },
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

        using var preRepairSkillBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.review_skill_markdown_before_repair",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "complete",
                    ["findings"] = new[] { "skill markdown finding" },
                },
            });
        Assert.Equal("SubagentCall", preRepairSkillBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var preRepairLockBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.review_package_lock_before_repair",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "complete",
                    ["findings"] = new[] { "package lock finding" },
                },
            });
        Assert.Equal("SubagentCall", preRepairLockBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var preRepairWorkflowBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.review_workflow_governance_before_repair",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "complete",
                    ["findings"] = new[] { "workflow governance finding" },
                },
            });
        Assert.Equal("SubagentCall", preRepairWorkflowBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var preRepairEvidenceBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.review_evidence_node_map_before_repair",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "complete",
                    ["findings"] = new[] { "evidence mapping finding" },
                },
            });
        Assert.Equal("SubagentCall", preRepairEvidenceBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var reviewAggregateBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.aggregate_review_findings",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "complete",
                    ["findings"] = new[] { "skill markdown finding", "package lock finding", "workflow governance finding", "evidence mapping finding" },
                    ["accepted_findings"] = new[] { "skill markdown finding", "package lock finding" },
                    ["rebutted_findings"] = new[] { "workflow governance finding" },
                    ["needs_validation_findings"] = new[] { "evidence mapping finding" },
                },
            });
        Assert.Equal("SubagentCall", reviewAggregateBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var batchRepairBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.apply_batch_repair",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "repaired",
                    ["changed_files"] = new[] { "SKILL.md", "assets/so-workflow/so-template.json", "assets/so-workflow/node-to-file-map.md" },
                    ["finding_to_change_mapping"] = new[] { "all findings considered together" },
                },
            });
        Assert.Equal("SubagentCall", batchRepairBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var postFixSkillBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.validate_skill_markdown_after_repair",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "passed",
                    ["findings"] = Array.Empty<string>(),
                },
            });
        Assert.Equal("SubagentCall", postFixSkillBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var postFixLockBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.validate_package_lock_after_repair",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "passed",
                    ["findings"] = Array.Empty<string>(),
                },
            });
        Assert.Equal("SubagentCall", postFixLockBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var postFixWorkflowBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.validate_workflow_governance_after_repair",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "passed",
                    ["findings"] = Array.Empty<string>(),
                },
            });
        Assert.Equal("SubagentCall", postFixWorkflowBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var postFixEvidenceBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.validate_evidence_node_map_after_repair",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "passed",
                    ["findings"] = Array.Empty<string>(),
                },
            });
        Assert.Equal("SubagentCall", postFixEvidenceBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var postFixAggregateBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.aggregate_post_fix_validation",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "passed",
                    ["validator_results"] = new[] { "skill markdown", "package lock", "workflow governance", "evidence node map" },
                    ["residual_blockers"] = Array.Empty<string>(),
                    ["preserved_strengths"] = new[] { "MCP-first", "same-copy execution" },
                },
            });
        Assert.Equal("WaitResume", postFixAggregateBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());

        using var serialValidationBoundary = await ResumeAndReadEnvelopeAsync(
            "transition.run_serial_validation",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "passed",
                    ["json_check"] = "passed",
                    ["graph_dataflow_check"] = "passed",
                    ["compile_check"] = "passed",
                    ["schema_demo_compile_check"] = "passed",
                    ["ordered_runtime_check"] = "passed",
                    ["runtime_semantic_probe_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["status"] = "passed",
                        ["summary"] = "0.3.282 inherited and replacement probes reached final Done",
                    },
                    ["batch_migration_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["status"] = "passed",
                        ["summary"] = "migration dry scan and idempotence verification passed",
                    },
                    ["decision_evidence_manifest"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["status"] = "passed",
                        ["summary"] = "durable decision evidence indexed",
                    },
                    ["review_fix_loop_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["status"] = "complete",
                        ["summary"] = "parallel review, aggregate, batch repair, parallel validation, and serial validation complete",
                    },
                    ["commit_report_ready"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["status"] = "ready",
                        ["summary"] = "commit report ready",
                    },
                },
            });
        Assert.Equal("WaitResume", serialValidationBoundary.RootElement.GetProperty("payload").GetProperty("current_step_kind").GetString());
        Assert.Contains("workflow_runtime_copy_json", ReadRequiredInputs(serialValidationBoundary.RootElement.GetProperty("payload")));
        Assert.Contains("event_log_file", ReadRequiredInputs(serialValidationBoundary.RootElement.GetProperty("payload")));
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
                        ["workflow_dataflow_json"] = Path.Combine(auditDirectory, "workflow.dataflow.json"),
                        ["mermaid_delivery"] = CreateMermaidDeliveryEvidence(),
                    },
                },
                WorkflowJsonSerializer.CreateDefaultOptions(indented: false)));

        var mismatchRun = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowPath}\" --result-file \"{mismatchResultFile}\" --audit-output \"{auditDirectory}\"");
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
                ["workflow_dataflow_json"] = Path.Combine(auditDirectory, "workflow.dataflow.json"),
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
        Assert.Equal("parallel review, aggregate, batch repair, parallel validation, and serial validation complete", completedContext.GetProperty("review_fix_loop_evidence").GetProperty("summary").GetString());
        Assert.Equal("ready", completedContext.GetProperty("commit_report_ready").GetProperty("status").GetString());
        Assert.Equal("output/exec-test/plan/skill-plan.md", completedContext.GetProperty("skill_plan_md").GetString());
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
        Assert.Contains("dotnet so.dll inspect-workflow-fragment", run.StdOut);
        Assert.Contains("bounded JSON Pointer fragment", run.StdOut);
        Assert.Contains("fragment is null and truncation metadata explains why", run.StdOut);
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
        Assert.Contains("direct line-range patch path", referenceContracts);
        var behaviorPath = Path.Combine(docsRoot, "guides", "so-guide-reference-behavior.md");
        Assert.True(File.Exists(behaviorPath));
        Assert.Contains("same persisted runtime copy", await File.ReadAllTextAsync(behaviorPath));
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
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-so-workspace-{Guid.NewGuid():N}");
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
        var mermaidFile = audit.GetProperty("mermaid_file").GetString()!;
        var htmlFile = audit.GetProperty("html_file").GetString()!;
        var workspaceMermaidFile = delivery.GetProperty("workspace_mermaid_file").GetString()!;
        var workspaceHtmlFile = delivery.GetProperty("workspace_html_file").GetString()!;
        Assert.True(File.Exists(mermaidFile));
        Assert.True(File.Exists(htmlFile));
        Assert.True(File.Exists(audit.GetProperty("workflow_backup_file").GetString()));
        Assert.True(File.Exists(audit.GetProperty("analysis_file").GetString()));
        Assert.True(File.Exists(audit.GetProperty("dataflow_file").GetString()));
        Assert.True(File.Exists(workspaceMermaidFile));
        Assert.True(File.Exists(workspaceHtmlFile));
        Assert.Equal(Path.GetRelativePath(workspaceRoot, workspaceMermaidFile).Replace('\\', '/'), delivery.GetProperty("workspace_relative_mermaid_file").GetString());
        Assert.Equal(Path.GetRelativePath(workspaceRoot, workspaceHtmlFile).Replace('\\', '/'), delivery.GetProperty("workspace_relative_html_file").GetString());
        Assert.True((await File.ReadAllBytesAsync(mermaidFile)).SequenceEqual(await File.ReadAllBytesAsync(workspaceMermaidFile)));
        Assert.True((await File.ReadAllBytesAsync(htmlFile)).SequenceEqual(await File.ReadAllBytesAsync(workspaceHtmlFile)));
        var mustShowFiles = payload.GetProperty("must_show_to_user_files").EnumerateArray().Select(static item => item.GetString()).ToArray();
        Assert.Equal(workspaceMermaidFile, mustShowFiles[0]);
        Assert.Equal(workspaceHtmlFile, mustShowFiles[1]);
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

    private sealed record DocumentCopyManifestFixture(
        string TargetSkillPath,
        string ManifestRelativePath,
        string MapRelativePath,
        string DocumentRelativePath,
        string PackageRoot);

    private static async Task<DocumentCopyManifestFixture> CreateDocumentCopyManifestFixtureAsync(
        string? sourceSha256 = null,
        string lockVersion = "0.3.253-beta",
        string contentMode = "full-document",
        string sourceVersion = "0.3.253-beta",
        string? sourcePackageId = "Techne.Loom.SkillOrchestrator.Runtime.win-x64",
        string? sourcePackageRid = "win-x64",
        string? sourcePackagePath = "tools/win-x64/docs/en/guides/so-guide-reference-contracts.md",
        string targetDocumentContent = "# Contract reference\n# Source contract\n",
        bool includeDocumentInMap = true,
        string? mapExtraPath = null,
        bool includePackageRoot = true)
    {
        var targetSkillPath = Path.Combine(Path.GetTempPath(), $"techne-loom-document-manifest-{Guid.NewGuid():N}");
        var referenceRoot = Path.Combine(targetSkillPath, "assets", "so-workflow", "reference", "so");
        var packageRoot = Path.Combine(targetSkillPath, "extracted-package");
        var runtimeRoot = Path.Combine(packageRoot, "tools", "win-x64");
        var packageGuidePath = Path.Combine(runtimeRoot, "docs", "en", "guides", "so-guide-reference-contracts.md");
        var packageRuntimeManifestPath = Path.Combine(runtimeRoot, "runtime.json");
        var sourceRelativePath = "docs/en/guides/so-guide-reference-contracts.md";
        var sourcePath = Path.Combine(targetSkillPath, sourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var packageLockPath = Path.Combine(targetSkillPath, "assets", "so-workflow", "so-package-lock.json");
        var manifestRelativePath = "assets/so-workflow/reference/document-copy-manifest.json";
        var mapRelativePath = "assets/so-workflow/node-to-file-map.md";
        var documentRelativePath = "assets/so-workflow/reference/so/runtime-contracts.md";
        var manifestPath = Path.Combine(targetSkillPath, manifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var mapPath = Path.Combine(targetSkillPath, mapRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var documentPath = Path.Combine(targetSkillPath, documentRelativePath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(referenceRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        if (includePackageRoot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(packageGuidePath)!);
        }
        await File.WriteAllTextAsync(documentPath, targetDocumentContent);
        await File.WriteAllTextAsync(sourcePath, "# Source contract\n");
        if (includePackageRoot)
        {
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
        }
        var sourceHashPath = includePackageRoot ? packageGuidePath : sourcePath;
        var actualSourceSha256 = ComputeCanonicalDocumentHash(sourceHashPath);
        await File.WriteAllTextAsync(packageLockPath, JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["package_id"] = "Techne.Loom.SkillOrchestrator",
            ["channel"] = "beta",
            ["resolved_version"] = lockVersion,
        }));

        var mapContent = "# Node To File Map" + Environment.NewLine
            + "All checked-in document paths in this map are relative to the target skill root." + Environment.NewLine
            + "| Node | File |" + Environment.NewLine
            + "| --- | --- |" + Environment.NewLine
            + "| inspect | `assets/so-workflow/reference/document-copy-manifest.json`";
        if (includeDocumentInMap)
        {
            mapContent += " and `assets/so-workflow/reference/so/runtime-contracts.md`";
        }
        if (!string.IsNullOrWhiteSpace(mapExtraPath))
        {
            mapContent += " and `" + mapExtraPath + "`";
        }
        mapContent += " |" + Environment.NewLine;
        await File.WriteAllTextAsync(mapPath, mapContent);

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
                    ["source_path"] = sourceRelativePath,
                    ["source_product"] = "so",
                    ["source_channel"] = "beta",
                    ["source_version"] = sourceVersion,
                    ["source_sha256"] = sourceSha256 ?? actualSourceSha256,
                    ["source_package_id"] = sourcePackageId,
                    ["source_package_rid"] = sourcePackageRid,
                    ["source_package_path"] = sourcePackagePath,
                    ["content_mode"] = contentMode,
                    ["artifact_origin"] = "verified-copy",
                    ["authority_scope"] = "target-local context only",
                    ["refreshed_by"] = "test",
                },
            },
        };
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));

        return new DocumentCopyManifestFixture(targetSkillPath, manifestRelativePath, mapRelativePath, documentRelativePath, packageRoot);
    }
    private static WorkflowInstance CreateCheckedInAssetMemoryReadWorkflow(
        string? assetRootInput = "target_skill_path",
        string? assetRootPath = null,
        IReadOnlyList<object?>? checkedInAssets = null,
        string? documentCopyManifestPath = null,
        string? nodeToFileMapPath = null,
        string? documentCopySourceRootPath = null)
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
                Parameters = BuildCheckedInAssetParameters(assetRootInput, assetRootPath, checkedInAssets, documentCopyManifestPath, nodeToFileMapPath, documentCopySourceRootPath),
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
        IReadOnlyList<object?>? checkedInAssets,
        string? documentCopyManifestPath,
        string? nodeToFileMapPath,
        string? documentCopySourceRootPath)
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

        if (documentCopyManifestPath is not null)
        {
            parameters["documentCopyManifestPath"] = documentCopyManifestPath;
        }

        if (nodeToFileMapPath is not null)
        {
            parameters["nodeToFileMapPath"] = nodeToFileMapPath;
        }

        if (documentCopySourceRootPath is not null)
        {
            parameters["documentCopySourceRootPath"] = documentCopySourceRootPath;
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
            TaskType = "skill_enhancement",
            WorkflowKind = "target_skill_enhancement",
            CaseId = "test-case",
            RunId = "test-run",
            Validation = new WorkflowValidationContract
            {
                DeclaredUserOwnedFields = ["plan_confirmation"],
                ReservedRuntimeOwnedFields = ["workflow_file"],
                Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
                {
                    ["gate.workflow"] = new WorkflowValidationGate { PassExpression = "context.Has(\"workflow_json\")", RequiredOutputFamilies = ["workflow_json"] },
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
            SucceedExpression = "context.Get<bool>(\"review.approved\")",
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
        const string evidencePredicate = "context.Has(\"mcp_startup_evidence\") && context.Get<string>(\"mcp_startup_evidence.transport\") == \"stdio\" && context.Get<bool>(\"mcp_startup_evidence.initialized\") == true && context.Get<bool>(\"mcp_startup_evidence.tool_called\") == true && context.Get<string>(\"mcp_startup_evidence.tool_name\") == \"so_inspect_workflow_fragment\" && context.Get<string>(\"mcp_startup_evidence.workflow_file\") != null && context.Get<bool>(\"mcp_startup_evidence.fragment_bounded\") == true";
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Runtime Proof",
            Groups = [new TransitionGroup { Id = "group.runtime", TransitionIds = ["transition.runtime_preflight"] }],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };
        var governanceEntry = new StateNode
        {
            Id = "state.governance_entry",
            Name = "Governance Entry",
            WorkflowPhase = "Runtime Proof",
            Groups = [new TransitionGroup { Id = "group.governance_entry", TransitionIds = ["transition.mcp_first"] }],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };
        var assessment = new StateNode
        {
            Id = "state.assessment",
            Name = "Assessment",
            WorkflowPhase = "Assessment",
            Groups = [new TransitionGroup { Id = "group.emit", TransitionIds = ["transition.emit_assessment"] }],
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
        var runtimePreflight = new CommandTransition
        {
            Id = "transition.runtime_preflight",
            Name = "Runtime preflight",
            TargetNodeId = governanceEntry.Id,
            OutputPath = "resolved_so_runtime",
            StepKind = WorkflowStepKind.WaitResume,
            GuardExpression = "true",
            SucceedExpression = "context.Has(\"resolved_so_runtime\")",
            PublishesOutputFamilies = ["runtime_preflight_result", "mcp_registration_attempt_evidence", "governance_entry_transport", "runtime_launch_descriptor_ref"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "workflow.reacquireRuntimeBundle",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["mcpPreflightExempt"] = true,
                    ["runtimePreflight"] = true,
                    ["mcpRegistrationRequired"] = true,
                    ["runtimeLaunchDescriptorOutput"] = "runtime_launch_descriptor_ref",
                    ["runtimeLaunchSelection"] = "runtime_owned",
                    ["mcpConfigFormats"] = new object?[] { "vscode", "claude" },
                    ["mcpConfigOutputDirectory"] = "<execution-output-root>/mcp-registration",
                    ["mcpRegistrationAttemptOutput"] = "mcp_registration_attempt_evidence",
                    ["resumeOutputKey"] = "resolved_so_runtime",
                    ["projectionMode"] = "canonical",
                    ["requiredInputs"] = new object?[] { "resolved_so_runtime", "runtime_preflight_result", "mcp_registration_attempt_evidence", "governance_entry_transport", "runtime_launch_descriptor_ref" },
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["runtime_preflight_result"] = "$context:runtime_preflight_result",
                        ["mcp_registration_attempt_evidence"] = "$context:mcp_registration_attempt_evidence",
                        ["governance_entry_transport"] = "$context:governance_entry_transport",
                        ["runtime_launch_descriptor_ref"] = "$result",
                    },
                },
            },
        };
        var mcpFirst = new CommandTransition
        {
            Id = "transition.mcp_first",
            Name = "Use MCP governance entry",
            TargetNodeId = assessment.Id,
            OutputPath = "mcp_startup_evidence",
            StepKind = WorkflowStepKind.McpCall,
            GuardExpression = "context.Get<string>(\"governance_entry_transport\") == \"mcp_stdio\" && context.Get<string>(\"mcp_registration_attempt_evidence.status\") == \"ready\" && context.Get<bool>(\"mcp_registration_attempt_evidence.mcp_attempted\") == true",
            SucceedExpression = evidencePredicate,
            SatisfiesGateIds = ["gate.bootstrap_mcp_ready"],
            PublishesOutputFamilies = ["mcp_startup_evidence"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "so_inspect_workflow_fragment",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["governanceEntry"] = true,
                    ["mcpFirst"] = true,
                    ["entryTransport"] = "mcp_stdio",
                    ["transport"] = "stdio",
                    ["requiredTool"] = "so_inspect_workflow_fragment",
                    ["runtimeLaunchDescriptorInput"] = "runtime_launch_descriptor_ref",
                    ["runtimeLaunchSelection"] = "runtime_owned",
                    ["mcpConfigRequired"] = true,
                    ["mcpConfigFormats"] = new object?[] { "vscode", "claude" },
                    ["mcpConfigOutputDirectory"] = "<execution-output-root>/mcp-registration",
                    ["mcpRegistrationAttemptInput"] = "mcp_registration_attempt_evidence",
                    ["resumeOutputKey"] = "mcp_startup_evidence",
                    ["projectionMode"] = "canonical",
                    ["workflowFileInput"] = "current_external_workflow_copy",
                    ["runtimeCommand"] = "dotnet so.dll mcp stdio",
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["mcp_startup_evidence"] = "$result",
                    },
                    ["requiredInputs"] = new object?[] { "mcp_startup_evidence", "runtime_launch_descriptor_ref", "mcp_registration_attempt_evidence" },
                },
            },
        };
        var emit = new CommandTransition
        {
            Id = "transition.emit_assessment",
            Name = "Emit assessment",
            Description = "Publish machine-readable and human-reviewable assessment outputs.",
            TargetNodeId = done.Id,
            OutputPath = "assessment_summary_json",
            StepKind = WorkflowStepKind.ArtifactEmit,
            TerminalRoutes = ["evaluation_only"],
            SatisfiesGateIds = ["gate.assessment"],
            PublishesOutputFamilies = ["assessment_summary_json", "assessment_report_md"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "workflow.emitAssessment",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = Path.Combine(Path.GetTempPath(), "techne-loom-assessment.md"),
                    ["content"] = "assessment",
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["assessment_report_md"] = "$context:assessment_summary_json",
                    },
                },
            },
        };
        return new WorkflowInstance
        {
            InstanceId = $"governed-valid-{Guid.NewGuid():N}",
            TemplateKind = "so-governed-target-skill",
            TaskType = "skill_enhancement",
            WorkflowKind = "target_skill_enhancement",
            CaseId = "test-case",
            RunId = "test-run",
            RuntimeBinding = "dotnet-so",
            Validation = CreateGovernedValidationContract(),
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [governanceEntry.Id] = governanceEntry,
                [assessment.Id] = assessment,
                [done.Id] = done,
                [runtimePreflight.Id] = runtimePreflight,
                [mcpFirst.Id] = mcpFirst,

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
                        PassExpression = "context.Get<bool>(\"gate_outputs_present\")",
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
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = Path.Combine(Path.GetTempPath(), "techne-loom-assessment.md"),
                    ["content"] = "assessment",
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["assessment_report_md"] = "$context:assessment_summary_json",
                    },
                },
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
                        PassExpression = "context.Get<bool>(\"gate_outputs_present\")",
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
            GovernanceEntry = new WorkflowGovernanceEntryContract(),
            Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
            {
                ["gate.bootstrap_mcp_ready"] = new WorkflowValidationGate
                {
                    Description = "The MCP-first governance entry must be complete before governed work.",
                    PassExpression = "context.Has(\"mcp_startup_evidence\") && context.Get<bool>(\"mcp_startup_evidence.fragment_bounded\") == true",
                    RequiredOutputFamilies = ["mcp_startup_evidence"],
                    RequiredMachineReadableOutputFamilies = ["mcp_startup_evidence"],
                    ValueSemantics = new Dictionary<string, string>(StringComparer.Ordinal) { ["mcp_startup_evidence"] = "nonEmptyObject" },
                    InstanceBinding = "current_workflow_instance",
                    FailureGuidance = new WorkflowGateFailureGuidance
                    {
                        Summary = "The governance-entry fragment inspection is incomplete.",
                        NextAction = "Try MCP registration first using the runtime descriptor; if MCP is unavailable before dispatch, use the same descriptor for the allowed CLI backup and retry.",
                        EvidenceReferences = [new WorkflowEvidenceReference { Path = "tests/dotnet/Techne.Loom.SkillOrchestrator.Tests/SkillOrchestratorBehaviorTests.cs", StartLine = 1, EndLine = 1, Quote = "using Techne.Loom.Abstractions.TaskTracking.Model;" }],
                    },
                },
                ["gate.assessment"] = new WorkflowValidationGate
                {
                    Description = "Assessment deliverables gate.",
                    InstanceBinding = "current_workflow_instance",
                    PassExpression = "context.Has(\"assessment_summary_json\") && context.Has(\"assessment_report_md\")",
                    RequiredOutputFamilies = ["assessment_summary_json", "assessment_report_md"],
                    RequiredMachineReadableOutputFamilies = ["assessment_summary_json"],
                    RequiredHumanReviewableOutputFamilies = ["assessment_report_md"],
                    ValueSemantics = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["assessment_summary_json"] = "nonEmptyString",
                        ["assessment_report_md"] = "nonEmptyString",
                    },
                    FailureGuidance = new WorkflowGateFailureGuidance
                    {
                        Summary = "Assessment evidence is incomplete.",
                        NextAction = "Publish both assessment output families and retry the gate.",
                        EvidenceReferences = [new WorkflowEvidenceReference
                        {
                            Path = "tests/dotnet/Techne.Loom.SkillOrchestrator.Tests/SkillOrchestratorBehaviorTests.cs",
                            StartLine = 1,
                            EndLine = 1,
                            Quote = "using Techne.Loom.Abstractions.TaskTracking.Model;",
                        }],
                    },
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
            SucceedExpression = "context.Get<bool>(\"review.approved\")",
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

    private static string ComputeCanonicalDocumentHash(string path)
    {
        var content = File.ReadAllText(path)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd();
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    private static Dictionary<string, object?> CreateMermaidDeliveryEvidence()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = "workspace_mirror",
            ["generation_status"] = "fresh",
            ["artifact_generated"] = true,
            ["link_resolvable"] = true,
        };
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
