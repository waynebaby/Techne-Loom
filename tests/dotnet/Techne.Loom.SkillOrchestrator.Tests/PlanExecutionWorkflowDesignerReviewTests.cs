using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class PlanExecutionWorkflowDesignerReviewTests
{
    [Theory]
    [InlineData("needs_changes", 1L, 1L, true)]
    [InlineData("needs_changes", 0L, 1L, false)]
    [InlineData("passed", 0L, 1L, true)]
    [InlineData("passed", 1L, 1L, false)]
    [InlineData("unknown", 1L, 1L, false)]
    [InlineData("needs_changes", 1L, 0L, false)]
    public void DesignerReviewGate_RequiresConsistentVerdictAndEvidence(string verdict, long findings, long evidence, bool expected)
    {
        var workflow = LoadWorkflow();
        var transition = Assert.IsType<CommandTransition>(workflow.Nodes["transition.review_ao_workflow_designer"]);
        var evaluator = new CSharpExpressionEvaluator(workflow.ExpressionBinding);
        var context = DesignerReviewContext();
        var review = (Dictionary<string, object?>)context["designerReview"]!;
        review["verdict"] = verdict;
        review["findingCount"] = findings;
        review["evidenceCount"] = evidence;
        Assert.Equal(expected, evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
    }

    [Fact]
    public async Task DesignerReviewSummary_PreservesAgentOwnershipAndRejectsBadTypes()
    {
        var workflow = LoadWorkflow();
        var skill = Path.Combine(FindRepositoryRoot(), ".agents", "skills", "loom-plan-execution");
        workflow.Context["target_skill_path"] = skill;
        var catalog = (await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(workflow)).Catalog;
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(skill, "assets", "so-workflow", "designer-review-summary.schema.json")));
        Assert.Equal("urn:techne-loom:plan-designer-review-summary:v2", schema.RootElement.GetProperty("$id").GetString());
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
        using var reportSchema = JsonDocument.Parse(File.ReadAllText(Path.Combine(skill, "assets", "so-workflow", "designer-review-report.schema.json")));
        Assert.Equal("urn:techne-loom:plan-designer-review-report:v1", reportSchema.RootElement.GetProperty("$id").GetString());
        var transition = Assert.IsType<CommandTransition>(workflow.Nodes["transition.review_ao_workflow_designer"]);
        Assert.Equal("designerReview", transition.OutputPath);
        var parameters = Assert.IsAssignableFrom<IDictionary<string, object?>>(transition.Command.Parameters);
        Assert.Equal("designerReview", Convert.ToString(parameters["resumeOutputKey"]));
        Assert.False(parameters.ContainsKey("outputBindings"));
        Assert.Contains("leave this seam blocked", Convert.ToString(parameters["skillHint"]));
        foreach (var path in new[]
        {
            "designerReview",
            "designerReview.schemaVersion",
            "designerReview.review_kind",
            "designerReview.reviewPath",
            "designerReview.verdict",
            "designerReview.findingCount",
            "designerReview.evidenceCount",
            "designerReview.producerName",
            "designerReview.instanceId",
            "designerReview.caseId",
            "designerReview.runId",
            "designerReview.waitId",
            "designerReview.targetSkillRoot",
            "designerReview.transitionId", "designerReview.checked_in_workflow_designer_asset"
        })
        {
            Assert.True(catalog.TryGetField(path, out var field));
            Assert.Equal("agent", field.Owner);
            Assert.False(field.Nullable);
        }

        Assert.True(catalog.TryGetField("designerReview.correlationKey", out var correlation));
        Assert.Equal("agent", correlation.Owner);
        Assert.True(correlation.Nullable);

        foreach (var property in schema.RootElement.GetProperty("properties").EnumerateObject())
        {
            Assert.True(catalog.TryGetField("designerReview." + property.Name, out var field));
            var expectedType = property.Value.TryGetProperty("type", out var declared) && declared.ValueKind == JsonValueKind.String ? declared.GetString() : "string";
            Assert.Equal(expectedType, field.Type);
            Assert.Equal(property.Name == "correlationKey", field.Nullable);
            if (property.Value.TryGetProperty("const", out var constant))
            {
                Assert.Equal(new[] { constant.GetString() }, field.AllowedValues.Select(value => value.GetString()));
            }
            if (property.Value.TryGetProperty("enum", out var allowed))
            {
                Assert.Equal(allowed.EnumerateArray().Select(value => value.GetString()), field.AllowedValues.Select(value => value.GetString()));
            }
        }

        var context = DesignerReviewContext();
        var payload = new Dictionary<string, object?> { ["designerReview"] = context["designerReview"] };
        var admission = ContextFieldRuntimeValidator.ValidateResumeProjection(workflow, catalog, "transition.review_ao_workflow_designer", payload, "designer-review-test");
        Assert.Same(context["designerReview"], admission.Payload!["designerReview"]);
        Assert.Throws<InvalidOperationException>(() => ContextFieldRuntimeValidator.ValidateResumeProjection(workflow, catalog, "transition.review_ao_workflow_designer", new Dictionary<string, object?>(), "designer-review-test"));
        var forged = new Dictionary<string, object?>(payload) { ["runtime_preflight_result"] = "forged" };
        Assert.Throws<InvalidOperationException>(() => ContextFieldRuntimeValidator.ValidateResumeProjection(workflow, catalog, "transition.review_ao_workflow_designer", forged, "designer-review-test"));
        catalog.ValidateContextValues(context, "designer-review-test");
        var review = (Dictionary<string, object?>)context["designerReview"]!;
        review["correlationKey"] = null;
        catalog.ValidateContextValues(context, "designer-review-test");
        review["correlationKey"] = false;
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "designer-review-test"));
        review.Remove("correlationKey");
        review["undeclared"] = "unexpected";
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "designer-review-test"));
        review.Remove("undeclared");
        review["evidenceCount"] = null;
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "designer-review-test"));
        review["evidenceCount"] = false;
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "designer-review-test"));
        review["evidenceCount"] = 1L; review["checked_in_workflow_designer_asset"] = false; Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "designer-review-test")); review["checked_in_workflow_designer_asset"] = "assets/agents/loom-plan-execution-workflow-designer.agent.md"; catalog.ValidateContextValues(context, "designer-review-test");
        review["verdict"] = "invented";
        Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(context, "designer-review-test"));
    }

    [Fact]
    public void DesignerReviewGate_RejectsMissingAndWhitespaceReportPath()
    {
        var workflow = LoadWorkflow();
        var transition = Assert.IsType<CommandTransition>(workflow.Nodes["transition.review_ao_workflow_designer"]);
        var evaluator = new CSharpExpressionEvaluator(workflow.ExpressionBinding);
        var context = DesignerReviewContext();
        var review = (Dictionary<string, object?>)context["designerReview"]!;
        review.Remove("reviewPath");
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        review["reviewPath"] = "  \t";
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        review["reviewPath"] = "review.json";
        review["targetSkillRoot"] = "different-root";
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        review["targetSkillRoot"] = "skill-root";
        review["waitId"] = " ";
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        review["waitId"] = "wait-1";
        review["transitionId"] = "other";
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        review["transitionId"] = "transition.review_ao_workflow_designer";
        review["producerName"] = "different reviewer";
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
        context.Remove("designerReview");
        Assert.False(evaluator.EvaluateBoolean(transition.PassExpression.Source, context));
    }

    private static Dictionary<string, object?> DesignerReviewContext() => new(StringComparer.Ordinal)
    {
        ["target_skill_path"] = "skill-root",
        ["designerReview"] = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "loom.plan.designer-review-summary.v2",
            ["review_kind"] = "plan-workflow-designer",
            ["reviewPath"] = "review.json",
            ["producerName"] = "loom-plan-execution Workflow Designer Contract Reviewer",
            ["instanceId"] = "instance-1",
            ["caseId"] = "case-1",
            ["runId"] = "run-1",
            ["transitionId"] = "transition.review_ao_workflow_designer", ["checked_in_workflow_designer_asset"] = "assets/agents/loom-plan-execution-workflow-designer.agent.md",
            ["waitId"] = "wait-1",
            ["targetSkillRoot"] = "skill-root",
            ["verdict"] = "needs_changes",
            ["findingCount"] = 1L,
            ["evidenceCount"] = 1L
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
