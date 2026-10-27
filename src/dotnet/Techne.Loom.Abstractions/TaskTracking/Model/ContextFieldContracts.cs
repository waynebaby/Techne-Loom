using System.Globalization;
using System.Text.Json;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record ContextFieldSuggestion(
    JsonElement Value,
    string Label,
    string Source,
    string Kind);

public sealed record ContextFieldDefinition(
    string Path,
    string Type,
    bool Nullable,
    string Owner,
    string Description,
    string? ItemType,
    string? ValueType,
    decimal? Minimum,
    decimal? Maximum,
    IReadOnlyList<JsonElement> AllowedValues,
    IReadOnlyList<ContextFieldSuggestion> SuggestedValues,
    string? SuggestionReason);
