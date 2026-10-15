using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class ContextFieldProjectionValidatorTests
{
    [Fact]
    public void Validate_OutputBindingRequiresSameDeclaredType()
    {
        var catalog = CreateCatalog();
        var transition = CreateTransition(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["target"] = "$context:source",
            },
        });

        var diagnostic = Assert.Single(ContextFieldProjectionValidator.Validate(
            CreateInstance(transition),
            catalog,
            "test/contract.json"));

        Assert.Equal("LOOM.CONTEXT.PROJECTION.TYPE_MISMATCH", diagnostic.Code);
        Assert.Contains("source", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("target", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_LiteralUpdateMustMatchDestinationType()
    {
        var catalog = CreateCatalog();
        var transition = CreateTransition(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["source"] = "not-a-boolean",
            },
        });

        var diagnostic = Assert.Single(ContextFieldProjectionValidator.Validate(
            CreateInstance(transition),
            catalog,
            "test/contract.json"));

        Assert.Equal("LOOM.CONTEXT.PROJECTION.VALUE_MISMATCH", diagnostic.Code);
        Assert.Contains("source", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DynamicOutputPathRequiresDeclarationWithoutGuessingProducerType()
    {
        var catalog = CreateCatalog();
        var transition = CreateTransition(new Dictionary<string, object?>(StringComparer.Ordinal), outputPath: "dynamic_result");

        var diagnostic = Assert.Single(ContextFieldProjectionValidator.Validate(
            CreateInstance(transition),
            catalog,
            "test/contract.json"));

        Assert.Equal("LOOM.CONTEXT.PROJECTION.TARGET_UNDECLARED", diagnostic.Code);
        Assert.Contains("dynamic_result", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("Declare the destination context path", diagnostic.SuggestedFix, StringComparison.Ordinal);
    }


    private static ContextFieldCatalog CreateCatalog()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "name": "projection-test",
              "inputs": {},
              "outputs": {},
              "default_assumptions": {},
              "context_fields": {
                "schemaVersion": "loom.context-fields.v1",
                "fields": [
                  {"path":"source","type":"boolean","nullable":false,"owner":"caller","description":"Source value."},
                  {"path":"target","type":"string","nullable":false,"owner":"runtime","description":"Projected value."}
                ]
              }
            }
            """);
        return ContextFieldCatalog.Parse(document.RootElement, "test/contract.json");
    }

    private static WorkflowInstance CreateInstance(CommandTransition transition)
        => new()
        {
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [transition.Id] = transition,
            },
        };

    private static CommandTransition CreateTransition(Dictionary<string, object?> parameters, string? outputPath = null)
        => new()
        {
            Id = "transition.test",
            OutputPath = outputPath,
            Command = new CommandInvocation
            {
                Name = "test",
                Kind = CommandInvocationKind.NativeCode,
                Parameters = parameters,
            },
        };
    [Fact]
    public void Validate_UnknownOutputBindingMarkerIsRejectedForStringTargets()
    {
        var catalog = CreateCatalog();
        var transition = CreateTransition(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["target"] = "$reslt",
            },
        });

        var diagnostic = Assert.Single(ContextFieldProjectionValidator.Validate(
            CreateInstance(transition),
            catalog,
            "test/contract.json"));

        Assert.Equal("LOOM.CONTEXT.PROJECTION.SOURCE_UNSUPPORTED", diagnostic.Code);
        Assert.Contains("unsupported output binding expression", diagnostic.Message, StringComparison.Ordinal);
    }
}
