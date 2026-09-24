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

public sealed class SkillOrchestratorExecutionTests : SkillOrchestratorBehaviorTestBase
{
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
                ["target_skill_contract_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["parsed"] = true,
                },
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
                ["target_skill_contract_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["parsed"] = true,
                },
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

                if (context.TryGetValue("internal_document_evidence", out var internalDocumentEvidence)
                    && internalDocumentEvidence is IDictionary<string, object?> internalEvidence)
                {
                    internalEvidence["target_skill_contract_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["parsed"] = true,
                    };
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
    public void LoomSkillEnhancementMermaidDeliveryReference_RequiresVerifiedPresentationPaths()
    {
        var skillRoot = GetLoomSkillEnhancementRoot(FindRepositoryRoot());
        var relativeFiles = new[]
        {
            "reference/mermaid-artifact-delivery.md",
            "assets/so-workflow/contract.json",
            "reference/so-skill-reference.md",
            "reference/packages.beta.md",
            "reference/packages.released.md",
        };

        foreach (var relativeFile in relativeFiles)
        {
            var content = File.ReadAllText(Path.Combine(skillRoot, relativeFile));
            Assert.Contains("not_emitted", content, StringComparison.Ordinal);
            Assert.Contains("runtime_path_only", content, StringComparison.Ordinal);
            Assert.Contains("delivery_failed", content, StringComparison.Ordinal);
            Assert.Contains("workspace-relative", content, StringComparison.OrdinalIgnoreCase);
            if (relativeFile is "reference/mermaid-artifact-delivery.md" or "assets/so-workflow/contract.json")
            {
                Assert.Contains("link_resolvable", content, StringComparison.Ordinal);
                Assert.True(
                    content.Contains("host-derived", StringComparison.OrdinalIgnoreCase) || content.Contains("host-only", StringComparison.OrdinalIgnoreCase),
                    $"{relativeFile} must describe host-derived or host-only presentation state.");
            }
            Assert.DoesNotContain("direct-link", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("direct clickable", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("absolute paths as link destinations", content, StringComparison.OrdinalIgnoreCase);
            if (relativeFile == "reference/mermaid-artifact-delivery.md")
            {
                Assert.Contains("The runtime-produced `mermaid_delivery.status` values are:", content, StringComparison.Ordinal);
                Assert.Contains("Host-only presentation states are not runtime evidence:", content, StringComparison.Ordinal);
                Assert.Contains("The host derives this continuity state", content, StringComparison.Ordinal);
                Assert.DoesNotContain("`mermaid_delivery.status` uses these values", content, StringComparison.Ordinal);
                Assert.Contains("`generation_status` is runtime evidence and reports `fresh` or `reused`", content, StringComparison.Ordinal);
                Assert.DoesNotContain("`generation_status` is independent and reports `fresh`, `reused`, or `not_emitted`", content, StringComparison.Ordinal);
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
                Path.Combine(skillRoot, "assets", "so-workflow", "reference", "so", "runtime-contracts.md")));
        File.WriteAllText(
            Path.Combine(packageGuideRoot, "so-guide-reference-governance.md"),
            ReadPackageGuideBody(
                Path.Combine(skillRoot, "assets", "so-workflow", "reference", "so", "runtime-governance.md")));
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
            payload.TryAdd("operation_id", $"test-{transitionId}");
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

        static string ReadPackageGuideBody(string targetPath)
        {
            const string endMarker = "<!-- loom-document-copy:end -->";
            var text = File.ReadAllText(targetPath).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var markerIndex = text.IndexOf(endMarker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                throw new InvalidOperationException($"Target-local package copy '{targetPath}' did not contain its provenance marker.");
            }

            var bodyStart = text.IndexOf("# ", markerIndex + endMarker.Length, StringComparison.Ordinal);
            if (bodyStart < 0)
            {
                throw new InvalidOperationException($"Target-local package copy '{targetPath}' did not contain a guide heading.");
            }

            return text[bodyStart..];
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
                       ["governance_entry_transport"] = "cli",
                       ["mcp_registration_attempt_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                       {
                           ["status"] = "unavailable",
                           ["mcp_attempted"] = false,
                           ["config_attempted"] = false,
                           ["fallback_reason"] = "mcp_transport_unavailable",
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
            Assert.Equal("WaitResume", payload.GetProperty("current_step_kind").GetString());
            Assert.Contains("mcp_startup_evidence", ReadRequiredInputs(payload));
         }

        using (var mcpBoundary = await ResumeAndReadEnvelopeAsync(
                   "transition.start_mcp",
                   new Dictionary<string, object?>(StringComparer.Ordinal)
                   {
                    ["mcp_startup_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["transport"] = "cli",
                        ["fallback_reason"] = "mcp_transport_unavailable",
                        ["runtime_version"] = "1.2.3",
                        ["launch_descriptor"] = "descriptor",
                        ["operation_id"] = "test-transition.start_mcp",
                        ["workflow_file"] = workflowPath,
                        ["workflow_sha256"] = "workflow-hash",
                        ["fragment_bounded"] = true,
                        ["result_sha256"] = "result-hash",
                    },
                   }))
                 {
                    var payload = mcpBoundary.RootElement.GetProperty("payload");
                    Assert.Equal("WaitResume", payload.GetProperty("current_step_kind").GetString());
                    var requiredInputs = ReadRequiredInputs(payload);
                    Assert.Contains("resolved_guide_surface_ref", requiredInputs);
                    Assert.Contains("resolved_guide_surface", requiredInputs);
                    Assert.DoesNotContain("mcp_startup_evidence", requiredInputs);
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
}
