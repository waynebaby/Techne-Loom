using System.Text.Json.Serialization;

namespace Techne.Loom.AgentOrchestrator.Models;

public sealed record AoWeaveOutContext(
    [property: JsonPropertyName("route")] string Route,
    [property: JsonPropertyName("return_channel")] string? ReturnChannel = null,
    [property: JsonPropertyName("session_id")] string? SessionId = null,
    [property: JsonPropertyName("metadata")] Dictionary<string, object?>? Metadata = null);
