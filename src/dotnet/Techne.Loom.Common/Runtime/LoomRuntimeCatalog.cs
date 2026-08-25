using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Techne.Loom.Common.Runtime;

public enum LoomRuntimeProduct
{
    AgentOrchestrator,
    SkillOrchestrator,
}

public enum LoomRuntimeMode
{
    FrameworkDependent,
    SelfContained,
}

public static class LoomRuntimeCatalog
{
    private static readonly Regex VersionPattern = new(
        "^(?<major>[0-9]+)\\.(?<minor>[0-9]+)\\.(?<patch>[0-9]+)(?<prerelease>-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<string> SupportedRuntimeIdentifiers { get; } =
    [
        "win-x64",
        "win-arm64",
        "linux-x64",
        "linux-arm64",
        "linux-musl-x64",
        "linux-musl-arm64",
        "osx-x64",
        "osx-arm64",
    ];

    public static string GetPackageId(LoomRuntimeProduct product, string runtimeIdentifier)
    {
        EnsureSupportedRuntimeIdentifier(runtimeIdentifier);
        return $"{GetProductPackagePrefix(product)}.Runtime.{runtimeIdentifier}";
    }

    public static string GetProductPackageId(LoomRuntimeProduct product)
        => GetProductPackagePrefix(product);

    public static string GetEntryPoint(LoomRuntimeProduct product)
        => product switch
        {
            LoomRuntimeProduct.AgentOrchestrator => "ao",
            LoomRuntimeProduct.SkillOrchestrator => "so",
            _ => throw new ArgumentOutOfRangeException(nameof(product), product, "Unsupported Loom runtime product."),
        };

    public static string GetEntryFile(LoomRuntimeProduct product, string runtimeIdentifier)
    {
        EnsureSupportedRuntimeIdentifier(runtimeIdentifier);
        var suffix = runtimeIdentifier.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : string.Empty;
        return GetEntryPoint(product) + suffix;
    }

    public static string NormalizeVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("A non-empty exact package version is required.", nameof(version));
        }

        var match = VersionPattern.Match(version.Trim());
        if (!match.Success)
        {
            throw new FormatException($"'{version}' is not a supported exact NuGet version. Expected <major>.<minor>.<patch>[-prerelease].");
        }

        var major = NormalizeNumericVersionPart(match.Groups["major"].Value);
        var minor = NormalizeNumericVersionPart(match.Groups["minor"].Value);
        var patch = NormalizeNumericVersionPart(match.Groups["patch"].Value);
        return $"{major}.{minor}.{patch}{match.Groups["prerelease"].Value.ToLowerInvariant()}";
    }

    private static string NormalizeNumericVersionPart(string value)
    {
        var normalized = value.TrimStart('0');
        return normalized.Length == 0 ? "0" : normalized;
    }

    public static string GetNuGetPackageUrl(string packageId, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        var normalizedVersion = NormalizeVersion(version);
        var normalizedPackageId = packageId.ToLowerInvariant();
        return $"https://api.nuget.org/v3-flatcontainer/{normalizedPackageId}/{normalizedVersion}/{normalizedPackageId}.{normalizedVersion}.nupkg";
    }

    public static string GetNuGetHashUrl(string packageId, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        var normalizedVersion = NormalizeVersion(version);
        var normalizedPackageId = packageId.ToLowerInvariant();
        return $"https://api.nuget.org/v3-flatcontainer/{normalizedPackageId}/{normalizedVersion}/{normalizedPackageId}.{normalizedVersion}.nupkg.sha512";
    }

    public static string GetGitHubPackageUrl(string packageId, string version, string channel, bool latestAlias = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        var normalizedVersion = NormalizeVersion(version);
        var assetName = latestAlias ? $"{packageId}.latest.nupkg" : $"{packageId}.{normalizedVersion}.nupkg";
        return $"https://github.com/waynebaby/Techne-Loom/releases/download/nuget-{channel}-latest/{assetName}";
    }

    public static string DetectCurrentRuntimeIdentifier()
    {
        var isMusl = OperatingSystem.IsLinux() &&
                     (RuntimeInformation.RuntimeIdentifier.Contains("musl", StringComparison.OrdinalIgnoreCase) || File.Exists("/etc/alpine-release"));
        return DetectRuntimeIdentifier(
            OperatingSystem.IsWindows(),
            OperatingSystem.IsLinux(),
            OperatingSystem.IsMacOS(),
            RuntimeInformation.ProcessArchitecture,
            isMusl);
    }

    public static string DetectRuntimeIdentifier(bool isWindows, bool isLinux, bool isMacOS, Architecture architecture, bool isMusl)
    {
        var operatingSystem = isWindows ? "win" : isLinux ? "linux" : isMacOS ? "osx" : "unsupported";
        var architectureName = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => "unsupported",
        };

        if (operatingSystem == "unsupported" || architectureName == "unsupported" || (operatingSystem != "linux" && isMusl))
        {
            throw new PlatformNotSupportedException($"Unsupported Loom runtime platform: os={operatingSystem}, architecture={architecture}, musl={isMusl}. Supported runtime identifiers: {string.Join(", ", SupportedRuntimeIdentifiers)}.");
        }

        var runtimeIdentifier = operatingSystem == "linux" && isMusl
            ? $"linux-musl-{architectureName}"
            : $"{operatingSystem}-{architectureName}";
        EnsureSupportedRuntimeIdentifier(runtimeIdentifier);
        return runtimeIdentifier;
    }

    public static void EnsureSupportedRuntimeIdentifier(string runtimeIdentifier)
    {
        if (string.IsNullOrWhiteSpace(runtimeIdentifier) ||
            !SupportedRuntimeIdentifiers.Contains(runtimeIdentifier, StringComparer.Ordinal))
        {
            throw new PlatformNotSupportedException($"Unsupported Loom runtime identifier '{runtimeIdentifier}'. Supported runtime identifiers: {string.Join(", ", SupportedRuntimeIdentifiers)}.");
        }
    }

    private static string GetProductPackagePrefix(LoomRuntimeProduct product)
        => product switch
        {
            LoomRuntimeProduct.AgentOrchestrator => "Techne.Loom.AgentOrchestrator",
            LoomRuntimeProduct.SkillOrchestrator => "Techne.Loom.SkillOrchestrator",
            _ => throw new ArgumentOutOfRangeException(nameof(product), product, "Unsupported Loom runtime product."),
        };
}
