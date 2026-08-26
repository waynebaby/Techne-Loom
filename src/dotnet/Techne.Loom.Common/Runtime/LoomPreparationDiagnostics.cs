using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Techne.Loom.Common.Runtime;

public static class LoomPreparationDiagnostics
{
    private const string ExtractionEnvironmentVariable = "DOTNET_BUNDLE_EXTRACT_BASE_DIR";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    public static string CreatePreparationId(
        LoomRuntimeMode mode,
        LoomRuntimeProduct product,
        string version,
        string runtimeIdentifier,
        string runtimeRoot,
        string? packageHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeRoot);
        var identity = string.Join(
            "|",
            mode.ToString().ToLowerInvariant(),
            GetProductName(product),
            LoomRuntimeCatalog.NormalizeVersion(version),
            runtimeIdentifier,
            Path.GetFullPath(runtimeRoot),
            packageHash ?? string.Empty);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return "prep-" + Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    public static IDictionary<string, string> CreateSelfContainedLaunchEnvironment(string extractionBaseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extractionBaseDirectory);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ExtractionEnvironmentVariable] = Path.GetFullPath(extractionBaseDirectory),
        };
    }

    public static string ToJson(LoomLaunchDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["runtime_mode"] = descriptor.RuntimeMode == LoomRuntimeMode.SelfContained ? "self-contained" : "framework-dependent",
            ["product"] = GetProductName(descriptor.Product),
            ["resolved_runtime_version"] = descriptor.ResolvedRuntimeVersion,
            ["channel"] = descriptor.Channel,
            ["rid"] = descriptor.Rid,
            ["package_id"] = descriptor.PackageId,
            ["package_ids"] = descriptor.PackageIds,
            ["package_url"] = descriptor.PackageUrl,
            ["package_hash_url"] = descriptor.PackageHashUrl,
            ["package_hash"] = descriptor.PackageHash,
            ["cache_root"] = descriptor.CacheRoot,
            ["runtime_root"] = descriptor.RuntimeRoot,
            ["launch_file"] = descriptor.LaunchFile,
            ["launch_prefix_args"] = descriptor.LaunchPrefixArgs,
            ["preflight_result"] = descriptor.PreflightResult,
            ["guide_path"] = descriptor.GuidePath,
            ["docs_root"] = descriptor.DocsRoot,
            ["guide_hash"] = descriptor.GuideHash,
            ["extraction_base_directory"] = descriptor.ExtractionBaseDirectory,
            ["preparation_id"] = descriptor.PreparationId,
            ["failure_category"] = descriptor.FailureCategory?.ToString().ToLowerInvariant(),
            ["tool_evidence"] = descriptor.ToolEvidence,
        };
        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    public static void ValidateForMode(LoomLaunchDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (string.IsNullOrWhiteSpace(descriptor.ResolvedRuntimeVersion))
        {
            throw new LoomRuntimeIntegrityException("Preparation descriptor is missing the resolved runtime version.");
        }

        try
        {
            LoomRuntimeCatalog.NormalizeVersion(descriptor.ResolvedRuntimeVersion);
        }
        catch (FormatException exception)
        {
            throw new LoomRuntimeIntegrityException($"Preparation descriptor carries an invalid exact version '{descriptor.ResolvedRuntimeVersion}'.", exception);
        }

        try
        {
            LoomRuntimeCatalog.EnsureSupportedRuntimeIdentifier(descriptor.Rid);
        }
        catch (PlatformNotSupportedException exception)
        {
            throw new LoomRuntimeIntegrityException($"Preparation descriptor carries an unsupported runtime identifier '{descriptor.Rid}'.", exception);
        }

        if (!Path.IsPathFullyQualified(descriptor.CacheRoot))
        {
            throw new LoomRuntimeIntegrityException("Preparation descriptor cache root must be an absolute path.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.RuntimeRoot) || !Path.IsPathFullyQualified(descriptor.RuntimeRoot))
        {
            throw new LoomRuntimeIntegrityException("Preparation descriptor runtime root must be a non-empty absolute path.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.LaunchFile) || !Path.IsPathFullyQualified(descriptor.LaunchFile))
        {
            throw new LoomRuntimeIntegrityException("Preparation descriptor launch file must be a non-empty absolute path.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.PreflightResult))
        {
            throw new LoomRuntimeIntegrityException("Preparation descriptor is missing its preflight result evidence.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.GuidePath) || !Path.IsPathFullyQualified(descriptor.GuidePath))
        {
            throw new LoomRuntimeIntegrityException("Preparation descriptor guide path must be a non-empty absolute path.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.DocsRoot) || !Path.IsPathFullyQualified(descriptor.DocsRoot))
        {
            throw new LoomRuntimeIntegrityException("Preparation descriptor docs root must be a non-empty absolute path.");
        }

        if (!IsPathWithinRoot(descriptor.DocsRoot, descriptor.GuidePath))
        {
            throw new LoomRuntimeIntegrityException("Preparation descriptor guide path must remain inside its docs root.");
        }

        ValidateSha512Base64(descriptor.GuideHash, "guide hash");

        if (string.IsNullOrWhiteSpace(descriptor.PreparationId) || !descriptor.PreparationId.StartsWith("prep-", StringComparison.Ordinal))
        {
            throw new LoomRuntimeIntegrityException("Preparation descriptor is missing a valid preparation id.");
        }

        switch (descriptor.RuntimeMode)
        {
            case LoomRuntimeMode.SelfContained:
                ValidateSelfContained(descriptor);
                break;
            case LoomRuntimeMode.FrameworkDependent:
                ValidateFrameworkDependent(descriptor);
                break;
            default:
                throw new LoomRuntimeIntegrityException($"Preparation descriptor carries an unsupported runtime mode '{descriptor.RuntimeMode}'.");
        }
    }

    private static void ValidateSelfContained(LoomLaunchDescriptor descriptor)
    {
        var expectedPackageId = LoomRuntimeCatalog.GetPackageId(descriptor.Product, descriptor.Rid);
        if (!string.Equals(descriptor.PackageId, expectedPackageId, StringComparison.Ordinal))
        {
            throw new LoomRuntimeIntegrityException($"Self-contained preparation descriptor package id must be '{expectedPackageId}'.");
        }

        if (descriptor.PackageIds.Count != 1 || !string.Equals(descriptor.PackageIds[0], expectedPackageId, StringComparison.Ordinal))
        {
            throw new LoomRuntimeIntegrityException("Self-contained preparation descriptor must reference exactly its single RID runtime package.");
        }

        ValidateSha512Base64(descriptor.PackageHash, "package hash");
        ValidateAbsoluteUrl(descriptor.PackageHashUrl, "package hash URL");

        if (string.IsNullOrWhiteSpace(descriptor.ExtractionBaseDirectory) || !Path.IsPathFullyQualified(descriptor.ExtractionBaseDirectory))
        {
            throw new LoomRuntimeIntegrityException("Self-contained preparation descriptor must carry an absolute extraction base directory.");
        }

        if (descriptor.LaunchPrefixArgs.Count != 0)
        {
            throw new LoomRuntimeIntegrityException("Self-contained preparation descriptor must not carry launch prefix arguments.");
        }

        var expectedEntryFile = LoomRuntimeCatalog.GetEntryFile(descriptor.Product, descriptor.Rid);
        if (!string.Equals(Path.GetFileName(descriptor.LaunchFile), expectedEntryFile, StringComparison.Ordinal))
        {
            throw new LoomRuntimeIntegrityException($"Self-contained preparation descriptor launch file must be named '{expectedEntryFile}'.");
        }
    }

    private static void ValidateFrameworkDependent(LoomLaunchDescriptor descriptor)
    {
        var expectedPackageIds = new[]
        {
            LoomRuntimeCatalog.GetProductPackageId(descriptor.Product),
            "Techne.Loom.Common",
            "Techne.Loom.Abstractions",
        };
        if (!descriptor.PackageIds.SequenceEqual(expectedPackageIds, StringComparer.Ordinal))
        {
            throw new LoomRuntimeIntegrityException(".NET CLI mode preparation descriptor must reference the exact .NET runtime bundle.");
        }

        if (descriptor.ExtractionBaseDirectory is not null)
        {
            throw new LoomRuntimeIntegrityException("Framework-dependent preparation descriptor must not carry an extraction base directory.");
        }

        if (descriptor.LaunchPrefixArgs.Count == 0 || !string.Equals(descriptor.LaunchPrefixArgs[0], "exec", StringComparison.Ordinal))
        {
            throw new LoomRuntimeIntegrityException("Framework-dependent preparation descriptor must launch through 'dotnet exec'.");
        }

        if (!descriptor.LaunchPrefixArgs.Contains("--runtimeconfig", StringComparer.Ordinal))
        {
            throw new LoomRuntimeIntegrityException("Framework-dependent preparation descriptor must bind an explicit --runtimeconfig file.");
        }

        if (!descriptor.LaunchFile.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new LoomRuntimeIntegrityException("Framework-dependent preparation descriptor launch file must be a managed DLL entry point.");
        }
    }

    private static void ValidateAbsoluteUrl(string? value, string fieldName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
        {
            throw new LoomRuntimeIntegrityException($"Preparation descriptor {fieldName} must be an absolute HTTP URL.");
        }
    }

    private static bool IsPathWithinRoot(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateSha512Base64(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new LoomRuntimeIntegrityException($"Preparation descriptor is missing its {fieldName}.");
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(value);
        }
        catch (FormatException exception)
        {
            throw new LoomRuntimeIntegrityException($"Preparation descriptor {fieldName} is not valid base64.", exception);
        }

        if (decoded.Length != SHA512.HashSizeInBytes)
        {
            throw new LoomRuntimeIntegrityException($"Preparation descriptor {fieldName} does not contain a SHA-512 digest.");
        }
    }

    private static string GetProductName(LoomRuntimeProduct product)
        => product switch
        {
            LoomRuntimeProduct.AgentOrchestrator => "ao",
            LoomRuntimeProduct.SkillOrchestrator => "so",
            _ => throw new ArgumentOutOfRangeException(nameof(product), product, "Unsupported Loom runtime product."),
        };
}
