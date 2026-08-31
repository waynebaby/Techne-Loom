using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Techne.Loom.Common.Mcp;

public sealed record McpStdioServerOptions(string ServerName, string ServerVersion)
{
    public string ProtocolVersion { get; init; } = "2025-06-18";

    public IReadOnlyList<string> SupportedProtocolVersions { get; init; } =
        ["2025-06-18", "2025-03-26", "2024-11-05"];

    public string? Instructions { get; init; }
}

public sealed class McpStdioServer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly McpToolRegistry _registry;
    private readonly McpStdioServerOptions _options;
    private readonly TextReader _input;
    private const int NotInitializedErrorCode = -32002;
    private readonly TextWriter _output;
    private bool _initializeStarted;
    private bool _initialized;

    public McpStdioServer(
        McpToolRegistry registry,
        McpStdioServerOptions options,
        TextReader? input = null,
        TextWriter? output = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.ServerName))
        {
            throw new ArgumentException("An MCP server name is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(_options.ServerVersion))
        {
            throw new ArgumentException("An MCP server version is required.", nameof(options));
        }

        _input = input ?? Console.In;
        _output = output ?? Console.Out;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var line = await _input.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is null)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                JsonNode? response;
                try
                {
                    using var document = JsonDocument.Parse(line);
                    response = await HandleRequestAsync(document.RootElement, ct).ConfigureAwait(false);
                }
                catch (JsonException)
                {
                    response = CreateErrorResponse(null, -32700, "Invalid JSON.");
                }

                if (response is not null)
                {
                    await WriteResponseAsync(response).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task<JsonNode?> HandleRequestAsync(JsonElement request, CancellationToken ct)
    {
        if (request.ValueKind != JsonValueKind.Object)
        {
            return CreateErrorResponse(null, -32600, "Request must be a JSON object.");
        }

        var hasId = request.TryGetProperty("id", out var idElement);
        var id = hasId && IsValidId(idElement) ? JsonNode.Parse(idElement.GetRawText()) : null;
        if (hasId && !IsValidId(idElement))
        {
            return CreateErrorResponse(null, -32600, "Request id must be a string, number, or null.");
        }

        if (!request.TryGetProperty("jsonrpc", out var jsonRpc)
            || jsonRpc.ValueKind != JsonValueKind.String
            || !string.Equals(jsonRpc.GetString(), "2.0", StringComparison.Ordinal)
            || !request.TryGetProperty("method", out var methodElement)
            || methodElement.ValueKind != JsonValueKind.String)
        {
            return CreateErrorResponse(id, -32600, "Request must use JSON-RPC 2.0 and include a method.");
        }

        var method = methodElement.GetString()!;
        var parameters = request.TryGetProperty("params", out var paramsElement) ? paramsElement : default;
        var isNotification = !hasId;

        switch (method)
        {
            case "initialize":
                if (isNotification)
                {
                    return null;
                }

                if (_initializeStarted)
                {
                    return CreateErrorResponse(id, -32600, "The MCP session has already received initialize.", isNotification);
                }

                if (!TryValidateInitializeParameters(parameters, out var initializeError))
                {
                    return CreateErrorResponse(id, -32602, initializeError!, isNotification);
                }

                return CreateSuccessResponse(id, HandleInitialize(parameters), isNotification);
            case "notifications/initialized":
                if (hasId)
                {
                    return CreateErrorResponse(id, -32600, "notifications/initialized must be sent without an id.");
                }

                if (_initializeStarted)
                {
                    _initialized = true;
                }

                return null;
            case "ping":
                return CreateSuccessResponse(id, new JsonObject(), isNotification);
            case "tools/list":
                if (!_initialized)
                {
                    return CreateErrorResponse(id, NotInitializedErrorCode, "The MCP session must receive notifications/initialized before listing tools.", isNotification);
                }

                return CreateSuccessResponse(id, HandleToolsList(), isNotification);
            case "tools/call":
                if (!_initialized)
                {
                    return CreateErrorResponse(id, NotInitializedErrorCode, "The MCP session must receive notifications/initialized before calling tools.", isNotification);
                }

                return await HandleToolCallAsync(id, parameters, isNotification, ct).ConfigureAwait(false);
            case "notifications/cancelled":
                if (hasId)
                {
                    return CreateErrorResponse(id, -32600, "notifications/cancelled must be sent without an id.");
                }

                return null;
            default:
                return CreateErrorResponse(id, -32601, $"Method '{method}' was not found.", isNotification);
        }
    }

    private static bool TryValidateInitializeParameters(JsonElement parameters, out string? error)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
        {
            error = "initialize params must be an object.";
            return false;
        }

        if (!parameters.TryGetProperty("protocolVersion", out var protocolVersion)
            || protocolVersion.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(protocolVersion.GetString()))
        {
            error = "initialize requires a non-empty protocolVersion.";
            return false;
        }

        if (!parameters.TryGetProperty("capabilities", out var capabilities)
            || capabilities.ValueKind != JsonValueKind.Object)
        {
            error = "initialize requires an object capabilities value.";
            return false;
        }

        if (!parameters.TryGetProperty("clientInfo", out var clientInfo)
            || clientInfo.ValueKind != JsonValueKind.Object)
        {
            error = "initialize requires an object clientInfo value.";
            return false;
        }

        error = null;
        return true;
    }

    private JsonNode HandleInitialize(JsonElement parameters)
    {
        _initializeStarted = true;
        var requestedVersion = GetOptionalString(parameters, "protocolVersion");
        var protocolVersion = _options.SupportedProtocolVersions.Contains(requestedVersion ?? string.Empty, StringComparer.Ordinal)
            ? requestedVersion!
            : _options.ProtocolVersion;
        var result = new JsonObject
        {
            ["protocolVersion"] = protocolVersion,
            ["capabilities"] = new JsonObject
            {
                ["tools"] = new JsonObject(),
            },
            ["serverInfo"] = new JsonObject
            {
                ["name"] = _options.ServerName,
                ["version"] = _options.ServerVersion,
            },
        };
        if (!string.IsNullOrWhiteSpace(_options.Instructions))
        {
            result["instructions"] = _options.Instructions;
        }

        return result;
    }

    private JsonNode HandleToolsList()
    {
        var tools = new JsonArray();
        foreach (var definition in _registry.ListDefinitions())
        {
            tools.Add(JsonSerializer.SerializeToNode(definition, JsonOptions));
        }

        return new JsonObject { ["tools"] = tools };
    }

    private async Task<JsonNode?> HandleToolCallAsync(
        JsonNode? id,
        JsonElement parameters,
        bool isNotification,
        CancellationToken ct)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
        {
            return CreateErrorResponse(id, -32602, "tools/call params must be an object.", isNotification);
        }

        var name = GetOptionalString(parameters, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return CreateErrorResponse(id, -32602, "tools/call requires a tool name.", isNotification);
        }

        if (!_registry.TryGet(name, out var tool) || tool is null)
        {
            return CreateErrorResponse(id, -32602, $"Tool '{name}' was not found.", isNotification);
        }

        var arguments = parameters.TryGetProperty("arguments", out var argumentsElement)
            ? argumentsElement
            : default;
        if (arguments.ValueKind is not (JsonValueKind.Object or JsonValueKind.Undefined))
        {
            return CreateErrorResponse(id, -32602, "tools/call arguments must be an object.", isNotification);
        }

        McpToolResult result;
        try
        {
            result = await tool.InvokeAsync(arguments, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            result = McpToolResults.Error(ex);
        }

        return CreateSuccessResponse(
            id,
            JsonSerializer.SerializeToNode(result, JsonOptions) ?? new JsonObject(),
            isNotification);
    }

    private async Task WriteResponseAsync(JsonNode response)
    {
        await _output.WriteLineAsync(response.ToJsonString(JsonOptions)).ConfigureAwait(false);
        await _output.FlushAsync().ConfigureAwait(false);
    }

    private static JsonNode? CreateSuccessResponse(JsonNode? id, JsonNode result, bool isNotification)
    {
        if (isNotification)
        {
            return null;
        }

        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result,
        };
    }

    private static JsonNode? CreateErrorResponse(JsonNode? id, int code, string message, bool isNotification = false)
    {
        if (isNotification)
        {
            return null;
        }

        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message,
            },
        };
    }

    private static bool IsValidId(JsonElement id)
        => id.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.Null;

    private static string? GetOptionalString(JsonElement value, string propertyName)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }
}

public static class McpToolResults
{
    public static McpToolResult Json(object value, JsonSerializerOptions? options = null)
    {
        var text = System.Text.Json.JsonSerializer.Serialize(value, options ?? new JsonSerializerOptions { WriteIndented = false });
        return new McpToolResult([new McpTextContent("text", text)]);
    }

    public static McpToolResult Error(Exception exception)
    {
        var message = exception switch
        {
            McpToolInputException => exception.Message,
            JsonException => "The input file is not valid JSON.",
            FileNotFoundException or DirectoryNotFoundException => "The requested input file was not found.",
            _ => "MCP tool execution failed.",
        };
        return Error(message);
    }

    public static McpToolResult Error(string message)
        => new([new McpTextContent("text", message)], IsError: true);
}
