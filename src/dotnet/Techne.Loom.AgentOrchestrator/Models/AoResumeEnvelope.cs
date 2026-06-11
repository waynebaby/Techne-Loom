using System.Text.Json.Serialization;

namespace Techne.Loom.AgentOrchestrator.Models;

public sealed record AoResumeEnvelope(
    [property: JsonPropertyName("transition_id")] string TransitionId,
    [property: JsonPropertyName("correlation_key")] string? CorrelationKey,
    [property: JsonPropertyName("payload")] Dictionary<string, object?>? Payload);
