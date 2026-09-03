namespace Techne.Loom.Common.Runtime;

public sealed class LoomRuntimeResolutionRequest
{
    public LoomRuntimeProduct Product { get; init; }
    public required string Version { get; init; }
    public string Channel { get; init; } = "released";
    public string? FrameworkBundleDirectory { get; init; }
    public string? CacheRoot { get; init; }
    public string? RuntimeIdentifier { get; init; }
    public string? NuGetPackageCacheRoot { get; init; }
    public bool ForceSelfContained { get; init; }
    public TimeSpan LockTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan GuideTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

public enum LoomRuntimeFailureCategory
{
    Acquisition,
    Integrity,
    HostStartup,
    GuideValidation,
    Command,
}

public sealed record LoomLaunchDescriptor(
    LoomRuntimeMode RuntimeMode,
    LoomRuntimeProduct Product,
    string ResolvedRuntimeVersion,
    string Channel,
    string Rid,
    string? PackageId,
    IReadOnlyList<string> PackageIds,
    string? PackageUrl,
    string? PackageHash,
    string CacheRoot,
    string RuntimeRoot,
    string LaunchFile,
    IReadOnlyList<string> LaunchPrefixArgs,
    string PreflightResult,
    string GuidePath,
    string DocsRoot,
    string GuideHash,
    string? ExtractionBaseDirectory,
    string PreparationId,
    string? PackageHashUrl = null,
    LoomRuntimeFailureCategory? FailureCategory = null,
    IReadOnlyDictionary<string, string>? ToolEvidence = null);

public sealed record LoomGuideResult(string Version, string DocsRoot, string GuidePath, string GuideHash);

public sealed record LoomRuntimePackageValidationResult(
    string PackageId,
    string Version,
    string RuntimeIdentifier,
    string EntryPointName,
    string ManifestPath);

public sealed record LoomRuntimePackageLimits(
    long MaxArchiveBytes = 512L * 1024L * 1024L,
    long MaxEntryBytes = 512L * 1024L * 1024L,
    long MaxTotalUncompressedBytes = 512L * 1024L * 1024L,
    long MaxManifestBytes = 1L * 1024L * 1024L);

public sealed record LoomProcessResult(
    bool Started,
    int ExitCode,
    string StandardOutput,
    string StandardError);

public interface ILoomRuntimeProcessRunner
{
    Task<LoomProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan timeout,
        IDictionary<string, string>? environmentVariables = null,
        CancellationToken cancellationToken = default);
}

public abstract class LoomRuntimeException : Exception
{
    protected LoomRuntimeException(string message)
        : base(message)
    {
    }

    protected LoomRuntimeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public abstract LoomRuntimeFailureCategory FailureCategory { get; }
}

public sealed class LoomRuntimeAcquisitionException : LoomRuntimeException
{
    public override LoomRuntimeFailureCategory FailureCategory => LoomRuntimeFailureCategory.Acquisition;

    public LoomRuntimeAcquisitionException(string message)
        : base(message)
    {
    }

    public LoomRuntimeAcquisitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class LoomRuntimeIntegrityException : LoomRuntimeException
{
    public override LoomRuntimeFailureCategory FailureCategory => LoomRuntimeFailureCategory.Integrity;

    public LoomRuntimeIntegrityException(string message)
        : base(message)
    {
    }

    public LoomRuntimeIntegrityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class LoomRuntimeHostStartupException : LoomRuntimeException
{
    public override LoomRuntimeFailureCategory FailureCategory => LoomRuntimeFailureCategory.HostStartup;

    public LoomRuntimeHostStartupException(string message)
        : base(message)
    {
    }

    public LoomRuntimeHostStartupException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class LoomRuntimeGuideValidationException : LoomRuntimeException
{
    public override LoomRuntimeFailureCategory FailureCategory => LoomRuntimeFailureCategory.GuideValidation;

    public LoomRuntimeGuideValidationException(string message)
        : base(message)
    {
    }

    public LoomRuntimeGuideValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class LoomRuntimeCommandException : LoomRuntimeException
{
    public override LoomRuntimeFailureCategory FailureCategory => LoomRuntimeFailureCategory.Command;

    public LoomRuntimeCommandException(string message)
        : base(message)
    {
    }

    public LoomRuntimeCommandException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
