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

    public static WorkflowInstance Deserialize(string json)
        => JsonSerializer.Deserialize<WorkflowInstance>(json, CreateDefaultOptions())
           ?? throw new InvalidOperationException("Failed to deserialize workflow instance.");
}