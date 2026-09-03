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
    public void PredicatePreservesLegacyObjectNullChecks()
    {
        var result = _compiler.Compile(Binding(), new ExpressionDefinition
        {
            Source = "context.Get<object?>(\"payload\") != null",
        });

        Assert.True(result.IsSuccess, result.Feedback.Message);
        Assert.True(result.Execute!(new ExpressionRuntimeContext(new Dictionary<string, object?>
        {
            ["payload"] = new object(),
        })));
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

    [Fact]
    public void DeclaredStringMathAndParsingCapabilitiesCompileAndResolve()
    {
        var result = _compiler.Compile(
            Binding(
                RoslynCapabilityCatalog.ExpressionString,
                RoslynCapabilityCatalog.ExpressionMath,
                RoslynCapabilityCatalog.ExpressionParsing),
            new ExpressionDefinition
            {
                Source = "string.Equals(context.Get<string>(\"name\"), \"ADMIN\", StringComparison.OrdinalIgnoreCase) && Math.Clamp(context.Get<int>(\"score\"), 0, 100) >= 60 && int.TryParse(context.Get<string>(\"attempts\"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0",
            });

        Assert.True(result.IsSuccess, result.Feedback.Message);
        Assert.Contains(RoslynCapabilityCatalog.ExpressionString, result.Feedback.Capabilities);
        Assert.Contains(RoslynCapabilityCatalog.ExpressionMath, result.Feedback.Capabilities);
        Assert.Contains(RoslynCapabilityCatalog.ExpressionParsing, result.Feedback.Capabilities);
        Assert.True(result.Execute!(new ExpressionRuntimeContext(new Dictionary<string, object?>
        {
            ["name"] = "admin",
            ["score"] = 87,
            ["attempts"] = "2",
        })));
    }

    [Fact]
    public void RegexUsesNativeCSharpSyntaxAndFiniteTimeout()
    {
        var result = _compiler.Compile(
            Binding(RoslynCapabilityCatalog.ExpressionRegex, RoslynCapabilityCatalog.ExpressionTimeSpan),
            new ExpressionDefinition
            {
                Source = "Regex.Match(context.Get<string>(\"value\"), \"a+\", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Success",
            });

        Assert.True(result.IsSuccess, result.Feedback.Message);
        Assert.True(result.Execute!(new ExpressionRuntimeContext(new Dictionary<string, object?> { ["value"] = "aaaa" })));

        var replaced = _compiler.Compile(
            Binding(RoslynCapabilityCatalog.ExpressionRegex, RoslynCapabilityCatalog.ExpressionTimeSpan),
            new ExpressionDefinition
            {
                Source = "Regex.Replace(context.Get<string>(\"value\"), \"a\", \"b\", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) == \"bbbb\"",
            });
        Assert.True(replaced.IsSuccess, replaced.Feedback.Message);
        Assert.True(replaced.Execute!(new ExpressionRuntimeContext(new Dictionary<string, object?> { ["value"] = "aaaa" })));

        var noTimeout = _compiler.Compile(
            Binding(RoslynCapabilityCatalog.ExpressionRegex),
            new ExpressionDefinition { Source = "Regex.IsMatch(context.Get<string>(\"value\"), \"a+\")" });
        Assert.False(noTimeout.IsSuccess);
        Assert.Equal("LOOM.EXPR.SECURITY.REGEX_TIMEOUT_REQUIRED", noTimeout.Feedback.DiagnosticCode);

        var powershell = _compiler.Compile(
            Binding(RoslynCapabilityCatalog.ExpressionRegex),
            new ExpressionDefinition { Source = "[regex]::Match(context.Get<string>(\"value\"), \"a\") != null" });
        Assert.False(powershell.IsSuccess);
        Assert.Equal("LOOM.EXPR.CSHARP.POWERSHELL_SYNTAX", powershell.Feedback.DiagnosticCode);
        Assert.Contains("Regex.Match", powershell.Feedback.SuggestedFix, StringComparison.Ordinal);
    }

    [Fact]
    public void RegexRejectsDisallowedOptionsAndTimeoutSources()
    {
        var disallowedOptions = _compiler.Compile(
            Binding(RoslynCapabilityCatalog.ExpressionRegex, RoslynCapabilityCatalog.ExpressionTimeSpan),
            new ExpressionDefinition { Source = "Regex.IsMatch(context.Get<string>(\"value\"), \"a+\", RegexOptions.Compiled, TimeSpan.FromSeconds(1))" });
        Assert.False(disallowedOptions.IsSuccess);
        Assert.Equal("LOOM.EXPR.SECURITY.REGEX_OPTIONS_REQUIRED", disallowedOptions.Feedback.DiagnosticCode);

        var contextTimeout = _compiler.Compile(
            Binding(RoslynCapabilityCatalog.ExpressionRegex, RoslynCapabilityCatalog.ExpressionTimeSpan),
            new ExpressionDefinition { Source = "Regex.IsMatch(context.Get<string>(\"value\"), \"a+\", RegexOptions.None, TimeSpan.FromSeconds(context.Get<int>(\"timeout\")))" });
        Assert.False(contextTimeout.IsSuccess);
        Assert.Equal("LOOM.EXPR.SECURITY.TIMEOUT_REQUIRED", contextTimeout.Feedback.DiagnosticCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("5.001")]
    public void RegexRejectsTimeoutOutsidePositiveFiveSecondRange(string timeout)
    {
        var result = _compiler.Compile(
            Binding(RoslynCapabilityCatalog.ExpressionRegex, RoslynCapabilityCatalog.ExpressionTimeSpan),
            new ExpressionDefinition
            {
                Source = $"Regex.IsMatch(context.Get<string>(\"value\"), \"a+\", RegexOptions.None, TimeSpan.FromSeconds({timeout}))",
            });

        Assert.False(result.IsSuccess);
        Assert.Equal("LOOM.EXPR.SECURITY.TIMEOUT_RANGE", result.Feedback.DiagnosticCode);
    }

    [Fact]
    public void ParsingRejectsEnumValuesOutsideExplicitAllowlist()
    {
        var sources = new[]
        {
            "int.TryParse(\"1\", (NumberStyles)256, CultureInfo.InvariantCulture, out var parsed)",
            "DateTimeOffset.TryParseExact(\"2024-01-01\", \"yyyy-MM-dd\", CultureInfo.InvariantCulture, (DateTimeStyles)32, out var parsed)",
            "TimeSpan.TryParseExact(\"01:00:00\", \"c\", CultureInfo.InvariantCulture, (TimeSpanStyles)2, out var parsed)",
        };

        foreach (var source in sources)
        {
            var result = _compiler.Compile(
                Binding(RoslynCapabilityCatalog.ExpressionParsing),
                new ExpressionDefinition { Source = source });

            Assert.False(result.IsSuccess);
            Assert.Equal("LOOM.EXPR.SECURITY.INVARIANT_PARSE_REQUIRED", result.Feedback.DiagnosticCode);
        }
    }

    [Fact]
    public void BoundedCollectionPredicatesAcceptParenthesizedAuthorizedLambdas()
    {
        var result = _compiler.Compile(
            Binding(RoslynCapabilityCatalog.ExpressionCollections),
            new ExpressionDefinition
            {
                Source = "context.Get<IReadOnlyList<int>>(\"scores\").Any((score => score >= 3))",
            });

        Assert.True(result.IsSuccess, result.Feedback.Message);
        Assert.True(result.Execute!(new ExpressionRuntimeContext(new Dictionary<string, object?>
        {
            ["scores"] = new[] { 1, 2, 3 },
        })));
    }

    [Fact]
    public void BoundedCollectionMaterializationRejectsResourceOverflow()
    {
        var result = _compiler.Compile(
            Binding(RoslynCapabilityCatalog.ExpressionCollections),
            new ExpressionDefinition { Source = "context.Get<IReadOnlyList<int>>(\"scores\").Count > 0" });

        Assert.True(result.IsSuccess, result.Feedback.Message);
        var values = new Dictionary<string, object?> { ["scores"] = Enumerable.Range(0, 33).ToArray() };
        var exception = Assert.Throws<ExpressionResourceLimitException>(() => result.Execute!(new ExpressionRuntimeContext(values)));
        Assert.Equal("LOOM.EXPR.RESOURCE.COLLECTION_ITEMS", exception.DiagnosticCode);
    }

    [Fact]
    public void JsonCollectionMaterializationRejectsOverflowAndInvalidNull()
    {
        var result = _compiler.Compile(
            Binding(RoslynCapabilityCatalog.ExpressionCollections),
            new ExpressionDefinition { Source = "context.Get<IReadOnlyList<int>>(\"scores\").Count > 0" });

        Assert.True(result.IsSuccess, result.Feedback.Message);
        var oversized = JsonSerializer.SerializeToElement(Enumerable.Range(0, 33).ToArray());
        var itemException = Assert.Throws<ExpressionResourceLimitException>(() => result.Execute!(new ExpressionRuntimeContext(new Dictionary<string, object?>
        {
            ["scores"] = oversized,
        })));
        Assert.Equal("LOOM.EXPR.RESOURCE.COLLECTION_ITEMS", itemException.DiagnosticCode);

        var invalidNull = JsonSerializer.SerializeToElement(new int?[] { null });
        var shapeException = Assert.Throws<ExpressionResourceLimitException>(() => result.Execute!(new ExpressionRuntimeContext(new Dictionary<string, object?>
        {
            ["scores"] = invalidNull,
        })));
        Assert.Equal("LOOM.EXPR.RESOURCE.COLLECTION_SHAPE", shapeException.DiagnosticCode);
    }

    [Fact]
    public void BoundedCollectionMaterializationRejectsProjectedByteOverflow()
    {
        var result = _compiler.Compile(
            Binding(RoslynCapabilityCatalog.ExpressionCollections),
            new ExpressionDefinition { Source = "context.Get<IReadOnlyList<string>>(\"values\").Count > 0" });

        Assert.True(result.IsSuccess, result.Feedback.Message);
        var exception = Assert.Throws<ExpressionResourceLimitException>(() => result.Execute!(new ExpressionRuntimeContext(new Dictionary<string, object?>
        {
            ["values"] = new[] { new string('x', 20_000), new string('y', 20_000) },
        })));
        Assert.Equal("LOOM.EXPR.RESOURCE.COLLECTION_BYTES", exception.DiagnosticCode);
    }

    [Fact]
    public void ContextPathDepthIsBounded()
    {
        var result = _compiler.Compile(
            Binding(),
            new ExpressionDefinition { Source = "context.Has(\"a.b.c.d.e.f.g\")" });

        Assert.True(result.IsSuccess, result.Feedback.Message);
        var exception = Assert.Throws<ExpressionResourceLimitException>(() => result.Execute!(new ExpressionRuntimeContext(new Dictionary<string, object?>())));
        Assert.Equal("LOOM.EXPR.RESOURCE.CONTEXT_DEPTH", exception.DiagnosticCode);
    }

    [Fact]
    public void UnknownAndScriptOnlyCapabilitiesAreRejected()
    {
        var unknown = _compiler.Compile(
            Binding("loom.expression.unknown"),
            new ExpressionDefinition { Source = "true" });
        Assert.False(unknown.IsSuccess);
        Assert.Equal("LOOM.EXPR.CONTRACT.UNKNOWN_CAPABILITY", unknown.Feedback.DiagnosticCode);

        var scriptOnly = _compiler.Compile(
            Binding(RoslynCapabilityCatalog.ScriptJson),
            new ExpressionDefinition { Source = "true" });
        Assert.False(scriptOnly.IsSuccess);
        Assert.Equal("LOOM.EXPR.CONTRACT.SURFACE_MISMATCH", scriptOnly.Feedback.DiagnosticCode);
    }

    [Fact]
    public void ContextGenericTypesAreRestrictedToScalarsAndBoundedCollections()
    {
        var result = _compiler.Compile(
            Binding(),
            new ExpressionDefinition { Source = "context.Get<System.IO.FileInfo>(\"file\") == null" });

        Assert.False(result.IsSuccess);
        Assert.Equal("LOOM.EXPR.SECURITY.UNAPPROVED_TYPE", result.Feedback.DiagnosticCode);

        var unaryOperator = _compiler.Compile(
            Binding(),
            new ExpressionDefinition { Source = "context.Get<DateTime>(\"start\") > context.Get<DateTime>(\"end\")" });
        Assert.False(unaryOperator.IsSuccess);
        Assert.Equal("LOOM.EXPR.SECURITY.UNAPPROVED_API", unaryOperator.Feedback.DiagnosticCode);
    }

    private static ExpressionBinding Binding(params string[] capabilities) => new()
    {
        Language = ExpressionContract.CurrentLanguage,
        LanguageVersion = ExpressionContract.CurrentLanguageVersion,
        ContractId = ExpressionContract.CurrentContractId,
        ContractVersion = ExpressionContract.CurrentContractVersion,
        RequiredExpressionCapabilities = [.. capabilities],
        CompileFeedbackContract = ExpressionContract.DetailedCompileFeedbackContract,
    };
}
