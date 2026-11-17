using System.Globalization;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

internal static class WorkflowDraftImport
{
    internal static WorkflowInstance Read(string json)
    {
        using var source = JsonDocument.Parse(json);
        if (source.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A workflow draft must be a JSON object.");
        }

        RejectDuplicateProperties(source.RootElement, "");
        if (source.RootElement.TryGetProperty("nodes", out var nodes) && nodes.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Expected an object at '/nodes'.");
        }

        var workflow = WorkflowJsonSerializer.Deserialize(json);
        using var serialized = JsonDocument.Parse(WorkflowJsonSerializer.Serialize(workflow));
        VerifyEquivalent(source.RootElement, serialized.RootElement, "");

        WorkflowJsonToCSharpBuilderExporter.EnsureTemplateOnly(workflow);
        if (string.IsNullOrWhiteSpace(workflow.InstanceId)
            || !string.Equals(workflow.CurrentNodeId, workflow.StartNodeId, StringComparison.Ordinal)
            || workflow.Nodes.Values.OfType<StateNode>().Any(state => state.EntranceTime.HasValue))
        {
            throw new InvalidOperationException("The workflow has no template identity or carries execution state.");
        }

        foreach (var (key, node) in workflow.Nodes)
        {
            if (string.IsNullOrWhiteSpace(key) || !string.Equals(key, node?.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Invalid node identity at '{AppendPointer("/nodes", key)}'.");
            }
        }

        return workflow;
    }

    private static void RejectDuplicateProperties(JsonElement element, string pointer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    var child = AppendPointer(pointer, property.Name);
                    if (!names.Add(property.Name))
                    {
                        throw new JsonException($"Duplicate workflow property at '{child}'.");
                    }

                    RejectDuplicateProperties(property.Value, child);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    RejectDuplicateProperties(item, AppendPointer(pointer, index.ToString(CultureInfo.InvariantCulture)));
                    index++;
                }
                break;
        }
    }

    private static void VerifyEquivalent(JsonElement source, JsonElement result, string pointer)
    {
        if (source.ValueKind == JsonValueKind.Object && result.ValueKind == JsonValueKind.Object)
        {
            var remaining = result.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
            foreach (var property in source.EnumerateObject())
            {
                var child = AppendPointer(pointer, property.Name);
                if (!remaining.Remove(property.Name, out var value))
                {
                    throw new JsonException($"Workflow property is not preserved by the serializer at '{child}'.");
                }

                VerifyEquivalent(property.Value, value, child);
            }

            if (remaining.Count > 0)
            {
                throw new JsonException($"Workflow serializer added a field at '{AppendPointer(pointer, remaining.Keys.First())}'.");
            }
            return;
        }

        if (source.ValueKind == JsonValueKind.Array && result.ValueKind == JsonValueKind.Array
            && source.GetArrayLength() == result.GetArrayLength())
        {
            var index = 0;
            using var resultItems = result.EnumerateArray();
            foreach (var item in source.EnumerateArray())
            {
                resultItems.MoveNext();
                VerifyEquivalent(item, resultItems.Current, AppendPointer(pointer, index.ToString(CultureInfo.InvariantCulture)));
                index++;
            }
            return;
        }

        // The inferred context converter can bind 1 and 1e0 to different CLR types.
        if (source.ValueKind == JsonValueKind.Number && result.ValueKind == JsonValueKind.Number)
        {
            if (source.GetRawText() == result.GetRawText())
            {
                return;
            }
        }
        else if (JsonElement.DeepEquals(source, result))
        {
            return;
        }

        throw new JsonException($"Workflow value changed during serialization at '{pointer}'.");
    }

    private static string AppendPointer(string parent, string segment)
        => parent + "/" + segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}
