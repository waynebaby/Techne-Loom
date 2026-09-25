using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Techne.Loom.Common.Runtime;

public sealed record McpRuntimeBinding(
    string Product,
    string RuntimeVersion,
    string Rid,
    string LaunchCommand,
    string LaunchFile,
    string LaunchArgumentsSha256,
    string ExecutableSha256)
{
    public bool Matches(McpRuntimeBinding other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(Product, other.Product, StringComparison.Ordinal)
            && string.Equals(RuntimeVersion, other.RuntimeVersion, StringComparison.Ordinal)
            && string.Equals(Rid, other.Rid, StringComparison.Ordinal)
            && string.Equals(LaunchCommand, other.LaunchCommand, StringComparison.Ordinal)
            && PathEquals(LaunchFile, other.LaunchFile)
            && string.Equals(LaunchArgumentsSha256, other.LaunchArgumentsSha256, StringComparison.Ordinal)
            && string.Equals(ExecutableSha256, other.ExecutableSha256, StringComparison.Ordinal);
    }

    private static bool PathEquals(string left, string right)
        => string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}

public static class McpRuntimeBindingPolicy
{
    private const string EnvironmentPrefix = "TECHNE_LOOM_MCP_BINDING_";

    public static McpRuntimeBinding FromIdentity(LoomRuntimeIdentity identity, LoomRuntimeLaunchCommand launch)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(launch);
        if (launch.RuntimeVersion != identity.Version
            || launch.Rid != identity.RuntimeIdentifier
            || !PathEquals(launch.LaunchFile, identity.LaunchFile)
            || !PathEquals(launch.Command, identity.LaunchFile))
        {
            throw new LoomRuntimeIntegrityException("The MCP launch does not match the current apphost identity.");
        }

        return new McpRuntimeBinding(
            identity.Product.ToString(),
            identity.Version,
            identity.RuntimeIdentifier,
            launch.Command,
            Path.GetFullPath(launch.LaunchFile),
            ComputeLaunchArgumentsSha256(launch.Arguments),
            ComputeExecutableSha256(launch.LaunchFile));
    }

    public static string ComputeIdentitySha256(LoomRuntimeIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var canonical = JsonSerializer.Serialize(new
        {
            product = identity.Product.ToString(),
            version = identity.Version,
            rid = identity.RuntimeIdentifier,
            packageId = identity.PackageId,
            launchFile = identity.LaunchFile,
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static string ComputeLaunchArgumentsSha256(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var canonical = JsonSerializer.Serialize(arguments);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static string ComputeExecutableSha256(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        using var stream = File.OpenRead(executablePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static IReadOnlyDictionary<string, string> ToEnvironment(McpRuntimeBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EnvironmentPrefix + "REQUIRED"] = "true",
            [EnvironmentPrefix + "PRODUCT"] = binding.Product,
            [EnvironmentPrefix + "VERSION"] = binding.RuntimeVersion,
            [EnvironmentPrefix + "RID"] = binding.Rid,
            [EnvironmentPrefix + "LAUNCH_COMMAND"] = binding.LaunchCommand,
            [EnvironmentPrefix + "LAUNCH_FILE"] = binding.LaunchFile,
            [EnvironmentPrefix + "LAUNCH_ARGUMENTS_SHA256"] = binding.LaunchArgumentsSha256,
            [EnvironmentPrefix + "EXECUTABLE_SHA256"] = binding.ExecutableSha256,
        };
    }

    public static bool IsBindingRequired()
        => string.Equals(Environment.GetEnvironmentVariable(EnvironmentPrefix + "REQUIRED"), "true", StringComparison.OrdinalIgnoreCase);

    public static McpRuntimeBinding? TryReadEnvironment()
    {
        var values = new[]
        {
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "PRODUCT"),
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "VERSION"),
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "RID"),
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "LAUNCH_COMMAND"),
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "LAUNCH_FILE"),
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "LAUNCH_ARGUMENTS_SHA256"),
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "EXECUTABLE_SHA256"),
        };
        if (values.All(static value => value is null)) return null;
        if (values.Any(string.IsNullOrWhiteSpace)) throw new LoomRuntimeIntegrityException("The MCP runtime binding environment is incomplete.");
        return new McpRuntimeBinding(values[0]!, values[1]!, values[2]!, values[3]!, values[4]!, values[5]!, values[6]!);
    }

    public static void EnsureMatches(McpRuntimeBinding? boundBinding, LoomRuntimeIdentity requestedIdentity)
    {
        ArgumentNullException.ThrowIfNull(requestedIdentity);
        if (boundBinding is null) throw new LoomRuntimeIntegrityException("The MCP server is not bound to a verified apphost identity.");
        var expectedBinding = FromIdentity(requestedIdentity, LoomRuntimeLaunch.CreateMcpCommand(requestedIdentity));
        if (!boundBinding.Matches(expectedBinding))
        {
            throw new LoomRuntimeIntegrityException("The requested apphost identity does not match the MCP server process.");
        }
    }

    public static void ValidateServerIdentity(
        McpRuntimeBinding? binding,
        LoomRuntimeProduct expectedProduct,
        string expectedVersion,
        bool requireBinding)
    {
        if (binding is null)
        {
            if (requireBinding) throw new LoomRuntimeIntegrityException("The MCP server is not bound to a verified apphost identity.");
            return;
        }

        var currentProcess = Environment.ProcessPath;
        var currentRid = LoomRuntimeCatalog.DetectCurrentRuntimeIdentifier();
        var expectedEntryPoint = LoomRuntimeCatalog.GetEntryFile(expectedProduct, currentRid);
        if (string.IsNullOrWhiteSpace(currentProcess)
            || !string.Equals(Path.GetFileName(currentProcess), expectedEntryPoint, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(binding.Product, expectedProduct.ToString(), StringComparison.Ordinal)
            || !string.Equals(LoomRuntimeCatalog.NormalizeVersion(binding.RuntimeVersion), LoomRuntimeCatalog.NormalizeVersion(expectedVersion), StringComparison.Ordinal)
            || !string.Equals(binding.Rid, currentRid, StringComparison.Ordinal)
            || !PathEquals(binding.LaunchCommand, currentProcess)
            || !PathEquals(binding.LaunchFile, currentProcess)
            || !string.Equals(binding.LaunchArgumentsSha256, ComputeLaunchArgumentsSha256(["mcp", "stdio"]), StringComparison.Ordinal)
            || !IsSha256(binding.LaunchArgumentsSha256)
            || !IsSha256(binding.ExecutableSha256)
            || !string.Equals(ComputeExecutableSha256(currentProcess), binding.ExecutableSha256, StringComparison.Ordinal))
        {
            throw new LoomRuntimeIntegrityException("The MCP server binding does not match the running self-contained apphost.");
        }
    }

    private static bool PathEquals(string left, string right)
        => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool IsSha256(string value)
    {
        try
        {
            return Convert.FromHexString(value).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
