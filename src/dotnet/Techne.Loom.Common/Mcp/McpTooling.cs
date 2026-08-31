using System.Text.Json;
using System.Text.Json.Serialization;

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
