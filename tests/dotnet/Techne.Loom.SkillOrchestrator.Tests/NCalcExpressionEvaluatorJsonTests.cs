using System.Text.Json;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class NCalcExpressionEvaluatorJsonTests
{
    private readonly NCalcExpressionEvaluator _evaluator = new();

    [Fact]
    public void JsonContext_EvaluatesNestedStatusAndNumericThresholds()
    {
        var context = Context("""
        {
          "runResult": { "status": "completed", "attempts": 2 },
          "score": 87
        }
        """);

        Assert.True(_evaluator.EvaluateBoolean("runResult.status == 'completed' && score >= 80", context));
        Assert.True(_evaluator.EvaluateBoolean("[runResult.status] == 'completed'", context));
        Assert.False(_evaluator.EvaluateBoolean("runResult.attempts > 2", context));
    }

    [Fact]
    public void JsonContext_EvaluatesBooleanStringNumberAndNullValues()
    {
        var context = Context("""
        {
          "approved": true,
          "reviewer": "alice",
          "count": 4,
          "optional": null,
          "disabledText": "false"
        }
        """);

        Assert.True(_evaluator.EvaluateBoolean("approved", context));
        Assert.True(_evaluator.EvaluateBoolean("reviewer == 'alice'", context));
        Assert.True(_evaluator.EvaluateBoolean("count == 4 && count != 0", context));
        Assert.True(_evaluator.EvaluateBoolean("optional == null", context));
        Assert.False(_evaluator.EvaluateBoolean("optional != null", context));
        Assert.False(_evaluator.EvaluateBoolean("disabledText", context));
    }

    [Fact]
    public void JsonContext_EvaluatesLogicalPrecedenceAndParentheses()
    {
        var context = Context("""
        {
          "status": "queued",
          "approved": false,
          "retryable": true
        }
        """);

        Assert.True(_evaluator.EvaluateBoolean("status == 'queued' || approved && retryable", context));
        Assert.False(_evaluator.EvaluateBoolean("(status == 'running' || approved) && retryable", context));
        Assert.True(_evaluator.EvaluateBoolean("!(status == 'failed') && retryable", context));
    }

    [Fact]
    public void JsonContext_ConvertsArraysAndObjectsForBooleanEvaluation()
    {
        var context = Context("""
        {
          "items": ["one", "two"],
          "emptyItems": [],
          "evidence": {},
          "outputs": { "published": true }
        }
        """);

        Assert.True(_evaluator.EvaluateBoolean("items", context));
        Assert.False(_evaluator.EvaluateBoolean("emptyItems", context));
        Assert.False(_evaluator.EvaluateBoolean("evidence", context));
        Assert.True(_evaluator.EvaluateBoolean("outputs.published", context));
    }

    [Fact]
    public void JsonContext_SupportsNegativeFloatingAndDecimalComparisons()
    {
        var context = Context("""
        {
          "temperature": -3.5,
          "ratio": 0.75,
          "limit": 1.25
        }
        """);

        Assert.True(_evaluator.EvaluateBoolean("temperature < 0", context));
        Assert.True(_evaluator.EvaluateBoolean("ratio >= 0.75", context));
        Assert.False(_evaluator.EvaluateBoolean("limit <= 1", context));
    }

    [Fact]
    public void JsonContext_MissingParametersAreNullAndDoNotCrashEvaluation()
    {
        var context = Context("""
        {
          "actual": "expected"
        }
        """);

        Assert.True(_evaluator.EvaluateBoolean("actual != missingPath", context));
        Assert.False(_evaluator.EvaluateBoolean("missingPath", context));
        Assert.True(_evaluator.EvaluateBoolean("missingPath == null", context));
        Assert.False(_evaluator.EvaluateBoolean("missingPath != null", context));
    }

    [Theory]
    [InlineData("")]
    [InlineData("status ==")]
    [InlineData("status === 'ready'")]
    [InlineData("status is not None")]
    [InlineData("ctx.get('status') == 'ready'")]
    public void InvalidNCalcExpressions_AreRejectedWithActionableCompileDiagnostics(string expression)
    {
        Assert.False(NCalcExpressionEvaluator.TryValidate(expression, out var diagnostic));
        Assert.False(string.IsNullOrWhiteSpace(diagnostic));
        Assert.Contains("NCalc", diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidNCalcExpression_RuntimeErrorIncludesExpressionAndContextPaths()
    {
        var context = Context("""
        {
          "runResult": { "status": "completed" },
          "score": 87
        }
        """);

        var error = Assert.Throws<NCalcExpressionException>(() =>
            _evaluator.EvaluateBoolean("runResult.status ==", context));

        Assert.Equal("runResult.status ==", error.Expression);
        Assert.Equal("evaluation", error.Phase);
        Assert.Contains("runResult.status", error.AvailableContextPaths);
        Assert.Contains("score", error.AvailableContextPaths);
        Assert.Contains("NCalc", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JsonContext_ExpressionValidationAcceptsDocumentedNCalcSyntax()
    {
        var expressions = new[]
        {
            "true",
            "approved && score >= 60",
            "(status == 'ok' || status == 'retry') && attempt < 3",
            "[runResult.status] != null",
            "!failed",
        };

        foreach (var expression in expressions)
        {
            Assert.True(NCalcExpressionEvaluator.TryValidate(expression, out var diagnostic), $"{expression}: {diagnostic}");
        }
    }

    private static IReadOnlyDictionary<string, object?> Context(string json)
        => JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
            ?? throw new InvalidOperationException("Test JSON did not deserialize to a context dictionary.");
}
