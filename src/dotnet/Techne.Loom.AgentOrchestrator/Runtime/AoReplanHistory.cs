using System.Text.Json;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Runtime;

internal static class AoReplanHistory
{
    private static readonly JsonSerializerOptions JsonOptions = WorkflowJsonSerializer.CreateDefaultOptions(indented: false);

    public static List<Dictionary<string, object?>> Read(IReadOnlyDictionary<string, object?> context)
    {
        return context.TryGetValue("replan_history", out var value)
            ? Read(value)
            : [];
    }

    public static List<Dictionary<string, object?>> Read(object? value)
    {
        if (value is JsonElement { ValueKind: JsonValueKind.Array } element)
        {
            return element.EnumerateArray()
                .Select(static item => ToDictionary(item))
                .Where(static item => item is not null)
                .Cast<Dictionary<string, object?>>()
                .ToList();
        }

        if (value is IEnumerable<Dictionary<string, object?>> dictionaries)
        {
            return dictionaries.Select(CloneDictionary).ToList();
        }

        if (value is IEnumerable<IReadOnlyDictionary<string, object?>> readOnlyDictionaries)
        {
            return readOnlyDictionaries
                .Select(static item => new Dictionary<string, object?>(item, StringComparer.Ordinal))
                .Select(CloneDictionary)
                .ToList();
        }

        if (value is IEnumerable<object?> items)
        {
            return items
                .Select(static item => ToDictionary(item))
                .Where(static item => item is not null)
                .Cast<Dictionary<string, object?>>()
                .ToList();
        }

        return [];
    }

    public static List<Dictionary<string, object?>> Merge(object? existing, object? incoming)
    {
        var merged = Read(existing);
        foreach (var entry in Read(incoming))
        {
            var serialized = Serialize(entry);
            if (merged.All(existingEntry => !string.Equals(Serialize(existingEntry), serialized, StringComparison.Ordinal)))
            {
                merged.Add(entry);
            }
        }

        return merged;
    }

    public static void Set(Dictionary<string, object?> context, IReadOnlyList<Dictionary<string, object?>> history)
    {
        context["replan_history"] = history.Select(static entry => (object?)CloneDictionary(entry)).ToList();
    }

    public static Dictionary<string, object?> CloneDictionary(IReadOnlyDictionary<string, object?> source)
    {
        return source.ToDictionary(static pair => pair.Key, static pair => CloneValue(pair.Value), StringComparer.Ordinal);
    }

    public static Dictionary<string, object?>? ToDictionary(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } element)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(element.GetRawText(), JsonOptions);
        }

        if (value is IReadOnlyDictionary<string, object?> readOnly)
        {
            return CloneDictionary(readOnly);
        }

        if (value is IDictionary<string, object?> mutable)
        {
            return CloneDictionary(new Dictionary<string, object?>(mutable, StringComparer.Ordinal));
        }

        return null;
    }

    public static bool HasMeaningfulValue(IReadOnlyDictionary<string, object?> context, string key)
    {
        return context.TryGetValue(key, out var value) && HasMeaningfulValue(value);
    }

    public static bool HasMeaningfulValue(object? value)
    {
        return value switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => false,
            JsonElement { ValueKind: JsonValueKind.String } element => !string.IsNullOrWhiteSpace(element.GetString()),
            JsonElement { ValueKind: JsonValueKind.Array } element => element.GetArrayLength() > 0,
            JsonElement { ValueKind: JsonValueKind.Object } element => element.EnumerateObject().Any(),
            IDictionary<string, object?> dictionary => dictionary.Count > 0,
            IReadOnlyDictionary<string, object?> dictionary => dictionary.Count > 0,
            IEnumerable<object?> items => items.Any(),
            _ => true,
        };
    }

    public static string? GetString(IReadOnlyDictionary<string, object?> context, string key)
    {
        if (!context.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => Convert.ToString(value),
        };
    }

    public static Dictionary<string, object?> ToAuditValue(WorkflowAuditArtifacts artifacts)
    {
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(
                   JsonSerializer.Serialize(artifacts, JsonOptions),
                   JsonOptions)
               ?? new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    public static object? CloneValue(object? value)
    {
        return value switch
        {
            null => null,
            JsonElement element => element.Clone(),
            Dictionary<string, object?> dictionary => CloneDictionary(dictionary),
            IDictionary<string, object?> dictionary => CloneDictionary(new Dictionary<string, object?>(dictionary, StringComparer.Ordinal)),
            IReadOnlyDictionary<string, object?> dictionary => CloneDictionary(dictionary),
            IEnumerable<object?> items => items.Select(CloneValue).ToList(),
            _ => value,
        };
    }

    private static string Serialize(IReadOnlyDictionary<string, object?> value)
        => JsonSerializer.Serialize(value, JsonOptions);
}
