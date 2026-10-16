using System.Text.Json;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class ContextFieldCatalogTests
{
    [Fact]
    public void ValidateContextValuesRejectsUndeclaredRootAndAllowsDeclaredRootContainer()
    {
        using var document = JsonDocument.Parse(ContractJson(
            """[{"path":"review.approved","type":"boolean","nullable":false,"owner":"user","description":"Review decision."}]"""));
        var catalog = ContextFieldCatalog.Parse(document.RootElement, "contract.json");
        var undeclared = new Dictionary<string, object?>(StringComparer.Ordinal) { ["unlisted"] = "value" };
        var declared = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["review"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["approved"] = true },
        };

        var error = Assert.Throws<InvalidOperationException>(() => catalog.ValidateContextValues(undeclared, "workflow.json"));
        Assert.Contains("Context root 'unlisted' is not declared", error.Message, StringComparison.Ordinal);
        catalog.ValidateContextValues(declared, "workflow.json");
    }

    [Fact]
    public void ParseReadsPrimitiveAndContainerTypesAndSuggestedValues()
    {
        using var document = JsonDocument.Parse(ContractJson("""
            [
              {"path":"review.approved","type":"boolean","nullable":false,"owner":"user","description":"Approval choice.","allowed_values":[true,false],"suggested_values":[{"value":true,"label":"Approve","source":"explicit user choice","kind":"choice"}]},
              {"path":"review.count","type":"integer","nullable":false,"owner":"agent","description":"Observed count.","minimum":0,"suggested_values":[]},
              {"path":"review.labels","type":"array","itemType":"string","nullable":false,"owner":"agent","description":"Observed labels.","suggested_values":[]},
              {"path":"review.results","type":"object","valueType":"string","nullable":false,"owner":"runtime","description":"Map of result summaries.","suggested_values":[]}
            ]
            """));

        var catalog = ContextFieldCatalog.Parse(document.RootElement, "contract.json");

        Assert.Equal(ContextFieldCatalog.CurrentSchemaVersion, catalog.SchemaVersion);
        Assert.Equal("boolean", catalog.Fields["review.approved"].Type);
        Assert.Equal("string", catalog.Fields["review.labels"].ItemType);
        Assert.Equal("string", catalog.Fields["review.results"].ValueType);
        Assert.Equal(JsonValueKind.True, catalog.Fields["review.approved"].SuggestedValues[0].Value.ValueKind);
    }

    [Fact]
    public void TryGetFieldResolvesMapValuesAsNonNullableDynamicFields()
    {
        using var document = JsonDocument.Parse(ContractJson("""
            [{"path":"review.results","type":"object","valueType":"boolean","nullable":true,"owner":"runtime","description":"Nullable map container."}]
            """));
        var catalog = ContextFieldCatalog.Parse(document.RootElement, "contract.json");

        Assert.True(catalog.TryGetField("review.results.approved", out var field));
        Assert.Equal("boolean", field.Type);
        Assert.False(field.Nullable);
        Assert.Equal("runtime", field.Owner);
    }

    [Fact]
    public void ValidateFieldValue_EnforcesDeclaredIntegerMinimum()
    {
        using var document = JsonDocument.Parse(ContractJson(
            """[{"path":"review.findings_count","type":"integer","nullable":false,"owner":"agent","description":"Finding count.","minimum":0}]"""));
        var catalog = ContextFieldCatalog.Parse(document.RootElement, "contract.json");

        Assert.Equal(0m, catalog.Fields["review.findings_count"].Minimum);
        var error = Assert.Throws<InvalidOperationException>(() =>
            catalog.ValidateFieldValue("review.findings_count", -1, "workflow.json"));
        Assert.Contains("below minimum 0", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateFieldValue_AllowedObjectsCompareByJsonSemantics()
    {
        using var document = JsonDocument.Parse(ContractJson(
            """[{"path":"settings","type":"object","nullable":false,"owner":"caller","description":"Settings.","allowed_values":[{"a":1,"b":2}]}]"""));
        var catalog = ContextFieldCatalog.Parse(document.RootElement, "contract.json");
        var reorderedObject = new Dictionary<string, object?>(StringComparer.Ordinal) { ["b"] = 2, ["a"] = 1 };

        catalog.ValidateFieldValue("settings", reorderedObject, "workflow.json");
    }

    [Fact]
    public void ParseRejectsSuggestionsOutsideAllowedValues()
    {
        using var document = JsonDocument.Parse(ContractJson(
            """[{"path":"choice","type":"string","nullable":false,"owner":"user","description":"Choice.","allowed_values":["yes"],"suggested_values":[{"value":"no","label":"No","source":"test","kind":"choice"}]}]"""));

        var error = Assert.Throws<InvalidOperationException>(() => ContextFieldCatalog.Parse(document.RootElement, "contract.json"));

        Assert.Contains("suggested_values must be members of allowed_values", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing catalog", "{\"name\":\"x\",\"inputs\":{},\"outputs\":{},\"default_assumptions\":{}}")]
    [InlineData("wrong version", "{\"context_fields\":{\"schemaVersion\":\"v9\",\"fields\":[]}}")]
    [InlineData("duplicate paths", "{\"context_fields\":{\"schemaVersion\":\"loom.context-fields.v1\",\"fields\":[{\"path\":\"x\",\"type\":\"string\",\"nullable\":false,\"owner\":\"agent\",\"description\":\"x\"},{\"path\":\"x\",\"type\":\"string\",\"nullable\":false,\"owner\":\"agent\",\"description\":\"x\"}]}}")]
    [InlineData("unknown type", "{\"context_fields\":{\"schemaVersion\":\"loom.context-fields.v1\",\"fields\":[{\"path\":\"x\",\"type\":\"dynamic\",\"nullable\":false,\"owner\":\"agent\",\"description\":\"x\"}]}}")]
    [InlineData("string suggestion for boolean", "{\"context_fields\":{\"schemaVersion\":\"loom.context-fields.v1\",\"fields\":[{\"path\":\"x\",\"type\":\"boolean\",\"nullable\":false,\"owner\":\"agent\",\"description\":\"x\",\"suggested_values\":[{\"value\":\"true\",\"label\":\"True\",\"source\":\"test\",\"kind\":\"choice\"}]}]}}")]
    [InlineData("array without item type", "{\"context_fields\":{\"schemaVersion\":\"loom.context-fields.v1\",\"fields\":[{\"path\":\"x\",\"type\":\"array\",\"nullable\":false,\"owner\":\"agent\",\"description\":\"x\"}]}}")]
    [InlineData("nullable true candidate on nonnullable", "{\"context_fields\":{\"schemaVersion\":\"loom.context-fields.v1\",\"fields\":[{\"path\":\"x\",\"type\":\"string\",\"nullable\":false,\"owner\":\"agent\",\"description\":\"x\",\"suggested_values\":[{\"value\":null,\"label\":\"Empty\",\"source\":\"test\",\"kind\":\"example\"}]}]}}")]
    [InlineData("array index path", "{\"context_fields\":{\"schemaVersion\":\"loom.context-fields.v1\",\"fields\":[{\"path\":\"items\",\"type\":\"array\",\"itemType\":\"string\",\"nullable\":false,\"owner\":\"agent\",\"description\":\"Items.\"},{\"path\":\"items.0\",\"type\":\"string\",\"nullable\":false,\"owner\":\"agent\",\"description\":\"First item.\"}]}}")]
    [InlineData("path segment boundary whitespace", "{\"context_fields\":{\"schemaVersion\":\"loom.context-fields.v1\",\"fields\":[{\"path\":\"topic \",\"type\":\"string\",\"nullable\":false,\"owner\":\"caller\",\"description\":\"x\"}]}}")]
    public void ParseRejectsInvalidCatalog(string scenario, string json)
    {
        Assert.False(string.IsNullOrWhiteSpace(scenario));
        using var document = JsonDocument.Parse(json);

        var error = Assert.Throws<InvalidOperationException>(() => ContextFieldCatalog.Parse(document.RootElement, "contract.json"));

        Assert.Contains("Context field catalog", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string ContractJson(string fields)
        => $$"""
        {
          "name": "test-contract",
          "inputs": {},
          "outputs": {},
          "default_assumptions": {},
          "context_fields": {
            "schemaVersion": "loom.context-fields.v1",
            "fields": {{fields}}
          }
        }
        """;
}
