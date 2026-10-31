using System;
using System.IO;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class PlanExecutionScopeContractTests
{
    [Fact]
    public void ScopePredicate_RejectsMissingEmptyAndWhitespace()
    {
        var root = FindRepositoryRoot();
        var templatePath = Path.Combine(root, ".agents", "skills", "loom-plan-execution", "assets", "so-workflow", "so-template.json");
        var templateJson = File.ReadAllText(templatePath);
        var workflow = WorkflowJsonSerializer.Deserialize(templateJson);
        var scope = Assert.IsType<CommandTransition>(workflow.Nodes["transition.collect_governance_scope"]);
        var evaluator = new CSharpExpressionEvaluator(workflow.ExpressionBinding);
        var context = new Dictionary<string, object?>(StringComparer.Ordinal);
        Assert.False(evaluator.EvaluateBoolean(scope.SucceedExpression.Source, context));
        context["governanceScope"] = string.Empty;
        Assert.False(evaluator.EvaluateBoolean(scope.SucceedExpression.Source, context));
        context["governanceScope"] = " \t\r\n\u00a0";
        Assert.False(evaluator.EvaluateBoolean(scope.SucceedExpression.Source, context));
        context["governanceScope"] = " Example scope ";
        Assert.True(evaluator.EvaluateBoolean(scope.SucceedExpression.Source, context));
    }

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
