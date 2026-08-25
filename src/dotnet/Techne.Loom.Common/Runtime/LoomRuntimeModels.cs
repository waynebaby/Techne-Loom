namespace Techne.Loom.Common.Runtime;

public sealed class LoomRuntimeResolutionRequest
{
    public LoomRuntimeProduct Product { get; init; }
    public required string Version { get; init; }
    public string Channel { get; init; } = "released";
    public string? FrameworkBundleDirectory { get; init; }
    public string? CacheRoot { get; init; }
    public string? RuntimeIdentifier { get; init; }
    public bool ForceSelfContained { get; init; }
    public TimeSpan LockTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan GuideTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

public sealed record LoomLaunchDescriptor(
    LoomRuntimeMode RuntimeMode,
    string ResolvedRuntimeVersion,
    string Rid,
    string? PackageId,
    IReadOnlyList<string> PackageIds,
    string? PackageUrl,
    string? PackageHash,
    string CacheRoot,
    string LaunchFile,
    IReadOnlyList<string> LaunchPrefixArgs,
    string PreflightResult,
    string GuidePath);

public sealed record LoomGuideResult(string Version, string DocsRoot, string GuidePath);

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
        CancellationToken cancellationToken = default);
}

public class LoomRuntimeException : Exception
{
    public LoomRuntimeException(string message)
        : base(message)
    {
    }

    public LoomRuntimeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class LoomRuntimeAcquisitionException : LoomRuntimeException
{
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
    public LoomRuntimeIntegrityException(string message)
        : base(message)
    {
    }
}

public sealed class LoomRuntimeHostStartupException : LoomRuntimeException
{
    public LoomRuntimeHostStartupException(string message)
        : base(message)
    {
    }
}

public sealed class LoomRuntimeGuideValidationException : LoomRuntimeException
{
    public LoomRuntimeGuideValidationException(string message)
        : base(message)
    {
    }
}

public sealed class LoomRuntimeCommandException : LoomRuntimeException
{
    public LoomRuntimeCommandException(string message)
        : base(message)
    {
    }
}
