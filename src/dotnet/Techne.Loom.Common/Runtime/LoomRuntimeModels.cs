using System.Reflection;

namespace Techne.Loom.Common.Runtime;

public enum LoomRuntimeFailureCategory
{
    Integrity,
    HostStartup,
    GuideValidation,
    Command,
}
public sealed record LoomRuntimeIdentity(
    LoomRuntimeProduct Product,
    string Version,
    string RuntimeIdentifier,
    string LaunchFile)
{
    public string PackageId => LoomRuntimeCatalog.GetPackageId(Product, RuntimeIdentifier);

    public static LoomRuntimeIdentity FromCurrentProcess(LoomRuntimeProduct product, Assembly runtimeAssembly)
    {
        ArgumentNullException.ThrowIfNull(runtimeAssembly);
        var informationalVersion = runtimeAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = string.IsNullOrWhiteSpace(informationalVersion)
            ? runtimeAssembly.GetName().Version?.ToString(3)
            : informationalVersion.Split('+', 2)[0];
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new LoomRuntimeHostStartupException("The current runtime assembly has no exact version.");
        }

        var runtimeIdentifier = LoomRuntimeCatalog.DetectCurrentRuntimeIdentifier();
        var launchFile = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(launchFile))
        {
            throw new LoomRuntimeHostStartupException("The current self-contained apphost path is unavailable.");
        }

        return Create(product, version, runtimeIdentifier, launchFile);
    }

    public static LoomRuntimeIdentity Create(
        LoomRuntimeProduct product,
        string version,
        string runtimeIdentifier,
        string launchFile)
    {
        var normalizedVersion = LoomRuntimeCatalog.NormalizeVersion(version);
        LoomRuntimeCatalog.EnsureSupportedRuntimeIdentifier(runtimeIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(launchFile);
        var fullLaunchFile = Path.GetFullPath(launchFile);
        var expectedEntryPoint = LoomRuntimeCatalog.GetEntryFile(product, runtimeIdentifier);
        if (!string.Equals(Path.GetFileName(fullLaunchFile), expectedEntryPoint, StringComparison.OrdinalIgnoreCase))
        {
            throw new LoomRuntimeHostStartupException(
                $"The current process must be the self-contained '{expectedEntryPoint}' apphost for RID '{runtimeIdentifier}'.");
        }

        if (!File.Exists(fullLaunchFile))
        {
            throw new LoomRuntimeHostStartupException($"The self-contained apphost '{fullLaunchFile}' does not exist.");
        }

        return new LoomRuntimeIdentity(product, normalizedVersion, runtimeIdentifier, fullLaunchFile);
    }
}

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
