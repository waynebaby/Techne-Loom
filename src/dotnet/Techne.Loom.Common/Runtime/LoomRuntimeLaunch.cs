namespace Techne.Loom.Common.Runtime;

public sealed record LoomRuntimeLaunchCommand(
    string Command,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    string LaunchFile,
    string RuntimeVersion,
    string Rid,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null);

public static class LoomRuntimeLaunch
{
    public static LoomRuntimeLaunchCommand CreateMcpCommand(LoomRuntimeIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var launch = CreateCommand(identity, GetMcpCommandArguments(identity.Product));
        return launch with
        {
            EnvironmentVariables = McpRuntimeBindingPolicy.ToEnvironment(
                McpRuntimeBindingPolicy.FromIdentity(identity, launch)),
        };
    }

    public static IReadOnlyList<string> GetMcpCommandArguments(LoomRuntimeProduct product)
        => product switch
        {
            LoomRuntimeProduct.AgentOrchestrator => ["agent", "mcp", "stdio"],
            LoomRuntimeProduct.SkillOrchestrator => ["skill", "mcp", "stdio"],
            _ => throw new ArgumentOutOfRangeException(nameof(product), product, "Unsupported Loom runtime product."),
        };

    public static LoomRuntimeLaunchCommand CreateCommand(
        LoomRuntimeIdentity identity,
        IReadOnlyList<string> operationArguments)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(operationArguments);
        var validatedIdentity = LoomRuntimeIdentity.Create(
            identity.Product,
            identity.Version,
            identity.RuntimeIdentifier,
            identity.LaunchFile);
        var launchFile = validatedIdentity.LaunchFile;
        return new LoomRuntimeLaunchCommand(
            launchFile,
            operationArguments.ToArray(),
            Path.GetDirectoryName(launchFile)!,
            launchFile,
            validatedIdentity.Version,
            validatedIdentity.RuntimeIdentifier);
    }
}
