using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class ExpressionContractJsonTests
{
    [Fact]
    public void StringShorthand_ReadsAndAlwaysWritesStructuredDefinition()
    {
        const string json = "{\"guardExpression\":\"approved == true\"}";
        var options = WorkflowJsonSerializer.CreateDefaultOptions(indented: false);

        var value = JsonSerializer.Deserialize<Dictionary<string, ExpressionDefinition>>(json, options);
        var serialized = JsonSerializer.Serialize(value, options);

        Assert.NotNull(value);
        Assert.Equal("approved == true", value!["guardExpression"].Source);
        Assert.Contains("\"kind\":\"predicate\"", serialized, StringComparison.Ordinal);
        Assert.Contains("\"source\":\"approved == true\"", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"guardExpression\":\"approved == true\"", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Clone_CopiesRootBindingAndExpressionDefinitions()
    {
        var source = new WorkflowInstance
        {
            RuntimeBinding = "dotnet-so",
            ExpressionBinding = new ExpressionBinding
            {
                Language = "csharp",
                LanguageVersion = "12",
                ContractId = "loom.expression.csharp",
                ContractVersion = "1",
                RequiredExpressionCapabilities = ["context.read"],
                CompileFeedbackContract = "detailedCompileFeedbackV1",
            },
            Validation = new WorkflowValidationContract
            {
                Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
                {
                    ["gate"] = new() { PassExpression = new ExpressionDefinition { Source = "gate_outputs_present" } },
                },
            },
        };

        var clone = WorkflowInstanceCloner.Clone(source);

        Assert.Equal(source.RuntimeBinding, clone.RuntimeBinding);
        Assert.Equal(source.ExpressionBinding.Language, clone.ExpressionBinding.Language);
        Assert.Equal(source.ExpressionBinding.RequiredExpressionCapabilities, clone.ExpressionBinding.RequiredExpressionCapabilities);
        Assert.NotSame(source.ExpressionBinding, clone.ExpressionBinding);
        Assert.NotSame(source.ExpressionBinding.RequiredExpressionCapabilities, clone.ExpressionBinding.RequiredExpressionCapabilities);
        Assert.NotNull(clone.Validation);
        Assert.NotSame(source.Validation!.Gates["gate"].PassExpression, clone.Validation!.Gates["gate"].PassExpression);
        Assert.Equal("gate_outputs_present", clone.Validation.Gates["gate"].PassExpression!.Source);
    }
}
