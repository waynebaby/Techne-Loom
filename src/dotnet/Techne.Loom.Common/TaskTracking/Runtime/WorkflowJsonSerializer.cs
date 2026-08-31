using System.Text.Json;
using System.Text.Json.Serialization;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class WorkflowJsonSerializer
{
    public static JsonSerializerOptions CreateDefaultOptions(bool indented = true)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new InferredTypeJsonConverter());

        return options;
    }

    public static string Serialize(WorkflowInstance instance, bool indented = true)
        => JsonSerializer.Serialize(instance, CreateDefaultOptions(indented));


    public static string FormatForFileOutput(string json)
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, CreateDefaultOptions(indented: true));
    }


    public static string FormatForFileOutputOrOriginal(string json)
    {
        try
        {
            return FormatForFileOutput(json);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    public static WorkflowInstance Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        var instance = JsonSerializer.Deserialize<WorkflowInstance>(json, CreateDefaultOptions())
            ?? throw new InvalidOperationException("Failed to deserialize workflow instance.");
        if (document.RootElement.TryGetProperty("nodes", out var nodes))
        {
            foreach (var node in nodes.EnumerateObject())
            {
                if (instance.Nodes.TryGetValue(node.Name, out var taskNode) && taskNode is TransitionBase transition && node.Value.ValueKind == JsonValueKind.Object)
                {
                    transition.GuardExpressionWasExplicitlyDeclared = node.Value.TryGetProperty("guardExpression", out _);
                    transition.SucceedExpressionWasExplicitlyDeclared = node.Value.TryGetProperty("succeedExpression", out _);
                }
            }
        }

        return instance;
    }
}
