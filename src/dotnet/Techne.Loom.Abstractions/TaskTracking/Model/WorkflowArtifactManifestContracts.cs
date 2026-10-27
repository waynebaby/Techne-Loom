using System.Text.Json.Serialization;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record WorkflowArtifactManifestEntry(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("exists")] bool Exists,
    [property: JsonPropertyName("size_bytes")] long? SizeBytes);

public sealed record WorkflowArtifactManifestResult(
    [property: JsonPropertyName("workflow_file")] string WorkflowFile,
    [property: JsonPropertyName("artifacts")] IReadOnlyList<WorkflowArtifactManifestEntry> Artifacts,
    [property: JsonPropertyName("operation_id")] string? OperationId = null);
