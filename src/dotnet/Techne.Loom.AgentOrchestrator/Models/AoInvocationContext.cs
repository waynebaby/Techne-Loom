using System.Text.Json.Serialization;

namespace Techne.Loom.AgentOrchestrator.Models;

public sealed record AoInvocationContext(
    [property: JsonPropertyName("weave_out")] AoWeaveOutContext? WeaveOut = null);
