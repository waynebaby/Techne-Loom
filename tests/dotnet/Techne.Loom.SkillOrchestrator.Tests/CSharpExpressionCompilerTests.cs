using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using System.Text.Json;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class CSharpExpressionCompilerTests
{
    private readonly CSharpExpressionCompiler _compiler = new();

    [Fact]
    public void Predicate_CompilesAndExecutesAgainstReadOnlyContext()
    {
        var result = _compiler.Compile(Binding(), new ExpressionDefinition
        {
            Kind = "predicate",
            Source = "context.Get<int>(\"score\") >= 60",
        }, "transition.guardExpression");

        Assert.True(result.IsSuccess, result.Feedback.Message);
        Assert.Equal("succeeded", result.Feedback.Status);
        Assert.Equal("LOOM.EXPR.OK", result.Feedback.DiagnosticCode);
        Assert.True(result.Execute!(new ExpressionRuntimeContext(new Dictionary<string, object?> { ["score"] = 87 })));
        Assert.False(result.Execute(new ExpressionRuntimeContext(new Dictionary<string, object?> { ["score"] = 42 })));
    }

    [Fact]
    public void Predicate_ResolvesNestedDictionaryAndJsonObjectPaths()
    {
        var result = _compiler.Compile(Binding(), new ExpressionDefinition
        {
            Source = "context.Get<string>(\"runResult.status\") == \"completed\" && context.Get<int>(\"details.attempts\") == 2",
        });
        var context = new Dictionary<string, object?>
        {
            ["runResult"] = JsonSerializer.SerializeToElement(new { status = "completed" }),
            ["details"] = new Dictionary<string, object?> { ["attempts"] = 2 },
        };

        Assert.True(result.IsSuccess, result.Feedback.Message);
        Assert.True(result.Execute!(new ExpressionRuntimeContext(context)));
    }

    [Theory]
    [InlineData("workspace_mirror", true, true)]
    [InlineData("runtime_path_only", true, false)]
    [InlineData("delivery_failed", false, false)]
    public void MermaidDeliveryGateExpressionRejectsFailedOrUnverifiedEvidence(string status, bool artifactGenerated, bool linkResolvable)
    {
        var result = _compiler.Compile(Binding(), new ExpressionDefinition
        {
            Source = "context.Has(\"mermaid_delivery\") && context.Get<bool>(\"mermaid_delivery.artifact_generated\") == true && (context.Get<string>(\"mermaid_delivery.status\") == \"workspace_mirror\" || context.Get<string>(\"mermaid_delivery.status\") == \"runtime_path_only\")",
        });
        var context = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["mermaid_delivery"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = status,
                ["artifact_generated"] = artifactGenerated,
                ["link_resolvable"] = linkResolvable,
            },
        };

        Assert.True(result.IsSuccess, result.Feedback.Message);
        Assert.Equal(status is "workspace_mirror" or "runtime_path_only" && artifactGenerated, result.Execute!(new ExpressionRuntimeContext(context)));
    }

    [Theory]
    [InlineData("async context.Get<bool>(\"ready\")", "LOOM.EXPR.SECURITY.SYNCHRONOUS_ONLY")]
    public void AsynchronousExpressionsAreRejected(string source, string diagnosticCode)
    {
        var result = _compiler.Compile(Binding(), new ExpressionDefinition { Source = source });

        Assert.False(result.IsSuccess);
        Assert.Equal(diagnosticCode, result.Feedback.DiagnosticCode);
        Assert.Equal("security", result.Feedback.DiagnosticCategory);
    }

    [Fact]
    public void UnsupportedKindProducesStructuredContractFeedback()
    {
        var result = _compiler.Compile(Binding(), new ExpressionDefinition { Kind = "method", Source = "true" }, "transition.guardExpression");

        Assert.False(result.IsSuccess);
        Assert.Equal("LOOM.EXPR.CONTRACT.UNSUPPORTED_KIND", result.Feedback.DiagnosticCode);
        Assert.Equal("contract", result.Feedback.DiagnosticCategory);
        Assert.Equal("transition.guardExpression", result.Feedback.Field);
    }

    [Theory]
    [InlineData("System.Environment.Exit(0) == 0")]
    [InlineData("new object() != null")]
    [InlineData("typeof(string) != null")]
    public void UnapprovedApisAreRejected(string source)
    {
        var result = _compiler.Compile(Binding(), new ExpressionDefinition { Source = source });

        Assert.False(result.IsSuccess);
        Assert.Equal("LOOM.EXPR.SECURITY.UNAPPROVED_API", result.Feedback.DiagnosticCode);
        Assert.Equal("security", result.Feedback.DiagnosticCategory);
    }

    [Fact]
    public void InvalidCSharpProducesSourceSpanAndStableDiagnostic()
    {
        var result = _compiler.Compile(Binding(), new ExpressionDefinition { Source = "context.Get<int>(\"score\") >=" });

        Assert.False(result.IsSuccess);
        Assert.StartsWith("LOOM.EXPR.CSHARP.", result.Feedback.DiagnosticCode, StringComparison.Ordinal);
        Assert.NotNull(result.Feedback.SourceSpan);
        Assert.Equal("semantic", result.Feedback.DiagnosticCategory);
        Assert.False(string.IsNullOrWhiteSpace(result.Feedback.Message));
    }

    private static ExpressionBinding Binding() => new()
    {
        Language = ExpressionContract.CurrentLanguage,
        LanguageVersion = ExpressionContract.CurrentLanguageVersion,
        ContractId = ExpressionContract.CurrentContractId,
        ContractVersion = ExpressionContract.CurrentContractVersion,
        CompileFeedbackContract = ExpressionContract.DetailedCompileFeedbackContract,
    };
}
