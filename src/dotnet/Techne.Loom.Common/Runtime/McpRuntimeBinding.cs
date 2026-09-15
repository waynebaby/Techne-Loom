using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Techne.Loom.Common.Runtime;

public sealed record McpRuntimeBinding(
    string Product,
    string RuntimeVersion,
    string RuntimeMode,
    string Rid,
    string PreparationId,
    string LaunchCommand,
    string LaunchFile,
    string LaunchArgumentsSha256,
    string DescriptorSha256)
{
    public bool Matches(McpRuntimeBinding other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(Product, other.Product, StringComparison.Ordinal)
            && string.Equals(RuntimeVersion, other.RuntimeVersion, StringComparison.Ordinal)
            && string.Equals(RuntimeMode, other.RuntimeMode, StringComparison.Ordinal)
            && string.Equals(Rid, other.Rid, StringComparison.Ordinal)
            && string.Equals(PreparationId, other.PreparationId, StringComparison.Ordinal)
            && string.Equals(LaunchCommand, other.LaunchCommand, StringComparison.Ordinal)
            && string.Equals(LaunchFile, other.LaunchFile, StringComparison.Ordinal)
            && string.Equals(LaunchArgumentsSha256, other.LaunchArgumentsSha256, StringComparison.Ordinal)
            && string.Equals(DescriptorSha256, other.DescriptorSha256, StringComparison.Ordinal);
    }
}

public static class McpRuntimeBindingPolicy
{
    private const string EnvironmentPrefix = "TECHNE_LOOM_MCP_BINDING_";

    public static McpRuntimeBinding FromDescriptor(LoomLaunchDescriptor descriptor, LoomRuntimeLaunchCommand launch)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(launch);
        return new McpRuntimeBinding(
            descriptor.Product.ToString(),
            LoomRuntimeCatalog.NormalizeVersion(descriptor.ResolvedRuntimeVersion),
            descriptor.RuntimeMode.ToString(),
            descriptor.Rid,
            descriptor.PreparationId,
            launch.Command,
            Path.GetFullPath(launch.LaunchFile),
            ComputeLaunchArgumentsSha256(launch.Arguments),
            ComputeDescriptorSha256(descriptor));
    }

    public static string ComputeDescriptorSha256(LoomLaunchDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var canonical = LoomPreparationDiagnostics.ToJson(descriptor, indented: false);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static string ComputeLaunchArgumentsSha256(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var canonical = JsonSerializer.Serialize(arguments);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static IReadOnlyDictionary<string, string> ToEnvironment(McpRuntimeBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EnvironmentPrefix + "REQUIRED"] = "true",
            [EnvironmentPrefix + "PRODUCT"] = binding.Product,
            [EnvironmentPrefix + "VERSION"] = binding.RuntimeVersion,
            [EnvironmentPrefix + "MODE"] = binding.RuntimeMode,
            [EnvironmentPrefix + "RID"] = binding.Rid,
            [EnvironmentPrefix + "PREPARATION_ID"] = binding.PreparationId,
            [EnvironmentPrefix + "LAUNCH_COMMAND"] = binding.LaunchCommand,
            [EnvironmentPrefix + "LAUNCH_FILE"] = binding.LaunchFile,
            [EnvironmentPrefix + "LAUNCH_ARGUMENTS_SHA256"] = binding.LaunchArgumentsSha256,
            [EnvironmentPrefix + "DESCRIPTOR_SHA256"] = binding.DescriptorSha256,
        };
    }

    public static bool IsBindingRequired()
        => string.Equals(
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "REQUIRED"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public static McpRuntimeBinding? TryReadEnvironment()
    {
        var values = new[]
        {
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "PRODUCT"),
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "VERSION"),
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "MODE"),
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "RID"),
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "PREPARATION_ID"),
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "LAUNCH_COMMAND"),
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "LAUNCH_FILE"),
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "LAUNCH_ARGUMENTS_SHA256"),
            Environment.GetEnvironmentVariable(EnvironmentPrefix + "DESCRIPTOR_SHA256"),
        };
        if (values.All(static value => value is null))
        {
            return null;
        }

        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new LoomRuntimeIntegrityException("The MCP runtime binding environment is incomplete.");
        }

        return new McpRuntimeBinding(
            values[0]!, values[1]!, values[2]!, values[3]!, values[4]!,
            values[5]!, values[6]!, values[7]!, values[8]!);
    }

    public static void EnsureMatches(McpRuntimeBinding? boundBinding, LoomLaunchDescriptor requestedDescriptor)
    {
        ArgumentNullException.ThrowIfNull(requestedDescriptor);
        if (boundBinding is null)
        {
            throw new LoomRuntimeIntegrityException("The MCP server is not bound to a verified runtime descriptor.");
        }

        var expectedLaunch = LoomRuntimeLaunch.CreateCommand(requestedDescriptor, ["mcp", "stdio"]);
        var expectedBinding = FromDescriptor(requestedDescriptor, expectedLaunch);
        if (!boundBinding.Matches(expectedBinding))
        {
            throw new LoomRuntimeIntegrityException("The requested runtime descriptor does not match the descriptor used to start this MCP server.");
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
            if (requireBinding)
            {
                throw new LoomRuntimeIntegrityException("The MCP server is not bound to a verified runtime descriptor.");
            }

            return;
        }

        var validMode = binding.RuntimeMode is nameof(LoomRuntimeMode.FrameworkDependent) or nameof(LoomRuntimeMode.SelfContained);
        var validRid = LoomRuntimeCatalog.SupportedRuntimeIdentifiers.Contains(binding.Rid, StringComparer.Ordinal);
        if (!string.Equals(binding.Product, expectedProduct.ToString(), StringComparison.Ordinal)
            || !string.Equals(
                LoomRuntimeCatalog.NormalizeVersion(binding.RuntimeVersion),
                LoomRuntimeCatalog.NormalizeVersion(expectedVersion),
                StringComparison.Ordinal)
            || !validMode
            || !validRid
            || string.IsNullOrWhiteSpace(binding.PreparationId)
            || string.IsNullOrWhiteSpace(binding.LaunchCommand)
            || !File.Exists(binding.LaunchFile)
            || !IsSha256(binding.LaunchArgumentsSha256)
            || !IsSha256(binding.DescriptorSha256))
        {
            throw new LoomRuntimeIntegrityException("The MCP server binding does not match the running runtime.");
        }

        static bool IsSha256(string value)
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
}
