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

public sealed class SkillOrchestratorGovernanceTests : SkillOrchestratorBehaviorTestBase
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

        Assert.True(run.ExitCode == 0, $"STDOUT:\n{run.StdOut}\nSTDERR:\n{run.StdErr}");
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
        var mermaidFile = Assert.Single(Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories));
        var mermaidMarkdown = await File.ReadAllTextAsync(mermaidFile);
        var summaryHeading = mermaidMarkdown.IndexOf("## Workflow Business Summary / 工作流业务说明", StringComparison.Ordinal);
        Assert.True(summaryHeading > mermaidMarkdown.LastIndexOf("```", StringComparison.Ordinal), "The business summary must follow the complete Mermaid code fence.");
        Assert.Contains("| Phase / 阶段 | State / 节点 | Node ID | Business purpose / 业务目的 |", mermaidMarkdown);
        Assert.Contains("| Runtime Proof | Start | state.start | Not provided / 未提供 |", mermaidMarkdown);
        var htmlFile = Assert.Single(Directory.GetFiles(auditDirectory, "workflow.html", SearchOption.AllDirectories));
        var auditHtml = await File.ReadAllTextAsync(htmlFile);
        Assert.Contains("Compile Audit / <span lang=\"zh-CN\">编译审计</span>", auditHtml);
        Assert.Contains("<span lang=\"zh-CN\">数据流证据</span>", auditHtml);
        Assert.Contains("Workflow SHA-256 / <span lang=\"zh-CN\">工作流哈希</span>", auditHtml);
        Assert.Contains("Control flow, ownership and gates / ", auditHtml);
        Assert.Contains("<span lang=\"zh-CN\">控制流</span>", auditHtml);
        Assert.Contains("<span lang=\"zh-CN\">归属与门禁</span>", auditHtml);
        Assert.Contains("Dataflow evidence / <span lang=\"zh-CN\">数据流证据</span>", auditHtml);
        Assert.Contains("Diagnostics / <span lang=\"zh-CN\">诊断</span>", auditHtml);
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

        Assert.True(run.ExitCode == 0, $"STDOUT:\n{run.StdOut}\nSTDERR:\n{run.StdErr}");
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
        Assert.Contains("assets/so-workflow/contract.json", strategySourceDocuments.Select(Convert.ToString));

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
        Assert.Contains("<execution-output-root>/plan/skill-plan.md", File.ReadAllText(Path.Combine(GetLoomSkillEnhancementRoot(repoRoot), "assets", "so-workflow", "contract.json")));

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

        var contractJson = File.ReadAllText(Path.Combine(GetLoomSkillEnhancementRoot(repoRoot), "assets", "so-workflow", "contract.json"));
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
}
