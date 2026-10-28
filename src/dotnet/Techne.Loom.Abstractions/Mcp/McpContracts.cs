using System.Text.Json;
using System.Text.Json.Serialization;
using Techne.Loom.Common.Runtime;

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

public sealed record McpServerInfo(string Name, string Version, string ProtocolVersion);

public enum McpDispatchStage
{
    PreDispatch,
    Dispatched,
    Unknown,
}

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

public sealed record McpStdioServerOptions(string ServerName, string ServerVersion)
{
    public string ProtocolVersion { get; init; } = "2025-06-18";

    public IReadOnlyList<string> SupportedProtocolVersions { get; init; } =
        ["2025-06-18", "2025-03-26", "2024-11-05"];

    public string? Instructions { get; init; }

    public McpRuntimeBinding? RuntimeBinding { get; init; }

    public LoomRuntimeProduct? ExpectedProduct { get; init; }

    public bool RequireRuntimeBinding { get; init; }
}

public sealed record McpSessionRegistration(
    string ServerName,
    string RequestedVersion,
    McpServerInfo ServerInfo,
    string RuntimeVersion,
    string Rid,
    string LaunchCommand,
    string? ConfigurationFile,
    string? ConfigurationSha256,
    DateTimeOffset RegisteredAtUtc,
    string HealthStatus)
{
    public string LaunchFile { get; init; } = string.Empty;

    public string LaunchArgumentsSha256 { get; init; } = string.Empty;

    public string ExecutableSha256 { get; init; } = string.Empty;
}

public sealed record McpSessionRegistrationResult(
    string Status,
    McpSessionRegistration Registration);

public sealed record McpWorkflowOperationResult(
    McpToolResult Result,
    string Transport,
    McpDispatchStage DispatchStage,
    string? FallbackReason,
    McpServerInfo? ServerInfo);

public sealed record McpWorkflowResumeEnvelope(
    [property: JsonPropertyName("transition_id")] string TransitionId,
    [property: JsonPropertyName("correlation_key")] string? CorrelationKey,
    [property: JsonPropertyName("payload")] Dictionary<string, object?>? Payload,
    [property: JsonPropertyName("result_id")] string? ResultId = null);
