using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class PlanExecutionDesignerEvidenceContractTests
{
    [Theory]
    [InlineData("passed", "passed", "passed", true)]
    [InlineData("failed", "passed", "passed", false)]
    [InlineData("unknown", "passed", "passed", false)]
    [InlineData("passed", "failed", "passed", false)]
    [InlineData("passed", "unknown", "passed", false)]
    [InlineData("passed", "passed", "failed", false)]
    [InlineData("passed", "passed", "unknown", false)]
    public void RefreshGate_RequiresAllDesignerVerdicts(string refreshVerdict, string reviewVerdict, string phaseVerdict, bool expected)
    {
        var workflow = LoadWorkflow();
        var transition = Assert.IsType<CommandTransition>(workflow.Nodes["transition.refresh_governed_template"]);
        var evaluator = new CSharpExpressionEvaluator(workflow.ExpressionBinding);
        var context = PassingContext();
        ((Dictionary<string, object?>)context["templateRefresh"]!)["verdict"] = refreshVerdict;
        ((Dictionary<string, object?>)context["static_contract_review"]!)["verdict"] = reviewVerdict;
        ((Dictionary<string, object?>)context["phase_design_evidence"]!)["verdict"] = phaseVerdict;
        Assert.Equal(expected, evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
    }

    [Fact]
    public void RefreshGate_RejectsMissingNullEmptyAndMismatchedEvidence()
    {
        var workflow = LoadWorkflow();
        var transition = Assert.IsType<CommandTransition>(workflow.Nodes["transition.refresh_governed_template"]);
        var evaluator = new CSharpExpressionEvaluator(workflow.ExpressionBinding);
        var context = PassingContext();
        context.Remove("phase_design_evidence");
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        context["phase_design_evidence"] = null;
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        context["phase_design_evidence"] = new Dictionary<string, object?>();
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        context = PassingContext();
        context.Remove("static_contract_review");
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        context["static_contract_review"] = null;
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        context["static_contract_review"] = new Dictionary<string, object?>();
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        context = PassingContext();
        context.Remove("templateRefresh");
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        context["templateRefresh"] = new Dictionary<string, object?>();
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        context = PassingContext();
        ((Dictionary<string, object?>)context["templateRefresh"]!)["candidate_path"] = "  \t";
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        context = PassingContext();
        ((Dictionary<string, object?>)context["templateRefresh"]!)["static_review_path"] = "different.json";
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        context = PassingContext();
        ((Dictionary<string, object?>)context["phase_design_evidence"]!)["record_path"] = "different.json";
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
    }

    [Fact]
    public async Task Catalog_RejectsWrongTypesAndPreservesAgentOwnership()
    {
        var workflow = LoadWorkflow();
        workflow.Context["target_skill_path"] = Path.Combine(FindRepositoryRoot(), ".agents", "skills", "loom-plan-execution");
        var catalog = (await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(workflow)).Catalog; Assert.False(catalog.TryGetField("completion_manifest_reference", out _)); Assert.False(new CSharpExpressionEvaluator(workflow.ExpressionBinding).EvaluateBoolean(workflow.Validation!.Gates["gate.official_done"].PassExpression!.Source, new Dictionary<string, object?> { ["checked_in_skill_markdown_asset"] = "skill.md", ["checked_in_workflow_designer_asset"] = "designer.agent.md", ["workflow_runtime_copy_json"] = "workflow.json", ["event_log_file"] = "events.jsonl" }));
        var paths = new[]
        {
            "templateRefresh",
            "templateRefresh.schemaVersion",
            "static_contract_review",
            "static_contract_review.verdict",
            "phase_design_evidence",
            "phase_design_evidence.verdict"
        };
        foreach (var path in paths)
        {
            Assert.True(catalog.TryGetField(path, out var field));
            Assert.Equal("agent", field.Owner);
        }

        var context = PassingContext();
        catalog.ValidateContextValues(context, "designer-test");
        context["phase_design_evidence"] = null;
        catalog.ValidateContextValues(context, "designer-test");
        context["phase_design_evidence"] = false;
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "designer-test"));
        context = PassingContext();
        context["templateRefresh"] = null;
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "designer-test"));
        context = PassingContext();
        context["templateRefresh"] = false;
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "designer-test"));
        context = PassingContext();
        ((Dictionary<string, object?>)context["templateRefresh"]!)["producerName"] = "AO Workflow Designer";
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "designer-test"));
    }

    [Fact]
    public void RefreshSchema_AndHandoffDeclareExactDesigner()
    {
        var skill = Path.Combine(FindRepositoryRoot(), ".agents", "skills", "loom-plan-execution");
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(skill, "assets", "so-workflow", "template-refresh.schema.json")));
        var root = schema.RootElement;
        Assert.Equal("urn:techne-loom:plan-so-template-refresh:v1", root.GetProperty("$id").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        var required = root.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Equal(new[] { "schemaVersion", "producerName", "candidate_path", "static_review_path", "verdict" }, required);
        Assert.Equal("loom.plan.so-template-refresh.v1", root.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetString());
        Assert.Equal("loom-skill-enhancement Workflow Designer", root.GetProperty("properties").GetProperty("producerName").GetProperty("const").GetString());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(skill, "assets", "so-workflow", "so-template.json")));
        var parameters = template.RootElement.GetProperty("nodes").GetProperty("transition.refresh_governed_template").GetProperty("command").GetProperty("parameters");
        Assert.Contains("loom-skill-enhancement Workflow Designer", parameters.GetProperty("skillHint").GetString());
        Assert.Contains("template-refresh.schema.json", parameters.GetProperty("skillHint").GetString());
        Assert.StartsWith("loom skill inspect-contract-fragment", parameters.GetProperty("contextFieldTypePolicy").GetProperty("catalogReadCommand").GetString());
    }

    [Fact]
    public async Task RecoverySuggestions_MatchVersionedAdvisorTypesAndRemainAgentOwned()
    {
        var workflow = LoadWorkflow();
        var skill = Path.Combine(FindRepositoryRoot(), ".agents", "skills", "loom-plan-execution");
        workflow.Context["target_skill_path"] = skill;
        var catalog = (await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(workflow)).Catalog;
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(skill, "assets", "so-workflow", "recovery-proposal.schema.json")));
        var properties = schema.RootElement.GetProperty("properties"); Assert.Equal("urn:techne-loom:plan-so-recovery-proposal:v2", schema.RootElement.GetProperty("$id").GetString()); Assert.Equal("loom.plan.so-recovery-proposal.v2", properties.GetProperty("schemaVersion").GetProperty("const").GetString()); Assert.Equal("array", properties.GetProperty("attempt_history").GetProperty("type").GetString()); Assert.Equal("object", properties.GetProperty("blocker_report").GetProperty("type").GetString()); Assert.Equal("array", properties.GetProperty("evidence_references").GetProperty("type").GetString()); Assert.Equal("array", properties.GetProperty("replan_evidence_references").GetProperty("type").GetString()); Assert.True(properties.GetProperty("rollback_plan").GetProperty("oneOf").EnumerateArray().Any(branch => branch.GetProperty("type").GetString() == "null"));
        foreach (var path in new[]
        {
            "replan_strategy",
            "replan_anchor",
            "candidate_terminal_path"
        })
        {
            Assert.Equal("string", properties.GetProperty(path).GetProperty("type").GetString());
            Assert.True(catalog.TryGetField(path, out var field));
            Assert.Equal("string", field.Type);
            Assert.Equal("agent", field.Owner);
            Assert.False(field.Nullable);
        }

        foreach (var (path, type, nullable) in new[] { ("attempt_history", "array", false), ("blocker_report", "object", false), ("evidence_references", "array", false), ("replan_evidence_references", "array", false), ("rollback_plan", "object", true), ("rollback_plan.reason", "string", false), ("rollback_plan.status", "string", true), ("rollback_plan.steps", "array", true), ("rollback_plan.evidence_references", "array", true) }) { Assert.True(catalog.TryGetField(path, out var producerField)); Assert.Equal(type, producerField.Type); Assert.Equal("agent", producerField.Owner); Assert.Equal(nullable, producerField.Nullable); } Assert.True(catalog.TryGetField("replan_strategy", out var strategy));
        Assert.Equal(properties.GetProperty("replan_strategy").GetProperty("enum").EnumerateArray().Select(x => x.GetString()), strategy.AllowedValues.Select(x => x.GetString()));
        var citation = new Dictionary<string, object?> { ["path"] = "workflow.json", ["start_line"] = 1L, ["end_line"] = 1L, ["quote"] = "The same workflow instance is waiting for resume.", ["role"] = "runtime", ["root"] = "execution" }; var context = new Dictionary<string, object?>
        {
            ["replan_strategy"] = "continue_from_current",
            ["replan_anchor"] = "state.review_workflow_designer",
            ["candidate_terminal_path"] = "state.review_workflow_designer -> state.done", ["attempt_history"] = Array.Empty<object>(), ["blocker_report"] = new Dictionary<string, object?> { ["status"] = "awaiting_resume", ["summary"] = "The same instance is waiting for resume.", ["evidence_references"] = new object[] { citation } }, ["evidence_references"] = new object[] { citation }, ["replan_evidence_references"] = new object[] { citation }, ["rollback_plan"] = null
        };
        catalog.ValidateContextValues(context, "advisor-test"); context["rollback_plan"] = new Dictionary<string, object?> { ["status"] = "validated", ["reason"] = "The prior state and corrective action are cited.", ["steps"] = new object[] { new Dictionary<string, object?> { ["action"] = "Restore the recorded prior state.", ["evidence_references"] = new object[] { citation } } }, ["evidence_references"] = new object[] { citation } }; catalog.ValidateContextValues(context, "advisor-test"); context["rollback_plan"] = null; foreach (var path in new[] { "attempt_history", "blocker_report", "evidence_references", "replan_evidence_references" }) { var prior = context[path]; context[path] = null; Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "advisor-test")); context[path] = false; Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "advisor-test")); context[path] = prior; } context["rollback_plan"] = false; Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "advisor-test")); context["rollback_plan"] = null;
        context.Remove("replan_anchor");
        catalog.ValidateContextValues(context, "advisor-test");
        context["replan_anchor"] = null;
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "advisor-test"));
        context["replan_anchor"] = false;
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "advisor-test"));
        context["replan_anchor"] = "state.review_workflow_designer";
        context["replan_strategy"] = "unknown_strategy";
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "advisor-test"));
    }

    [Theory]
    [InlineData("needs_changes", 1L, 1L, true)]
    [InlineData("needs_changes", 0L, 1L, false)]
    [InlineData("passed", 0L, 1L, true)]
    [InlineData("passed", 1L, 1L, false)]
    [InlineData("unknown", 1L, 1L, false)]
    [InlineData("needs_changes", 1L, 0L, false)]
    public void SkillReviewGate_RequiresConsistentVerdictAndEvidence(string verdict, long findings, long evidence, bool expected)
    {
        var workflow = LoadWorkflow();
        var transition = Assert.IsType<CommandTransition>(workflow.Nodes["transition.review_ao_skill_wording"]);
        var evaluator = new CSharpExpressionEvaluator(workflow.ExpressionBinding);
        var context = SkillReviewContext();
        var review = (Dictionary<string, object?>)context["skillReview"]!;
        review["verdict"] = verdict;
        review["findingCount"] = findings;
        review["evidenceCount"] = evidence;
        Assert.Equal(expected, evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
    }

    [Fact]
    public async Task SkillReviewSummary_PreservesAgentOwnershipAndRejectsBadTypes()
    {
        var workflow = LoadWorkflow();
        var skill = Path.Combine(FindRepositoryRoot(), ".agents", "skills", "loom-plan-execution");
        workflow.Context["target_skill_path"] = skill;
        var catalog = (await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(workflow)).Catalog;
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(skill, "assets", "so-workflow", "skill-review-summary.schema.json")));
        Assert.Equal("urn:techne-loom:plan-skill-review-summary:v2", schema.RootElement.GetProperty("$id").GetString());
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean()); Assert.Equal("string", schema.RootElement.GetProperty("properties").GetProperty("checked_in_skill_markdown_asset").GetProperty("type").GetString()); Assert.True(schema.RootElement.GetProperty("required").EnumerateArray().Any(item => item.GetString() == "checked_in_skill_markdown_asset"));
        foreach (var path in new[]
        {
            "skillReview",
            "skillReview.schemaVersion",
            "skillReview.review_kind",
            "skillReview.reviewPath",
            "skillReview.verdict",
            "skillReview.findingCount",
            "skillReview.evidenceCount", "skillReview.checked_in_skill_markdown_asset"
        })
        {
            Assert.True(catalog.TryGetField(path, out var field));
            Assert.Equal("agent", field.Owner);
            Assert.False(field.Nullable);
        }

        Assert.True(catalog.TryGetField("skillReview.checked_in_skill_markdown_asset", out var assetField)); Assert.Equal("string", assetField.Type); var context = SkillReviewContext();
        catalog.ValidateContextValues(context, "skill-review-test");
        var review = (Dictionary<string, object?>)context["skillReview"]!; review["checked_in_skill_markdown_asset"] = false; Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "skill-review-test")); review["checked_in_skill_markdown_asset"] = "SKILL.md"; catalog.ValidateContextValues(context, "skill-review-test");
        review["evidenceCount"] = null;
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "skill-review-test"));
        review["evidenceCount"] = false;
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "skill-review-test"));
        review["evidenceCount"] = 1L;
        review["verdict"] = "invented";
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "skill-review-test"));
    }

    [Fact]
    public void SkillReviewGate_RejectsMissingAndWhitespaceReportPath()
    {
        var workflow = LoadWorkflow();
        var transition = Assert.IsType<CommandTransition>(workflow.Nodes["transition.review_ao_skill_wording"]);
        var evaluator = new CSharpExpressionEvaluator(workflow.ExpressionBinding);
        var context = SkillReviewContext();
        var review = (Dictionary<string, object?>)context["skillReview"]!;
        review.Remove("reviewPath");
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        review["reviewPath"] = "  \t";
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        context.Remove("skillReview");
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
    }

    [Fact]
    public async Task CompileFeedbackSummary_UsesRuntimeProducedStatusAndExplicitProjection()
    {
        var workflow = LoadWorkflow();
        var skill = Path.Combine(FindRepositoryRoot(), ".agents", "skills", "loom-plan-execution");
        workflow.Context["target_skill_path"] = skill;
        var transition = Assert.IsType<CommandTransition>(workflow.Nodes["transition.compile_template"]);
        Assert.Equal("workflow_compile_feedback", transition.OutputPath);
        var parameters = Assert.IsAssignableFrom<IDictionary<string, object?>>(transition.Command.Parameters);
        Assert.Equal("workflow_compile_feedback", Convert.ToString(parameters["resumeOutputKey"]));
        var inputs = Assert.IsAssignableFrom<IEnumerable<object?>>(parameters["requiredInputs"]).Select(Convert.ToString).ToArray();
        Assert.Contains("workflow_compile_feedback", inputs);
        Assert.DoesNotContain("compileAudit", inputs);
        var bindings = Assert.IsAssignableFrom<IDictionary<string, object?>>(parameters["outputBindings"]);
        Assert.False(bindings.ContainsKey("workflow_compile_feedback"));
        Assert.Equal("$payload:workflow_dataflow_json", Convert.ToString(bindings["workflow_dataflow_json"]));
        Assert.Contains("compile-feedback-summary.schema.json", Convert.ToString(parameters["skillHint"]));
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(skill, "assets", "so-workflow", "compile-feedback-summary.schema.json")));
        Assert.Equal("urn:techne-loom:plan-so-compile-feedback-summary:v1", schema.RootElement.GetProperty("$id").GetString());
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("string", schema.RootElement.GetProperty("properties").GetProperty("status").GetProperty("type").GetString());
        Assert.Equal(typeof(string), typeof(WorkflowCompileFeedback).GetProperty("Status")!.PropertyType);
        var catalog = (await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(workflow)).Catalog;
        Assert.True(catalog.TryGetField("workflow_compile_feedback", out var root));
        Assert.Equal("runtime", root.Owner);
        Assert.True(root.Nullable);
        Assert.True(catalog.TryGetField("workflow_compile_feedback.status", out var status));
        Assert.Equal("runtime", status.Owner);
        Assert.Equal("string", status.Type);
        Assert.False(status.Nullable);
        var feedback = new WorkflowCompileFeedback
        {
            Status = "succeeded"
        };
        var context = new Dictionary<string, object?>
        {
            ["workflow_compile_feedback"] = new Dictionary<string, object?>
            {
                ["status"] = feedback.Status
            }
        };
        catalog.ValidateContextValues(context, "compile-feedback-test");
        ((Dictionary<string, object?>)context["workflow_compile_feedback"]!)["status"] = false;
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "compile-feedback-test"));
        context["workflow_compile_feedback"] = JsonSerializer.SerializeToElement(feedback);
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "compile-feedback-test"));
        context["workflow_compile_feedback"] = null;
        catalog.ValidateContextValues(context, "compile-feedback-test");
        context["workflow_compile_feedback"] = false;
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "compile-feedback-test"));
    }

    [Fact]
    public void CompileFeedbackGate_RejectsMissingNullEmptyAndFailedEvidence()
    {
        var workflow = LoadWorkflow();
        var source = workflow.Validation!.Gates["gate.compile_review_ready"].PassExpression!.Source;
        var evaluator = new CSharpExpressionEvaluator(workflow.ExpressionBinding);
        var context = new Dictionary<string, object?>
        {
            ["workflow_mermaid_md"] = "diagram.md",
            ["workflow_html"] = "diagram.html",
            ["workflow_analysis_json"] = "analysis.json",
            ["workflow_dataflow_json"] = "dataflow.json"
        };
        Assert.False(evaluator.EvaluateBoolean(source, context));
        context["workflow_compile_feedback"] = null;
        Assert.False(evaluator.EvaluateBoolean(source, context));
        var summary = new Dictionary<string, object?>();
        context["workflow_compile_feedback"] = summary;
        Assert.False(evaluator.EvaluateBoolean(source, context));
        summary["status"] = "";
        Assert.False(evaluator.EvaluateBoolean(source, context));
        summary["status"] = "failed";
        Assert.False(evaluator.EvaluateBoolean(source, context));
        summary["status"] = "succeeded";
        Assert.True(evaluator.EvaluateBoolean(source, context));
        context.Remove("workflow_html");
        Assert.False(evaluator.EvaluateBoolean(source, context));
    }

    private static Dictionary<string, object?> SkillReviewContext() => new(StringComparer.Ordinal)
    {
        ["skillReview"] = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "loom.plan.skill-review-summary.v2",
            ["review_kind"] = "plan-skill-markdown",
            ["reviewPath"] = "review.json",
            ["verdict"] = "needs_changes",
            ["findingCount"] = 1L,
            ["evidenceCount"] = 1L, ["checked_in_skill_markdown_asset"] = "SKILL.md"
        }
    };

    [Fact]
    public async Task SoWaitAliases_AreTypedRuntimeOwnedFactsNotAgentSuggestions()
    {
        var workflow = LoadWorkflow();
        workflow.Context["target_skill_path"] = Path.Combine(FindRepositoryRoot(), ".agents", "skills", "loom-plan-execution");
        var catalog = (await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(workflow)).Catalog;
        Assert.Equal(typeof(string), typeof(Techne.Loom.SkillOrchestrator.Models.SkillBoundaryPayload).GetProperty("WorkflowFile")!.PropertyType);
        Assert.Equal(typeof(string), typeof(PendingWaitGroup).GetProperty("TransitionId")!.PropertyType);
        foreach (var path in new[]
        {
            "workflow_instance_file",
            "last_transition_id"
        })
        {
            Assert.True(catalog.TryGetField(path, out var field));
            Assert.Equal("string", field.Type);
            Assert.Equal("runtime", field.Owner);
            Assert.False(field.Nullable);
            var delta = new Dictionary<string, object?>
            {
                [path] = "forged"
            };
            Assert.Throws<InvalidOperationException>(() => ContextFieldRuntimeValidator.ValidateContextDelta(catalog, new Dictionary<string, object?>(), delta, "agent-delta"));
        }

        var context = new Dictionary<string, object?>
        {
            ["workflow_instance_file"] = "current.json",
            ["last_transition_id"] = "transition.wait"
        };
        catalog.ValidateContextValues(context, "so-wait-test");
        context["last_transition_id"] = false;
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "so-wait-test"));
    }

    private static Dictionary<string, object?> PassingContext() => new(StringComparer.Ordinal)
    {
        ["templateRefresh"] = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "loom.plan.so-template-refresh.v1",
            ["producerName"] = "loom-skill-enhancement Workflow Designer",
            ["candidate_path"] = "candidate.json",
            ["static_review_path"] = "review.json",
            ["verdict"] = "passed"
        },
        ["static_contract_review"] = new Dictionary<string, object?>
        {
            ["path"] = "review.json",
            ["schemaVersion"] = "workflow-designer.static-contract-review.v1",
            ["runtimeVersion"] = "0.4.15-beta",
            ["verdict"] = "passed"
        },
        ["phase_design_evidence"] = new Dictionary<string, object?>
        {
            ["mode"] = "wholeWorkflow",
            ["authored_state_node_count"] = 20,
            ["verdict"] = "passed",
            ["record_key"] = "phaseDesignEvidence",
            ["record_path"] = "review.json"
        }
    };
    private static WorkflowInstance LoadWorkflow() => WorkflowJsonSerializer.Deserialize(File.ReadAllText(Path.Combine(FindRepositoryRoot(), ".agents", "skills", "loom-plan-execution", "assets", "so-workflow", "so-template.json")));
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Techne.Loom.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Techne-Loom repository root.");
    }
}
