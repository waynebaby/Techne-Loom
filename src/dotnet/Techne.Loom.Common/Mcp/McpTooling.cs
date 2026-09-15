using System.Text.Json;
using System.Text.Json.Serialization;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.Common.Mcp;

public sealed class McpToolInputException : Exception
{
    public McpToolInputException(string message)
        : base(message)
    {
    }
}

public sealed record McpToolDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] JsonElement InputSchema);

public sealed record McpTextContent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text);

public sealed record McpToolResult(
    [property: JsonPropertyName("content")] IReadOnlyList<McpTextContent> Content,
    [property: JsonPropertyName("isError")] bool IsError = false);

public interface IMcpTool
{
    McpToolDefinition Definition { get; }
    Task<McpToolResult> InvokeAsync(JsonElement arguments, CancellationToken ct = default);
}
public sealed class DelegateMcpTool : IMcpTool
{
    private readonly Func<JsonElement, CancellationToken, Task<McpToolResult>> _handler;
    public DelegateMcpTool(
        string name,
        string description,
        string inputSchema,
        Func<JsonElement, CancellationToken, Task<McpToolResult>> handler)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An MCP tool name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("An MCP tool description is required.", nameof(description));
        ArgumentException.ThrowIfNullOrWhiteSpace(inputSchema);
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        using var document = JsonDocument.Parse(inputSchema);
        Definition = new McpToolDefinition(name, description, document.RootElement.Clone());
    }
    public McpToolDefinition Definition { get; }
    public Task<McpToolResult> InvokeAsync(JsonElement arguments, CancellationToken ct = default)
        => _handler(arguments, ct);
}
public static class McpToolArguments
{
    public static bool IsValidOperationId(string? value)
        => WorkflowOperationLedger.IsValidOperationId(value);

    public static string RequiredOperationId(JsonElement arguments, string name = "operation_id")
    {
        var value = RequiredString(arguments, name);
        if (!IsValidOperationId(value))
        {
            throw new McpToolInputException(
                $"The '{name}' argument must be 1-128 ASCII characters using only letters, digits, '.', '_', or '-'.");
        }

        return value;
    }

    public static string RequiredString(JsonElement arguments, string name)
    {
        var value = OptionalString(arguments, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new McpToolInputException($"The '{name}' argument is required.");
        }
        return value;
    }
    public static string RequiredExistingPath(JsonElement arguments, string name)
    {
        var value = RequiredString(arguments, name);
        var trimmed = value.TrimStart();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            throw new McpToolInputException($"The '{name}' argument accepts a file path only; inline JSON is not supported.");
        }
        var path = Path.GetFullPath(value);
        if (!File.Exists(path))
        {
            throw new McpToolInputException("The requested input file was not found.");
        }
        return path;
    }
    public static string? OptionalString(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty(name, out var value))
        {
            return null;
        }
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new McpToolInputException($"The '{name}' argument must be a string.");
        }
        return value.GetString();
    }
}
public sealed class McpToolRegistry
{
    private readonly Dictionary<string, IMcpTool> _tools = new(StringComparer.Ordinal);

    public void Register(IMcpTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (string.IsNullOrWhiteSpace(tool.Definition.Name))
        {
            throw new ArgumentException("MCP tool names must not be empty.", nameof(tool));
        }

        if (!_tools.TryAdd(tool.Definition.Name, tool))
        {
            throw new InvalidOperationException($"MCP tool '{tool.Definition.Name}' is already registered.");
        }
    }

    public bool TryGet(string name, out IMcpTool? tool)
        => _tools.TryGetValue(name, out tool);

    public IReadOnlyList<McpToolDefinition> ListDefinitions()
        => _tools.Values.Select(static tool => tool.Definition).ToArray();
}
