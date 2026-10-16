using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class ContextFieldExpressionTypeAnalyzerTests
{
    private readonly CSharpExpressionCompiler _compiler = new();

    [Theory]
    [InlineData("context.Get<string>(\"request.name\") != null")]
    [InlineData("context.Get<bool>(\"request.approved\")")]
    [InlineData("context.Get<long>(\"request.count\") >= 0")]
    [InlineData("context.Get<IReadOnlyList<string>>(\"request.labels\") != null")]
    public void MatchingConsumerTypesCompile(string source)
    {
        var result = _compiler.CompileWithContextFieldCatalog(Binding(), Predicate(source), Catalog());

        Assert.True(result.IsSuccess, result.Feedback.Message);
    }

    [Fact]
    public void DeclaredContextPathBeyondRuntimeDepthLimitFailsCompile()
    {
        using var document = JsonDocument.Parse("""
            {"name":"deep-context-test","inputs":{},"outputs":{},"default_assumptions":{},"context_fields":{"schemaVersion":"loom.context-fields.v1","fields":[{"path":"request.one.two.three.four.five.six","type":"string","nullable":false,"owner":"agent","description":"Too-deep expression path."}]}}
            """);
        var catalog = ContextFieldCatalog.Parse(document.RootElement, "contract.json");
        var result = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Get<string>(\"request.one.two.three.four.five.six\") != null"),
            catalog);

        Assert.False(result.IsSuccess);
        Assert.Equal("LOOM.EXPR.RESOURCE.CONTEXT_DEPTH", result.Feedback.DiagnosticCode);
        Assert.Contains("6-segment expression limit", result.Feedback.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsumerNullabilityMustMatchCatalog()
    {
        var nonNullableBooleanReadAsNullable = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Get<bool?>(\"request.approved\") != null"),
            Catalog());
        var nonNullableStringReadAsNullable = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Get<string?>(\"request.name\") != null"),
            Catalog());
        using var nullableStringContract = JsonDocument.Parse("""
            {"name":"nullable-string-test","inputs":{},"outputs":{},"default_assumptions":{},"context_fields":{"schemaVersion":"loom.context-fields.v1","fields":[{"path":"value","type":"string","nullable":true,"owner":"caller","description":"Optional text."}]}}
            """);
        var nullableStringCatalog = ContextFieldCatalog.Parse(nullableStringContract.RootElement, "contract.json");
        var nullableStringReadAsNonNullable = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Get<string>(\"value\") != null"),
            nullableStringCatalog);
        var nullableStringReadCorrectly = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Get<string?>(\"value\") != null"),
            nullableStringCatalog);

        Assert.False(nonNullableBooleanReadAsNullable.IsSuccess);
        Assert.False(nonNullableStringReadAsNullable.IsSuccess);
        Assert.False(nullableStringReadAsNonNullable.IsSuccess);
        Assert.Equal("LOOM.EXPR.CONTEXT.FIELD_TYPE_MISMATCH", nullableStringReadAsNonNullable.Feedback.DiagnosticCode);
        Assert.True(nullableStringReadCorrectly.IsSuccess, nullableStringReadCorrectly.Feedback.Message);
    }

    [Fact]
    public void ArrayConsumerMustMatchRuntimeMaterializationAndElementNullability()
    {
        var listType = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Get<List<string>>(\"request.labels\") != null"),
            Catalog());
        var nullableElement = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Get<IReadOnlyList<string?>>(\"request.labels\") != null"),
            Catalog());

        Assert.False(listType.IsSuccess);
        Assert.False(nullableElement.IsSuccess);
        Assert.Equal("LOOM.EXPR.CONTEXT.FIELD_TYPE_MISMATCH", nullableElement.Feedback.DiagnosticCode);
    }

    [Fact]
    public void MismatchedConsumerTypeFailsWithStableCatalogDiagnostic()
    {
        var result = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Get<bool>(\"request.name\")"),
            Catalog(),
            "transition.guardExpression");

        Assert.False(result.IsSuccess);
        Assert.Equal("LOOM.EXPR.CONTEXT.FIELD_TYPE_MISMATCH", result.Feedback.DiagnosticCode);
        Assert.Equal("contract", result.Feedback.DiagnosticCategory);
        Assert.Equal("transition.guardExpression", result.Feedback.Field);
        Assert.Contains("request.name", result.Feedback.Message, StringComparison.Ordinal);
        Assert.NotNull(result.Feedback.SourceSpan);
    }

    [Fact]
    public void NullableScalarRequiresNullableConsumer()
    {
        var incompatible = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Get<bool>(\"request.optionalApproval\")"),
            Catalog());
        var compatible = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Get<bool?>(\"request.optionalApproval\") != null"),
            Catalog());

        Assert.False(incompatible.IsSuccess);
        Assert.Equal("LOOM.EXPR.CONTEXT.FIELD_TYPE_MISMATCH", incompatible.Feedback.DiagnosticCode);
        Assert.Contains("Get<bool?>", incompatible.Feedback.SuggestedFix, StringComparison.Ordinal);
        Assert.True(compatible.IsSuccess, compatible.Feedback.Message);
    }

    [Fact]
    public void ConflictingConsumersForOnePathFailCompile()
    {
        var result = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Get<string>(\"request.name\") != null && context.Get<bool>(\"request.name\")"),
            Catalog());

        Assert.False(result.IsSuccess);
        Assert.Equal("LOOM.EXPR.CONTEXT.FIELD_TYPE_MISMATCH", result.Feedback.DiagnosticCode);
    }

    [Fact]
    public void UndeclaredLiteralPathFailsCompile()
    {
        var result = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Get<string>(\"request.unlisted\") != null"),
            Catalog());

        Assert.False(result.IsSuccess);
        Assert.Equal("LOOM.EXPR.CONTEXT.FIELD_UNDECLARED", result.Feedback.DiagnosticCode);
    }

    [Fact]
    public void NullableMapContainerDoesNotMakeNonNullValuesRequireNullableReads()
    {
        using var document = JsonDocument.Parse("""
            {"name":"map-expression-test","inputs":{},"outputs":{},"default_assumptions":{},"context_fields":{"schemaVersion":"loom.context-fields.v1","fields":[{"path":"request.key","type":"string","nullable":false,"owner":"agent","description":"Dynamic key."},{"path":"request.flags","type":"object","valueType":"boolean","nullable":true,"owner":"agent","description":"Boolean-valued map whose container may be null."}]}}
            """);
        var catalog = ContextFieldCatalog.Parse(document.RootElement, "contract.json");

        var result = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Get<bool>(\"request.flags.\" + context.Get<string>(\"request.key\"))"),
            catalog);

        Assert.True(result.IsSuccess, result.Feedback.Message);
    }

    [Fact]
    public void DynamicMapReadRequiresAPathSeparatorBeforeTheKey()
    {
        using var document = JsonDocument.Parse("""
            {"name":"dynamic-map-separator-test","inputs":{},"outputs":{},"default_assumptions":{},"context_fields":{"schemaVersion":"loom.context-fields.v1","fields":[{"path":"request.key","type":"string","nullable":false,"owner":"agent","description":"Map key."},{"path":"request.results","type":"object","valueType":"boolean","nullable":false,"owner":"agent","description":"Boolean map."}]}}
            """);
        var catalog = ContextFieldCatalog.Parse(document.RootElement, "contract.json");
        var result = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Get<bool>(\"request.results\" + context.Get<string>(\"request.key\"))"),
            catalog);

        Assert.False(result.IsSuccess);
        Assert.Equal("LOOM.EXPR.CONTEXT.FIELD_UNDECLARED", result.Feedback.DiagnosticCode);
    }

    [Fact]
    public void DynamicMapInterpolatedKeyCompilesWithoutTrailingPathSegments()
    {
        var result = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("""context.Get<string>($"request.results.{context.Get<string>("request.key")}") != null"""),
            Catalog());

        Assert.True(result.IsSuccess, result.Feedback.Message);
    }

    [Fact]
    public void DynamicMapInterpolatedKeyWithTrailingPathSegmentFailsCompile()
    {
        var result = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("""context.Get<string>($"request.results.{context.Get<string>("request.key")}.nested") != null"""),
            Catalog());

        Assert.False(result.IsSuccess);
        Assert.Equal("LOOM.EXPR.CONTEXT.FIELD_UNDECLARED", result.Feedback.DiagnosticCode);
    }

    [Fact]
    public void DynamicMapValueUsesDeclaredValueType()
    {
        var result = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Get<string>(\"request.results.\" + context.Get<string>(\"request.key\")) != null"),
            Catalog());

        Assert.True(result.IsSuccess, result.Feedback.Message);
    }

    [Fact]
    public void HasIsPresenceOnlyButMustReferToDeclaredPath()
    {
        var declared = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Has(\"request.approved\")"),
            Catalog());
        var missing = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context.Has(\"request.unknown\")"),
            Catalog());

        Assert.True(declared.IsSuccess, declared.Feedback.Message);
        Assert.False(missing.IsSuccess);
        Assert.Equal("LOOM.EXPR.CONTEXT.FIELD_UNDECLARED", missing.Feedback.DiagnosticCode);
    }

    [Fact]
    public void UntypedIndexerCannotConsumeTypedBusinessField()
    {
        var result = _compiler.CompileWithContextFieldCatalog(
            Binding(),
            Predicate("context[\"request.approved\"] != null"),
            Catalog());

        Assert.False(result.IsSuccess);
        Assert.Equal("LOOM.EXPR.CONTEXT.INDEXER_TYPE_UNDECLARED", result.Feedback.DiagnosticCode);
    }

    private static ExpressionBinding Binding() => new();

    private static ExpressionDefinition Predicate(string source)
        => new() { Kind = "predicate", Source = source, EntryPoint = "Evaluate", ResultType = "bool" };

    private static ContextFieldCatalog Catalog()
    {
        using var document = JsonDocument.Parse("""
            {
              "name":"test-contract",
              "inputs":{},
              "outputs":{},
              "default_assumptions":{},
              "context_fields":{
                "schemaVersion":"loom.context-fields.v1",
                "fields":[
                  {"path":"request.name","type":"string","nullable":false,"owner":"caller","description":"Name."},
                  {"path":"request.approved","type":"boolean","nullable":false,"owner":"user","description":"User decision."},
                  {"path":"request.count","type":"integer","nullable":false,"owner":"agent","description":"Count."},
                  {"path":"request.labels","type":"array","itemType":"string","nullable":false,"owner":"agent","description":"Labels."},
                  {"path":"request.key","type":"string","nullable":false,"owner":"agent","description":"Dynamic map key."},
                  {"path":"request.results","type":"object","valueType":"string","nullable":false,"owner":"runtime","description":"String-valued dynamic map."},
                  {"path":"request.optionalApproval","type":"boolean","nullable":true,"owner":"user","description":"Optional approval."}
                ]
              }
            }
            """);
        return ContextFieldCatalog.Parse(document.RootElement, "contract.json");
    }
}
