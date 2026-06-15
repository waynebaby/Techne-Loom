using System.Text.Json;
using System.Text.Json.Serialization;

namespace Techne.Loom.AgentOrchestrator.Models;

public enum AoPromptBlockConsumptionRequirement
{
    Required,
    Optional,
}

public sealed record AoPromptBlock(
    [property: JsonPropertyName("block_id")] string BlockId,
    [property: JsonPropertyName("block_kind")] string BlockKind,
    [property: JsonPropertyName("semantic_role")] string SemanticRole,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("content_type")] string ContentType,
    [property: JsonPropertyName("order")] int Order,
    [property: JsonPropertyName("consumption_requirement")]
    [property: JsonConverter(typeof(AoPromptBlockConsumptionRequirementJsonConverter))]
    AoPromptBlockConsumptionRequirement ConsumptionRequirement,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("tags")] IReadOnlyList<string>? Tags = null);

internal sealed class AoPromptBlockConsumptionRequirementJsonConverter : JsonConverter<AoPromptBlockConsumptionRequirement>
{
    public override AoPromptBlockConsumptionRequirement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetString() switch
        {
            "required" => AoPromptBlockConsumptionRequirement.Required,
            "optional" => AoPromptBlockConsumptionRequirement.Optional,
            var value => throw new JsonException($"Unsupported consumption requirement '{value ?? "<null>"}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, AoPromptBlockConsumptionRequirement value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            AoPromptBlockConsumptionRequirement.Required => "required",
            AoPromptBlockConsumptionRequirement.Optional => "optional",
            _ => throw new JsonException($"Unsupported consumption requirement '{value}'."),
        });
    }
}