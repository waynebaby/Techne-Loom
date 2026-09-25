using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.Common.Runtime;

namespace Techne.Loom.Common.Mcp;

public enum McpDispatchStage
{
    PreDispatch,
    Dispatched,
    Unknown,
}

public static class McpCliFallbackPolicy
{
    public static IReadOnlyList<string> AllowedReasons { get; } =
    [
        "mcp_transport_unavailable",
        "mcp_handshake_unsupported",
        "mcp_tool_unavailable",
    ];

    public static bool CanFallback(McpDispatchStage stage, string reason)
        => stage == McpDispatchStage.PreDispatch
            && AllowedReasons.Contains(reason, StringComparer.Ordinal);

    public static LoomRuntimeLaunchCommand CreateCommand(
        LoomRuntimeIdentity identity,
        IReadOnlyList<string> operationArguments,
        McpDispatchStage stage,
        string reason)
    {
        if (!CanFallback(stage, reason))
        {
            throw new InvalidOperationException(
                $"CLI fallback is allowed only before MCP dispatch with one of the approved reasons: {string.Join(", ", AllowedReasons)}.");
        }

        return LoomRuntimeLaunch.CreateCommand(identity, operationArguments);
    }

    public static LoomRuntimeLaunchCommand CreateInspectWorkflowFragmentCommand(
        LoomRuntimeIdentity identity,
        string workflowFile,
        McpDispatchStage stage,
        string reason,
        string? jsonPointer = null,
        WorkflowFragmentLimits? limits = null,
        string? operationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowFile);
        var effectiveLimits = limits ?? WorkflowFragmentLimits.Default;
        effectiveLimits.Validate();
        var arguments = new List<string>
        {
            "inspect-workflow-fragment",
            "--workflow-file",
            Path.GetFullPath(workflowFile),
        };
        if (!string.IsNullOrWhiteSpace(operationId))
        {
            arguments.Add("--operation-id");
            arguments.Add(operationId);
        }

        if (jsonPointer is not null)
        {
            arguments.Add("--json-pointer");
            arguments.Add(jsonPointer);
        }

        arguments.Add("--max-bytes");
        arguments.Add(effectiveLimits.MaxBytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
        arguments.Add("--max-array-items");
        arguments.Add(effectiveLimits.MaxArrayItems.ToString(System.Globalization.CultureInfo.InvariantCulture));
        arguments.Add("--max-object-properties");
        arguments.Add(effectiveLimits.MaxObjectProperties.ToString(System.Globalization.CultureInfo.InvariantCulture));
        arguments.Add("--max-depth");
        arguments.Add(effectiveLimits.MaxDepth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return CreateCommand(identity, arguments, stage, reason);
    }
}