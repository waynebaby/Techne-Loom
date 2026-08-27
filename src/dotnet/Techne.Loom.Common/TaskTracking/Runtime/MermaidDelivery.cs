using System.Text.Json.Serialization;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record MermaidDelivery(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("mermaid_file")] string? MermaidFile,
    [property: JsonPropertyName("html_file")] string? HtmlFile,
    [property: JsonPropertyName("workspace_root")] string? WorkspaceRoot,
    [property: JsonPropertyName("workspace_step_directory")] string? WorkspaceStepDirectory,
    [property: JsonPropertyName("workspace_mermaid_file")] string? WorkspaceMermaidFile,
    [property: JsonPropertyName("workspace_html_file")] string? WorkspaceHtmlFile,
    [property: JsonPropertyName("mirror_status")] string MirrorStatus,
    [property: JsonPropertyName("mermaid_exists")] bool MermaidExists,
    [property: JsonPropertyName("html_exists")] bool HtmlExists,
    [property: JsonPropertyName("mermaid_readable")] bool MermaidReadable,
    [property: JsonPropertyName("html_readable")] bool HtmlReadable,
    [property: JsonPropertyName("mermaid_size_bytes")] long? MermaidSizeBytes,
    [property: JsonPropertyName("html_size_bytes")] long? HtmlSizeBytes,
    [property: JsonPropertyName("mermaid_sha256")] string? MermaidSha256,
    [property: JsonPropertyName("html_sha256")] string? HtmlSha256,
    [property: JsonPropertyName("preview_status")] string PreviewStatus,
    [property: JsonPropertyName("card_status")] string CardStatus,
    [property: JsonPropertyName("card_input_file")] string? CardInputFile,
    [property: JsonPropertyName("card_fallback")] string? CardFallback,
    [property: JsonPropertyName("source_step_directory")] string? SourceStepDirectory,
    [property: JsonPropertyName("reuse_manifest_file")] string? ReuseManifestFile,
    [property: JsonPropertyName("error")] string? Error)
{
    [JsonPropertyName("workspace_relative_mermaid_file")]
    public string? WorkspaceRelativeMermaidFile { get; init; }

    [JsonPropertyName("workspace_relative_html_file")]
    public string? WorkspaceRelativeHtmlFile { get; init; }

    [JsonPropertyName("generation_status")]
    public string GenerationStatus { get; init; } = "unknown";

    [JsonPropertyName("artifact_generated")]
    public bool ArtifactGenerated { get; init; }

    [JsonPropertyName("link_resolvable")]
    public bool LinkResolvable { get; init; }

    [JsonPropertyName("visual_preview_rendered")]
    public bool VisualPreviewRendered { get; init; }

    [JsonPropertyName("card_display_available")]
    public bool CardDisplayAvailable { get; init; }

    public static MermaidDelivery Failed(string error)
        => new(
            Status: "delivery_failed",
            MermaidFile: null,
            HtmlFile: null,
            WorkspaceRoot: null,
            WorkspaceStepDirectory: null,
            WorkspaceMermaidFile: null,
            WorkspaceHtmlFile: null,
            MirrorStatus: "not-attempted",
            MermaidExists: false,
            HtmlExists: false,
            MermaidReadable: false,
            HtmlReadable: false,
            MermaidSizeBytes: null,
            HtmlSizeBytes: null,
            MermaidSha256: null,
            HtmlSha256: null,
            PreviewStatus: "unavailable",
            CardStatus: "unavailable",
            CardInputFile: null,
            CardFallback: "direct-link",
            SourceStepDirectory: null,
            ReuseManifestFile: null,
            Error: string.IsNullOrWhiteSpace(error) ? "Mermaid delivery failed." : error)
        {
            GenerationStatus = "unknown",
            ArtifactGenerated = false,
            LinkResolvable = false,
            VisualPreviewRendered = false,
            CardDisplayAvailable = false,
        };
}
