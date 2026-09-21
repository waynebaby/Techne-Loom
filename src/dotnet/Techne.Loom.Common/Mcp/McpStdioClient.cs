using System.ComponentModel;
using System.Diagnostics;

using System.Text;

using System.Text.Json;

using System.Text.Json.Nodes;

using System.Text.Json.Serialization;

using Techne.Loom.Common.Runtime;



namespace Techne.Loom.Common.Mcp;



public sealed record McpServerInfo(string Name, string Version, string ProtocolVersion);



public abstract class McpDispatchException : InvalidOperationException

{

    protected McpDispatchException(string message, string reason, McpDispatchStage stage, Exception? innerException = null)

        : base(message, innerException)

    {

        Reason = reason;

        Stage = stage;

    }



    public string Reason { get; }



    public McpDispatchStage Stage { get; }

}



public sealed class McpTransportUnavailableException : McpDispatchException

{

    public McpTransportUnavailableException(string message, McpDispatchStage stage = McpDispatchStage.PreDispatch, Exception? innerException = null)

        : base(message, "mcp_transport_unavailable", stage, innerException)

    {

    }

}



public sealed class McpHandshakeUnsupportedException : McpDispatchException

{

    public McpHandshakeUnsupportedException(string message, McpDispatchStage stage = McpDispatchStage.PreDispatch, Exception? innerException = null)

        : base(message, "mcp_handshake_unsupported", stage, innerException)

    {

    }

}



public sealed class McpToolUnavailableException : McpDispatchException

{

    public McpToolUnavailableException(string message, McpDispatchStage stage = McpDispatchStage.PreDispatch, Exception? innerException = null)

        : base(message, "mcp_tool_unavailable", stage, innerException)

    {

    }

}



public sealed class McpApplicationException : McpDispatchException

{

    public McpApplicationException(string message, McpDispatchStage stage = McpDispatchStage.Dispatched, Exception? innerException = null)

        : base(message, "mcp_application_failed", stage, innerException)

    {

    }

}



public sealed class McpStdioClient : IAsyncDisposable

{

    private static readonly JsonSerializerOptions WireOptions = new()

    {

        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        WriteIndented = false,

    };



    private readonly Process _process;

    private readonly StreamWriter _input;

    private readonly StreamReader _output;

    private readonly Task<string> _standardErrorTask;

    private readonly TimeSpan _timeout;

    private readonly HashSet<string> _tools = new(StringComparer.Ordinal);

    private long _nextRequestId;

    private bool _initialized;

    private bool _disposed;
    private readonly SemaphoreSlim _requestGate = new(1, 1);



    private McpStdioClient(Process process, TimeSpan timeout)

    {

        _process = process;

        _timeout = timeout;

        _input = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false))

        {

            AutoFlush = true,

        };

        _output = new StreamReader(process.StandardOutput.BaseStream, new UTF8Encoding(false));

        _standardErrorTask = process.StandardError.ReadToEndAsync();

    }



    public McpDispatchStage DispatchStage { get; private set; } = McpDispatchStage.PreDispatch;



    public McpServerInfo? ServerInfo { get; private set; }



    public IReadOnlySet<string> Tools => _tools;



    public static Task<McpStdioClient> StartAsync(

        LoomRuntimeLaunchCommand launch,

        TimeSpan timeout,

        CancellationToken cancellationToken = default)

    {

        ArgumentNullException.ThrowIfNull(launch);

        if (timeout <= TimeSpan.Zero)

        {

            throw new ArgumentOutOfRangeException(nameof(timeout), "MCP timeout must be positive.");

        }



        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo

        {

            FileName = launch.Command,

            WorkingDirectory = launch.WorkingDirectory,

            RedirectStandardInput = true,

            RedirectStandardOutput = true,

            RedirectStandardError = true,

            UseShellExecute = false,

            CreateNoWindow = true,

            StandardInputEncoding = new UTF8Encoding(false),

            StandardOutputEncoding = new UTF8Encoding(false),

            StandardErrorEncoding = new UTF8Encoding(false),

        };

        foreach (var argument in launch.Arguments)

        {

            startInfo.ArgumentList.Add(argument);

        }



        if (launch.EnvironmentVariables is not null)
        {
            foreach (var environmentVariable in launch.EnvironmentVariables)
            {
                startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
            }
        }
        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        try

        {

            if (!process.Start())

            {

                process.Dispose();

                throw new McpTransportUnavailableException($"MCP process '{launch.Command}' could not start.");

            }

        }

        catch (McpTransportUnavailableException)

        {

            throw;

        }

        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)

        {

            process.Dispose();

            throw new McpTransportUnavailableException($"MCP process '{launch.Command}' could not start.", McpDispatchStage.PreDispatch, exception);

        }



        return Task.FromResult(new McpStdioClient(process, timeout));

    }



    public async Task<McpServerInfo> InitializeAsync(CancellationToken cancellationToken = default)

    {

        ThrowIfDisposed();

        if (_initialized && ServerInfo is not null)

        {

            return ServerInfo;

        }



        var response = await SendRequestAsync(

            "initialize",

            new Dictionary<string, object?>(StringComparer.Ordinal)

            {

                ["protocolVersion"] = "2025-06-18",

                ["capabilities"] = new Dictionary<string, object?>(StringComparer.Ordinal),

                ["clientInfo"] = new Dictionary<string, object?>(StringComparer.Ordinal)

                {

                    ["name"] = "techne-loom-client",

                    ["version"] = "1",

                },

            },

            markOperationDispatched: false,

            cancellationToken).ConfigureAwait(false);



        try

        {

            var result = RequiredObject(response, "result");

            var protocolVersion = RequiredString(result, "protocolVersion");

            var serverInfo = RequiredObject(result, "serverInfo");

            var name = RequiredString(serverInfo, "name");

            var version = RequiredString(serverInfo, "version");

            ServerInfo = new McpServerInfo(name, version, protocolVersion);

        }

        catch (McpHandshakeUnsupportedException)

        {

            throw;

        }

        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)

        {

            throw new McpHandshakeUnsupportedException("The MCP initialize response did not contain a valid serverInfo object.", McpDispatchStage.PreDispatch, exception);

        }



        try

        {

            await SendNotificationAsync(

                "notifications/initialized",

                new Dictionary<string, object?>(StringComparer.Ordinal),

                cancellationToken).ConfigureAwait(false);

        }

        catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException)

        {

            throw new McpHandshakeUnsupportedException("The MCP initialized notification could not be sent.", McpDispatchStage.PreDispatch, exception);

        }



        _initialized = true;

        return ServerInfo;

    }



    public async Task<IReadOnlySet<string>> ListToolsAsync(CancellationToken cancellationToken = default)

    {

        ThrowIfDisposed();

        EnsureInitialized();

        JsonElement response;

        try

        {

            response = await SendRequestAsync(

                "tools/list",

                new Dictionary<string, object?>(StringComparer.Ordinal),

                markOperationDispatched: false,

                cancellationToken).ConfigureAwait(false);

        }

        catch (McpDispatchException exception) when (exception.Stage == McpDispatchStage.PreDispatch)

        {

            throw new McpToolUnavailableException("The MCP tool list could not be read before tool dispatch.", McpDispatchStage.PreDispatch, exception);

        }



        try

        {

            var result = RequiredObject(response, "result");

            if (!result.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Array)

            {

                throw new InvalidOperationException("The MCP tools/list response did not contain a tools array.");

            }



            _tools.Clear();

            foreach (var tool in tools.EnumerateArray())

            {

                if (tool.ValueKind == JsonValueKind.Object

                    && tool.TryGetProperty("name", out var name)

                    && name.ValueKind == JsonValueKind.String

                    && !string.IsNullOrWhiteSpace(name.GetString()))

                {

                    _tools.Add(name.GetString()!);

                }

            }



            return _tools;

        }

        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)

        {

            throw new McpToolUnavailableException("The MCP tools/list response was not supported.", McpDispatchStage.PreDispatch, exception);

        }

    }



    public async Task<McpToolResult> CallToolAsync(

        string toolName,

        JsonElement arguments,

        CancellationToken cancellationToken = default)

    {

        ThrowIfDisposed();

        EnsureInitialized();

        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (!_tools.Contains(toolName))

        {

            throw new McpToolUnavailableException($"The MCP tool '{toolName}' was not discovered before dispatch.");

        }



        if (arguments.ValueKind != JsonValueKind.Object)

        {

            throw new ArgumentException("MCP tool arguments must be an object.", nameof(arguments));

        }



        var response = await SendRequestAsync(

            "tools/call",

            new Dictionary<string, object?>(StringComparer.Ordinal)

            {

                ["name"] = toolName,

                ["arguments"] = arguments,

            },

            markOperationDispatched: true,

            cancellationToken).ConfigureAwait(false);

        try

        {

            var result = RequiredObject(response, "result");

            return JsonSerializer.Deserialize<McpToolResult>(result.GetRawText(), WireOptions)

                ?? throw new InvalidOperationException("The MCP tool result was empty.");

        }

        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)

        {

            throw new McpApplicationException("The MCP tool response did not contain a valid result.", DispatchStage, exception);

        }

    }



    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)

    {

        ThrowIfDisposed();

        try

        {

            await SendRequestAsync(

                "ping",

                new Dictionary<string, object?>(StringComparer.Ordinal),

                markOperationDispatched: false,

                cancellationToken).ConfigureAwait(false);

            return true;

        }

        catch (McpDispatchException)

        {

            return false;

        }

    }



    public async ValueTask DisposeAsync()
    {
        await _requestGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _input.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
            }

            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (Win32Exception)
            {
            }

            try
            {
                await _process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
            }

            _output.Dispose();
            _process.Dispose();
            try
            {
                await _standardErrorTask.ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
        }
        finally
        {
            _requestGate.Release();
        }
    }
    private async Task<JsonElement> SendRequestAsync(
        string method,
        object parameters,
        bool markOperationDispatched,
        CancellationToken cancellationToken)
    {
        await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var requestId = Interlocked.Increment(ref _nextRequestId);
            var request = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = requestId,
                ["method"] = method,
                ["params"] = JsonSerializer.SerializeToNode(parameters, WireOptions),
            };
            if (markOperationDispatched)
            {
                DispatchStage = McpDispatchStage.Dispatched;
            }

            try
            {
                await WriteLineAsync(request.ToJsonString(WireOptions), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new McpApplicationException($"MCP request '{method}' exceeded the timeout of {_timeout}.", DispatchStage);
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException)
            {
                if (markOperationDispatched)
                {
                    throw new McpApplicationException($"MCP request '{method}' could not be sent after dispatch.", DispatchStage, exception);
                }

                throw new McpHandshakeUnsupportedException($"MCP request '{method}' could not be sent.", McpDispatchStage.PreDispatch, exception);
            }

            return await ReadResponseAsync(method, requestId, markOperationDispatched, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private async Task SendNotificationAsync(string method, object parameters, CancellationToken cancellationToken)
    {
        await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var request = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = JsonSerializer.SerializeToNode(parameters, WireOptions),
            };
            await WriteLineAsync(request.ToJsonString(WireOptions), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _requestGate.Release();
        }
    }
    private async Task WriteLineAsync(string value, CancellationToken cancellationToken)

    {

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeoutSource.CancelAfter(_timeout);

        await _input.WriteLineAsync(value.AsMemory(), timeoutSource.Token).ConfigureAwait(false);

        await _input.FlushAsync(timeoutSource.Token).ConfigureAwait(false);

    }



    private async Task<JsonElement> ReadResponseAsync(
        string method,
        long requestId,
        bool operationDispatched,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);

        while (true)
        {
            string? line;
            try
            {
                line = await _output.ReadLineAsync(timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw operationDispatched
                    ? new McpApplicationException($"MCP request '{method}' exceeded the timeout of {_timeout}.", DispatchStage)
                    : new McpHandshakeUnsupportedException($"MCP request '{method}' exceeded the timeout of {_timeout}.", McpDispatchStage.PreDispatch);
            }
            catch (IOException exception)
            {
                throw operationDispatched
                    ? new McpApplicationException($"MCP request '{method}' could not read a response.", DispatchStage, exception)
                    : new McpHandshakeUnsupportedException($"MCP request '{method}' could not read a response.", McpDispatchStage.PreDispatch, exception);
            }

            if (line is null)
            {
                var message = $"MCP process exited before responding to '{method}'.";
                throw operationDispatched
                    ? new McpApplicationException(message, DispatchStage)
                    : new McpHandshakeUnsupportedException(message, McpDispatchStage.PreDispatch);
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("id", out var responseId))
                {
                    if (root.TryGetProperty("method", out var notificationMethod)
                        && notificationMethod.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(notificationMethod.GetString()))
                    {
                        continue;
                    }

                    throw new InvalidOperationException("The MCP response did not contain a matching request id.");
                }

                if (responseId.ValueKind != JsonValueKind.Number
                    || !responseId.TryGetInt64(out var actualId)
                    || actualId != requestId)
                {
                    throw new InvalidOperationException("The MCP response id did not match the request.");
                }

                if (root.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
                {
                    var message = error.ValueKind == JsonValueKind.Object
                        && error.TryGetProperty("message", out var errorMessage)
                        && errorMessage.ValueKind == JsonValueKind.String
                        ? errorMessage.GetString()
                        : "The MCP request returned a JSON-RPC error.";
                    throw new InvalidOperationException(message);
                }

                if (!root.TryGetProperty("result", out var result))
                {
                    throw new InvalidOperationException("The MCP response did not contain a result.");
                }

                return root.Clone();
            }
            catch (JsonException exception)
            {
                if (!operationDispatched)
                {
                    throw new McpHandshakeUnsupportedException($"MCP request '{method}' returned invalid JSON.", McpDispatchStage.PreDispatch, exception);
                }

                throw new McpApplicationException($"MCP request '{method}' returned invalid JSON.", DispatchStage, exception);
            }
            catch (InvalidOperationException exception)
            {
                if (!operationDispatched)
                {
                    throw new McpHandshakeUnsupportedException($"MCP request '{method}' returned an unsupported response.", McpDispatchStage.PreDispatch, exception);
                }

                throw new McpApplicationException($"MCP request '{method}' returned an error.", DispatchStage, exception);
            }
        }
    }
    private static JsonElement RequiredObject(JsonElement value, string propertyName)

    {

        if (!value.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)

        {

            throw new McpHandshakeUnsupportedException($"The MCP response requires an object '{propertyName}'.");

        }



        return property;

    }



    private static string RequiredString(JsonElement value, string propertyName)

    {

        if (!value.TryGetProperty(propertyName, out var property)

            || property.ValueKind != JsonValueKind.String

            || string.IsNullOrWhiteSpace(property.GetString()))

        {

            throw new McpHandshakeUnsupportedException($"The MCP response requires a non-empty string '{propertyName}'.");

        }



        return property.GetString()!;

    }



    private void EnsureInitialized()

    {

        if (!_initialized || ServerInfo is null)

        {

            throw new McpHandshakeUnsupportedException("The MCP session must complete initialize before this operation.");

        }

    }



    private void ThrowIfDisposed()

    {

        if (_disposed)

        {

            throw new ObjectDisposedException(nameof(McpStdioClient));

        }

    }

}