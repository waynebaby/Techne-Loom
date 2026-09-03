using System.Text.Json;
using System.Text.Json.Serialization;

namespace Techne.Loom.Abstractions.TaskTracking.Model;

public static class ExpressionContract
{
    public const string CurrentLanguage = "csharp";
    public const string CurrentLanguageVersion = "12";
    public const string CurrentContractId = "loom.expression.csharp";
    public const string CurrentContractVersion = "1";
    public const string DetailedCompileFeedbackContract = "detailedCompileFeedbackV1";
}


public static class ExpressionCapabilityIds
{
    public const string ContextGet = "loom.expression.context.get";
    public const string ContextHas = "loom.expression.context.has";
    public const string ContextIndexer = "loom.expression.context.indexer";
    public const string StringOrdinal = "loom.expression.string.ordinal";
    public const string Math = "loom.expression.math";
    public const string TimeSpan = "loom.expression.timespan";
    public const string Regex = "loom.expression.regex";
    public const string InvariantParsing = "loom.expression.parsing.invariant";
    public const string BoundedCollections = "loom.expression.collections.bounded";
}

public sealed class ExpressionBinding
{
    public string Language { get; set; } = ExpressionContract.CurrentLanguage;

    public string LanguageVersion { get; set; } = ExpressionContract.CurrentLanguageVersion;

    public string ContractId { get; set; } = ExpressionContract.CurrentContractId;

    public string ContractVersion { get; set; } = ExpressionContract.CurrentContractVersion;

    public List<string> RequiredExpressionCapabilities { get; set; } = [];

    public string CompileFeedbackContract { get; set; } = ExpressionContract.DetailedCompileFeedbackContract;
}

[JsonConverter(typeof(ExpressionDefinitionJsonConverter))]
public sealed class ExpressionDefinition
{
    public string Kind { get; set; } = "predicate";

    public string Source { get; set; } = "true";

    public string? EntryPoint { get; set; }

    public string ResultType { get; set; } = "bool";

    public static implicit operator ExpressionDefinition(string source)
    {
        return new ExpressionDefinition { Source = source };
    }

    public static implicit operator string?(ExpressionDefinition? definition)
    {
        return definition?.Source;
    }
}

public sealed class ExpressionDefinitionJsonConverter : JsonConverter<ExpressionDefinition>
{
    public override ExpressionDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new ExpressionDefinition { Source = reader.GetString() ?? string.Empty };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expression definitions must be a string shorthand or an object.");
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        return new ExpressionDefinition
        {
            Kind = GetString(root, "kind") ?? "predicate",
            Source = GetString(root, "source") ?? string.Empty,
            EntryPoint = GetString(root, "entryPoint"),
            ResultType = GetString(root, "resultType") ?? "bool",
        };
    }

    public override void Write(Utf8JsonWriter writer, ExpressionDefinition value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", value.Kind);
        writer.WriteString("source", value.Source);
        if (value.EntryPoint is not null)
        {
            writer.WriteString("entryPoint", value.EntryPoint);
        }

        writer.WriteString("resultType", value.ResultType);
        writer.WriteEndObject();
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}
