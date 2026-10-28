using System.Text.Json.Serialization;

namespace Techne.Loom.AgentOrchestrator.Models;

public sealed record AoPropertyEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("ts")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("payload")] object Payload);
