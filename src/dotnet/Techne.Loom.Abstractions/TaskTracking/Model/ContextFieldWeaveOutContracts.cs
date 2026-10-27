using System.Text.Json;
using System.Text.Json.Serialization;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record ContextFieldConsumerExpression(
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("read_as")] string ReadAs);

public sealed record ContextFieldWeaveOutField(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("nullable")] bool Nullable,
    [property: JsonPropertyName("owner")] string Owner,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("item_type")] string? ItemType,
    [property: JsonPropertyName("value_type")] string? ValueType,
    [property: JsonPropertyName("minimum")] decimal? Minimum,
    [property: JsonPropertyName("maximum")] decimal? Maximum,
    [property: JsonPropertyName("allowed_values")] IReadOnlyList<JsonElement> AllowedValues,
    [property: JsonPropertyName("suggested_values")] IReadOnlyList<ContextFieldSuggestion> SuggestedValues,
    [property: JsonPropertyName("suggestion_reason")] string? SuggestionReason,
    [property: JsonPropertyName("consumer_expressions")] IReadOnlyList<ContextFieldConsumerExpression> ConsumerExpressions,
    [property: JsonPropertyName("host_decision_guidance")] string? HostDecisionGuidance);
