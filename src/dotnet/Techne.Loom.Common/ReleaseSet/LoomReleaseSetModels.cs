using System.Text.Json.Serialization;

namespace Techne.Loom.Common.ReleaseSet;

public enum LoomReleaseSetAuthorityMode
{
    CheckIn,
    Release,
}

public enum LoomReleaseSetValidationPhase
{
    PrePublish,
    PrePublishPackageClosure,
    PostPublish,
    PostPublishPackageClosure,
}

public sealed record LoomReleaseSetValidationRequest
{
    public required string RepositoryRoot { get; init; }
    public string ManifestPath { get; init; } = "release-set.json";
    public required string Channel { get; init; }
    public required LoomReleaseSetAuthorityMode AuthorityMode { get; init; }
    public LoomReleaseSetValidationPhase Phase { get; init; } = LoomReleaseSetValidationPhase.PrePublish;
    public string? CandidateVersion { get; init; }
    public string? PackageRoot { get; init; }
    public ILoomReleaseSetPackageMetadataSource? PackageMetadataSource { get; init; }
}

public interface ILoomReleaseSetPackageMetadataSource
{
    Task<string> GetLatestVersionAsync(string packageId, string channel, CancellationToken cancellationToken = default);
}

public sealed class LoomReleaseSetManifest
{
    [JsonPropertyName("schema_version")]
    public string? SchemaVersion { get; set; }

    [JsonPropertyName("release_set_id")]
    public string? ReleaseSetId { get; set; }

    [JsonPropertyName("version_authority")]
    public LoomReleaseSetVersionAuthority? VersionAuthority { get; set; }

    [JsonPropertyName("channels")]
    public Dictionary<string, LoomReleaseSetChannelRule>? Channels { get; set; }

    [JsonPropertyName("packages")]
    public LoomReleaseSetPackageScope? Packages { get; set; }

    [JsonPropertyName("skills")]
    public List<LoomReleaseSetSkillSurface>? Skills { get; set; }

    [JsonPropertyName("surfaces")]
    public LoomReleaseSetSurfaceSet? Surfaces { get; set; }

    [JsonPropertyName("ci")]
    public LoomReleaseSetCiContract? Ci { get; set; }

    [JsonPropertyName("excluded_artifacts")]
    public List<LoomReleaseSetExcludedArtifact>? ExcludedArtifacts { get; set; }
}

public sealed class LoomReleaseSetVersionAuthority
{
    [JsonPropertyName("check_in")]
    public LoomReleaseSetAuthorityRule? CheckIn { get; set; }

    [JsonPropertyName("release")]
    public LoomReleaseSetAuthorityRule? Release { get; set; }
}

public sealed class LoomReleaseSetAuthorityRule
{
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("requires_all_packages_same_exact_version")]
    public bool? RequiresAllPackagesSameExactVersion { get; set; }
}

public sealed class LoomReleaseSetChannelRule
{
    [JsonPropertyName("version_pattern")]
    public string? VersionPattern { get; set; }

    [JsonPropertyName("package_index_paths")]
    public List<string>? PackageIndexPaths { get; set; }
}

public sealed class LoomReleaseSetPackageScope
{
    [JsonPropertyName("core")]
    public List<string>? Core { get; set; }

    [JsonPropertyName("runtime")]
    public LoomReleaseSetRuntimeScope? Runtime { get; set; }
}

public sealed class LoomReleaseSetRuntimeScope
{
    [JsonPropertyName("products")]
    public List<string>? Products { get; set; }

    [JsonPropertyName("rids")]
    public List<string>? RuntimeIdentifiers { get; set; }
}

public sealed class LoomReleaseSetSkillSurface
{
    [JsonPropertyName("product")]
    public string? Product { get; set; }

    [JsonPropertyName("root")]
    public string? Root { get; set; }

    [JsonPropertyName("version_block")]
    public string? VersionBlock { get; set; }

    [JsonPropertyName("package_lock")]
    public string? PackageLock { get; set; }

    [JsonPropertyName("document_copy_manifest")]
    public string? DocumentCopyManifest { get; set; }
}

public sealed class LoomReleaseSetSurfaceSet
{
    [JsonPropertyName("package_indexes")]
    public List<LoomReleaseSetPackageIndexSurface> PackageIndexes { get; set; } = [];

    [JsonPropertyName("guide_metadata")]
    public List<LoomReleaseSetGuideSurface> GuideMetadata { get; set; } = [];

    [JsonPropertyName("workflow_paths")]
    public List<string> WorkflowPaths { get; set; } = [];
}

public sealed class LoomReleaseSetPackageIndexSurface
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("channel")]
    public string? Channel { get; set; }
}

public sealed class LoomReleaseSetGuideSurface
{
    [JsonPropertyName("glob")]
    public string? Glob { get; set; }

    [JsonPropertyName("product")]
    public string? Product { get; set; }
}

public sealed class LoomReleaseSetCiContract
{
    [JsonPropertyName("workflow_paths")]
    public List<string>? WorkflowPaths { get; set; }

    [JsonPropertyName("shared_version_job_id")]
    public string? SharedVersionJobId { get; set; }

    [JsonPropertyName("package_version_output")]
    public string? PackageVersionOutput { get; set; }

    [JsonPropertyName("forbidden_automation_patterns")]
    public List<string>? ForbiddenAutomationPatterns { get; set; }
}

public sealed class LoomReleaseSetExcludedArtifact
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("classification")]
    public string? Classification { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public sealed record LoomReleaseSetValidationIssue(
    string Code,
    string Path,
    string Message);

public sealed record LoomReleaseSetValidationReport(
    bool IsValid,
    string AuthorityMode,
    string Phase,
    string Channel,
    string? ExpectedVersion,
    IReadOnlyDictionary<string, string> LatestPackageVersions,
    IReadOnlyList<LoomReleaseSetValidationIssue> Issues)
{
    public string ToDiagnosticString()
    {
        if (IsValid)
        {
            return $"Release set validation passed for channel '{Channel}' with version '{ExpectedVersion}'.";
        }

        var lines = new List<string>
        {
            $"Release set validation failed for channel '{Channel}'.",
        };
        lines.AddRange(Issues.Select(issue => $"[{issue.Code}] {issue.Path}: {issue.Message}"));
        return string.Join(Environment.NewLine, lines);
    }
}
