namespace Techne.Loom.Common.Runtime;

public sealed record LoomRuntimeLaunchCommand(
    string RuntimeMode,
    string Command,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    string LaunchFile,
    string RuntimeVersion,
    string Rid,
    string PreparationId);

public static class LoomRuntimeLaunch
{
    public static LoomRuntimeLaunchCommand CreateMcpCommand(LoomLaunchDescriptor descriptor)
        => CreateCommand(descriptor, ["mcp", "stdio"]);

    public static LoomRuntimeLaunchCommand CreateCommand(
        LoomLaunchDescriptor descriptor,
        IReadOnlyList<string> operationArguments)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(operationArguments);
        LoomPreparationDiagnostics.ValidateForMode(descriptor);
        if (!File.Exists(descriptor.LaunchFile))
        {
            throw new LoomRuntimeIntegrityException($"Runtime launch file '{descriptor.LaunchFile}' does not exist.");
        }

        if (descriptor.RuntimeMode == LoomRuntimeMode.SelfContained)
        {
            return new LoomRuntimeLaunchCommand(
                "self-contained",
                Path.GetFullPath(descriptor.LaunchFile),
                operationArguments.ToArray(),
                Path.GetFullPath(descriptor.RuntimeRoot),
                Path.GetFullPath(descriptor.LaunchFile),
                descriptor.ResolvedRuntimeVersion,
                descriptor.Rid,
                descriptor.PreparationId);
        }

        if (descriptor.RuntimeMode == LoomRuntimeMode.FrameworkDependent)
        {
            var arguments = descriptor.LaunchPrefixArgs
                .Concat([descriptor.LaunchFile])
                .Concat(operationArguments)
                .ToArray();
            return new LoomRuntimeLaunchCommand(
                "framework-dependent",
                ResolveDotnetHost(descriptor),
                arguments,
                Path.GetFullPath(descriptor.RuntimeRoot),
                Path.GetFullPath(descriptor.LaunchFile),
                descriptor.ResolvedRuntimeVersion,
                descriptor.Rid,
                descriptor.PreparationId);
        }

        throw new LoomRuntimeIntegrityException($"Unsupported runtime mode '{descriptor.RuntimeMode}'.");
    }

    private static string ResolveDotnetHost(LoomLaunchDescriptor descriptor)
    {
        if (descriptor.ToolEvidence?.TryGetValue("dotnet_host_path", out var recordedHost) == true
            && !string.IsNullOrWhiteSpace(recordedHost))
        {
            var fullHost = Path.GetFullPath(recordedHost);
            if (!File.Exists(fullHost))
            {
                throw new LoomRuntimeIntegrityException($"Recorded .NET host '{fullHost}' does not exist.");
            }

            return fullHost;
        }

        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath)
            && string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(processPath);
        }

        return "dotnet";
    }
}
