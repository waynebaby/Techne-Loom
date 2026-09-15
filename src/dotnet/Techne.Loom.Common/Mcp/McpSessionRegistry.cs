using System.Text.Json;
using Techne.Loom.Common.Runtime;

namespace Techne.Loom.Common.Mcp;

public static class McpSessionVersionPolicy
{
    public static bool IsSameVersion(string requestedVersion, McpServerInfo serverInfo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedVersion);
        ArgumentNullException.ThrowIfNull(serverInfo);
        return string.Equals(
            LoomRuntimeCatalog.NormalizeVersion(requestedVersion),
            LoomRuntimeCatalog.NormalizeVersion(serverInfo.Version),
            StringComparison.Ordinal);
    }

    public static bool IsReusable(
        string requestedVersion,
        LoomRuntimeLaunchCommand launch,
        McpSessionRegistration registration,
        string? runtimeDescriptorSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedVersion);
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(registration);
        return IsSameVersion(requestedVersion, registration.ServerInfo)
            && string.Equals(registration.RuntimeMode, launch.RuntimeMode, StringComparison.Ordinal)
            && string.Equals(registration.Rid, launch.Rid, StringComparison.Ordinal)
            && string.Equals(registration.PreparationId, launch.PreparationId, StringComparison.Ordinal)
            && string.Equals(registration.LaunchCommand, launch.Command, StringComparison.Ordinal)
            && string.Equals(registration.LaunchFile, Path.GetFullPath(launch.LaunchFile), StringComparison.Ordinal)
            && string.Equals(
                registration.LaunchArgumentsSha256,
                McpRuntimeBindingPolicy.ComputeLaunchArgumentsSha256(launch.Arguments),
                StringComparison.Ordinal)
            && string.Equals(registration.RuntimeDescriptorSha256, runtimeDescriptorSha256, StringComparison.Ordinal);
    }
}

public sealed record McpSessionRegistration(
    string ServerName,
    string RequestedVersion,
    McpServerInfo ServerInfo,
    string RuntimeMode,
    string Rid,
    string PreparationId,
    string LaunchCommand,
    string? ConfigurationFile,
    string? ConfigurationSha256,
    string? RuntimeDescriptorFile,
    string? RuntimeDescriptorSha256,
    DateTimeOffset RegisteredAtUtc,
    string HealthStatus)
{
    public string LaunchFile { get; init; } = string.Empty;

    public string LaunchArgumentsSha256 { get; init; } = string.Empty;
}

public sealed record McpSessionRegistrationResult(
    string Status,
    McpSessionRegistration Registration);

public sealed class McpSessionRegistry : IAsyncDisposable
{
    private readonly Dictionary<string, McpSessionEntry> _sessions = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private readonly object _sessionSync = new();
    private bool _disposed;

    public async Task<McpSessionRegistrationResult> RegisterAsync(
        string serverName,
        string requestedVersion,
        LoomRuntimeLaunchCommand launch,
        string requiredTool,
        string runtimeDescriptorSha256,
        string? configurationFile = null,
        string? configurationSha256 = null,
        string? runtimeDescriptorFile = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
            ArgumentException.ThrowIfNullOrWhiteSpace(requestedVersion);
            ArgumentNullException.ThrowIfNull(launch);
            ArgumentException.ThrowIfNullOrWhiteSpace(requiredTool);
            ArgumentException.ThrowIfNullOrWhiteSpace(runtimeDescriptorSha256);
            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
            if (effectiveTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "MCP session timeout must be positive.");
            }

            McpSessionEntry? existing;
            lock (_sessionSync)
            {
                _sessions.TryGetValue(serverName, out existing);
            }

            var staleReplaced = false;
            if (existing is not null)
            {
                var reusable = existing.Client.ServerInfo is not null
                    && McpSessionVersionPolicy.IsReusable(
                        requestedVersion,
                        launch,
                        existing.Registration,
                        runtimeDescriptorSha256)
                    && existing.Client.Tools.Contains(requiredTool)
                    && await existing.Client.PingAsync(cancellationToken).ConfigureAwait(false);
                if (reusable)
                {
                    var reusedRegistration = existing.Registration with { HealthStatus = "healthy" };
                    lock (_sessionSync)
                    {
                        _sessions[serverName] = existing with { Registration = reusedRegistration };
                    }
                    return new McpSessionRegistrationResult("reused", reusedRegistration);
                }

                staleReplaced = true;
                await existing.Client.DisposeAsync().ConfigureAwait(false);
                lock (_sessionSync)
                {
                    _sessions.Remove(serverName);
                }
            }

            var client = await McpStdioClient.StartAsync(launch, effectiveTimeout, cancellationToken).ConfigureAwait(false);
            try
            {
                var serverInfo = await client.InitializeAsync(cancellationToken).ConfigureAwait(false);
                if (!McpSessionVersionPolicy.IsSameVersion(requestedVersion, serverInfo))
                {
                    throw new McpHandshakeUnsupportedException(
                        $"MCP server version '{serverInfo.Version}' does not match requested version '{requestedVersion}'.");
                }

                var tools = await client.ListToolsAsync(cancellationToken).ConfigureAwait(false);
                if (!tools.Contains(requiredTool))
                {
                    throw new McpToolUnavailableException($"MCP tool '{requiredTool}' was not discovered.");
                }

                var registration = new McpSessionRegistration(
                    serverName,
                    LoomRuntimeCatalog.NormalizeVersion(requestedVersion),
                    serverInfo,
                    launch.RuntimeMode,
                    launch.Rid,
                    launch.PreparationId,
                    launch.Command,
                    configurationFile,
                    configurationSha256,
                    runtimeDescriptorFile,
                    runtimeDescriptorSha256,
                    DateTimeOffset.UtcNow,
                    "healthy")
                {
                    LaunchFile = Path.GetFullPath(launch.LaunchFile),
                    LaunchArgumentsSha256 = McpRuntimeBindingPolicy.ComputeLaunchArgumentsSha256(launch.Arguments),
                };
                lock (_sessionSync)
                {
                    _sessions[serverName] = new McpSessionEntry(client, registration);
                }
                return new McpSessionRegistrationResult(staleReplaced ? "stale-replaced" : "created", registration);
            }
            catch
            {
                await client.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    public bool TryGet(string serverName, out McpSessionRegistration? registration)
    {
        ThrowIfDisposed();
        lock (_sessionSync)
        {
            if (_sessions.TryGetValue(serverName, out var entry))
            {
                registration = entry.Registration;
                return true;
            }
        }

        registration = null;
        return false;
    }

    public async Task<McpToolResult> CallToolAsync(
        string serverName,
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            McpSessionEntry? entry;
            lock (_sessionSync)
            {
                _sessions.TryGetValue(serverName, out entry);
            }

            if (entry is null)
            {
                throw new InvalidOperationException($"MCP session '{serverName}' is not registered.");
            }

            var result = await entry.Client.CallToolAsync(toolName, arguments, cancellationToken).ConfigureAwait(false);
            lock (_sessionSync)
            {
                _sessions[serverName] = entry with
                {
                    Registration = entry.Registration with { HealthStatus = "healthy" },
                };
            }
            return result;
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _sessionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            McpSessionEntry[] sessions;
            lock (_sessionSync)
            {
                sessions = _sessions.Values.ToArray();
                _sessions.Clear();
            }

            foreach (var session in sessions)
            {
                await session.Client.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(McpSessionRegistry));
        }
    }

    private sealed record McpSessionEntry(McpStdioClient Client, McpSessionRegistration Registration);
}
