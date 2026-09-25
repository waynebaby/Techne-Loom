using System.Reflection;
using Techne.Loom.Common.Mcp;
using Techne.Loom.Common.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpSessionRegistryTests
{
    [Fact]
    public void IsSameVersion_UsesOnlyNormalizedServerInfoVersion()
    {
        var serverInfo = new McpServerInfo("loom-so-0.3.282", "0.3.282", "2025-06-18");
        Assert.True(McpSessionVersionPolicy.IsSameVersion("0.3.282", serverInfo));
        Assert.True(McpSessionVersionPolicy.IsSameVersion("0.3.282-BETA", serverInfo with { Version = "0.3.282-beta" }));
        Assert.False(McpSessionVersionPolicy.IsSameVersion("0.3.283", serverInfo));
    }

    [Fact]
    public void IsReusable_RejectsAnyChangedAppHostIdentityField()
    {
        var assembly = typeof(DefaultWorkflowTaskTrackingService).Assembly.Location;
        var runtimeRoot = Path.GetDirectoryName(assembly)!;
        var launch = new LoomRuntimeLaunchCommand(
            "dotnet",
            [assembly, "mcp", "stdio"],
            runtimeRoot,
            assembly,
            "0.1.0",
            "win-x64");
        var registration = new McpSessionRegistration(
            "loom-so-0.1.0",
            "0.1.0",
            new McpServerInfo("loom-so-0.1.0", "0.1.0", "2025-06-18"),
            "0.1.0",
            "win-x64",
            "dotnet",
            null,
            null,
            DateTimeOffset.UtcNow,
            "healthy")
        {
            LaunchFile = Path.GetFullPath(launch.LaunchFile),
            LaunchArgumentsSha256 = McpRuntimeBindingPolicy.ComputeLaunchArgumentsSha256(launch.Arguments),
            ExecutableSha256 = McpRuntimeBindingPolicy.ComputeExecutableSha256(launch.LaunchFile),
        };

        Assert.True(McpSessionVersionPolicy.IsReusable("0.1.0", launch, registration));
        Assert.All(
            new[]
            {
                launch with { RuntimeVersion = "0.1.1" },
                launch with { Rid = "linux-x64" },
                launch with { Command = "other-dotnet" },
                launch with { LaunchFile = Path.Combine(runtimeRoot, "other.dll") },
                launch with { Arguments = [assembly, "mcp", "other"] },
            },
            changedLaunch => Assert.False(McpSessionVersionPolicy.IsReusable("0.1.0", changedLaunch, registration)));
        Assert.False(McpSessionVersionPolicy.IsReusable(
            "0.1.0",
            launch,
            registration with { ExecutableSha256 = new string('f', 64) }));
    }

    [Fact]
    public async Task ConcurrentRegisterAsync_SerializesSharedSessionLifecycle()
    {
        var assembly = typeof(DefaultWorkflowTaskTrackingService).Assembly;
        var assemblyPath = assembly.Location;
        var runtimeRoot = Path.GetDirectoryName(assemblyPath)!;
        var runtimeVersion = GetRuntimeVersion(assembly);
        var launch = new LoomRuntimeLaunchCommand(
            "dotnet",
            [assemblyPath, "mcp", "stdio"],
            runtimeRoot,
            assemblyPath,
            runtimeVersion,
            "win-x64");
        await using var registry = new McpSessionRegistry();

        var results = await Task.WhenAll(
            registry.RegisterAsync($"loom-so-{runtimeVersion}", runtimeVersion, launch, "so_inspect_workflow_fragment"),
            registry.RegisterAsync($"loom-so-{runtimeVersion}", runtimeVersion, launch, "so_inspect_workflow_fragment"));

        Assert.Contains(results, static result => result.Status == "created");
        Assert.Contains(results, static result => result.Status == "reused");
        Assert.True(registry.TryGet($"loom-so-{runtimeVersion}", out var registration));
        Assert.Equal("healthy", registration!.HealthStatus);
    }

    [Fact]
    public async Task RegisterAsync_ReusesSameAppHostWhenConfigurationPathChanges()
    {
        var assembly = typeof(DefaultWorkflowTaskTrackingService).Assembly;
        var assemblyPath = assembly.Location;
        var runtimeRoot = Path.GetDirectoryName(assemblyPath)!;
        var runtimeVersion = GetRuntimeVersion(assembly);
        var launch = new LoomRuntimeLaunchCommand(
            "dotnet",
            [assemblyPath, "mcp", "stdio"],
            runtimeRoot,
            assemblyPath,
            runtimeVersion,
            "win-x64");
        await using var registry = new McpSessionRegistry();

        var first = await registry.RegisterAsync(
            $"loom-so-{runtimeVersion}", runtimeVersion, launch, "so_inspect_workflow_fragment",
            configurationFile: "C:\\one\\mcp.json", configurationSha256: "hash-one");
        var second = await registry.RegisterAsync(
            $"loom-so-{runtimeVersion}", runtimeVersion, launch, "so_inspect_workflow_fragment",
            configurationFile: "C:\\two\\mcp.json", configurationSha256: "hash-two");

        Assert.Equal("created", first.Status);
        Assert.Equal("reused", second.Status);
        Assert.Equal(runtimeVersion, second.Registration.ServerInfo.Version);
        Assert.Equal("healthy", second.Registration.HealthStatus);
        Assert.Equal("C:\\one\\mcp.json", second.Registration.ConfigurationFile);
    }

    private static string GetRuntimeVersion(Assembly assembly)
    {
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?.Split('+', 2)[0]
            .Trim();
        return LoomRuntimeCatalog.NormalizeVersion(
            string.IsNullOrWhiteSpace(informationalVersion)
                ? assembly.GetName().Version?.ToString(3) ?? throw new InvalidOperationException("The SO test assembly has no version.")
                : informationalVersion);
    }
}