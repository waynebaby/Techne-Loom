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
    public void IsReusable_RejectsAnyChangedRuntimeIdentityField()
    {
        var assembly = typeof(DefaultWorkflowTaskTrackingService).Assembly.Location;
        var runtimeRoot = Path.GetDirectoryName(assembly)!;
        var launch = new LoomRuntimeLaunchCommand(
            "framework-dependent",
            "dotnet",
            [assembly, "mcp", "stdio"],
            runtimeRoot,
            assembly,
            "0.1.0",
            "win-x64",
            "registry-identity-test");
        var registration = new McpSessionRegistration(
            "loom-so-0.1.0",
            "0.1.0",
            new McpServerInfo("loom-so-0.1.0", "0.1.0", "2025-06-18"),
            launch.RuntimeMode,
            launch.Rid,
            launch.PreparationId,
            launch.Command,
            null,
            null,
            null,
            "descriptor-one",
            DateTimeOffset.UtcNow,
            "healthy")
        {
            LaunchFile = Path.GetFullPath(launch.LaunchFile),
            LaunchArgumentsSha256 = McpRuntimeBindingPolicy.ComputeLaunchArgumentsSha256(launch.Arguments),
        };

        Assert.True(McpSessionVersionPolicy.IsReusable("0.1.0", launch, registration, "descriptor-one"));
        Assert.All(
            new[]
            {
                launch with { RuntimeMode = "self-contained" },
                launch with { Rid = "linux-x64" },
                launch with { PreparationId = "other-preparation" },
                launch with { Command = "other-dotnet" },
                launch with { LaunchFile = Path.Combine(runtimeRoot, "other.dll") },
                launch with { Arguments = [assembly, "mcp", "other"] },
            },
            changedLaunch => Assert.False(McpSessionVersionPolicy.IsReusable("0.1.0", changedLaunch, registration, "descriptor-one")));
        Assert.False(McpSessionVersionPolicy.IsReusable("0.1.0", launch, registration, "descriptor-two"));
    }

    [Fact]
    public async Task ConcurrentRegisterAsync_SerializesSharedSessionLifecycle()
    {
        var assembly = typeof(DefaultWorkflowTaskTrackingService).Assembly;
        var assemblyPath = assembly.Location;
        var runtimeRoot = Path.GetDirectoryName(assemblyPath)!;
        var runtimeVersion = GetRuntimeVersion(assembly);
        var launch = new LoomRuntimeLaunchCommand(
            "framework-dependent",
            "dotnet",
            [assemblyPath, "mcp", "stdio"],
            runtimeRoot,
            assemblyPath,
            runtimeVersion,
            "win-x64",
            "registry-concurrency-test");
        await using var registry = new McpSessionRegistry();

        var results = await Task.WhenAll(
            registry.RegisterAsync($"loom-so-{runtimeVersion}", runtimeVersion, launch, "so_inspect_workflow_fragment", "descriptor-one"),
            registry.RegisterAsync($"loom-so-{runtimeVersion}", runtimeVersion, launch, "so_inspect_workflow_fragment", "descriptor-one"));

        Assert.Contains(results, static result => result.Status == "created");
        Assert.Contains(results, static result => result.Status == "reused");
        Assert.True(registry.TryGet($"loom-so-{runtimeVersion}", out var registration));
        Assert.Equal("healthy", registration!.HealthStatus);
    }
    [Fact]
    public async Task RegisterAsync_ReplacesHealthySessionWhenDescriptorHashChanges()
    {
        var assembly = typeof(DefaultWorkflowTaskTrackingService).Assembly;
        var assemblyPath = assembly.Location;
        var runtimeRoot = Path.GetDirectoryName(assemblyPath)!;
        var runtimeVersion = GetRuntimeVersion(assembly);
        var launch = new LoomRuntimeLaunchCommand(
            "framework-dependent",
            "dotnet",
            [assemblyPath, "mcp", "stdio"],
            runtimeRoot,
            assemblyPath,
            runtimeVersion,
            "win-x64",
            "registry-test");
        await using var registry = new McpSessionRegistry();

        var first = await registry.RegisterAsync(
            $"loom-so-{runtimeVersion}",
            runtimeVersion,
            launch,
            "so_inspect_workflow_fragment",
            configurationFile: "C:\\one\\mcp.json",
            configurationSha256: "hash-one",
            runtimeDescriptorFile: "C:\\one\\descriptor.json",
            runtimeDescriptorSha256: "descriptor-one");
        var second = await registry.RegisterAsync(
            $"loom-so-{runtimeVersion}",
            runtimeVersion,
            launch,
            "so_inspect_workflow_fragment",
            configurationFile: "C:\\two\\mcp.json",
            configurationSha256: "hash-two",
            runtimeDescriptorFile: "C:\\two\\descriptor.json",
            runtimeDescriptorSha256: "descriptor-two");

        Assert.Equal("created", first.Status);
        Assert.Equal("stale-replaced", second.Status);
        Assert.Equal(runtimeVersion, second.Registration.ServerInfo.Version);
        Assert.Equal("healthy", second.Registration.HealthStatus);
        Assert.Equal("C:\\two\\mcp.json", second.Registration.ConfigurationFile);
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