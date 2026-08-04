using System.Text.Json.Serialization;

namespace Techne.Loom.AgentOrchestrator.Models;

public sealed record AoWeaveOutRequest(
    [property: JsonPropertyName("objective")] string Objective,
    [property: JsonPropertyName("artifacts")] IReadOnlyList<string> Artifacts,
    [property: JsonPropertyName("evidence_references")] IReadOnlyList<AoEvidenceReference>? EvidenceReferences = null);

public sealed record AoEvidenceReference(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("start_line")] int StartLine,
    [property: JsonPropertyName("end_line")] int EndLine,
    [property: JsonPropertyName("role")] string Role);
