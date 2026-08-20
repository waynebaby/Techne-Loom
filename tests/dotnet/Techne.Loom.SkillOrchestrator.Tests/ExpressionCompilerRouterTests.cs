using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class ExpressionCompilerRouterTests
{
    [Fact]
    public void UnsupportedLanguageFailsThroughStructuredRouterFeedback()
    {
        var binding = new ExpressionBinding
        {
            Language = "cel",
            LanguageVersion = "1.0",
            ContractId = "loom.expression.cel",
            ContractVersion = "1",
            CompileFeedbackContract = ExpressionContract.DetailedCompileFeedbackContract,
        };

        var result = new ExpressionCompilerRouter().Compile(binding, new ExpressionDefinition { Source = "true" }, "validation.gates.ready/passExpression");

        Assert.False(result.IsSuccess);
        Assert.Equal("LOOM.EXPR.ROUTER.UNSUPPORTED_LANGUAGE", result.Feedback.DiagnosticCode);
        Assert.Equal("contract", result.Feedback.DiagnosticCategory);
        Assert.Equal("validation.gates.ready/passExpression", result.Feedback.Field);
    }
}
