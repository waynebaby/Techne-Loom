using System.Text.Json.Serialization;

namespace Techne.Loom.AgentOrchestrator.Models;

public sealed record AoSamplingRequest(
    [property: JsonPropertyName("objective")] string Objective,
    [property: JsonPropertyName("artifacts")] IReadOnlyList<string> Artifacts);
