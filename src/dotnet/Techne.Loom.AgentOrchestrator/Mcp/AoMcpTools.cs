using System.ComponentModel;
using ModelContextProtocol.Server;
using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.AgentOrchestrator.Runtime;

namespace Techne.Loom.AgentOrchestrator.Mcp;

[McpServerToolType]
public sealed class AoMcpTools
{
    private readonly AoRuntimeService _runtime;

    public AoMcpTools(AoRuntimeService runtime)
    {
        _runtime = runtime;
    }

    [McpServerTool, Description("Run AO with objective, context, and a session directory. AO generates a session_id and persists derived workflow and event-log files for that session.")]
    public Task<AoControlPayload> AoRun(
        [Description("Objective text for this run.")] string objective,
        [Description("Context dictionary for this run.")] Dictionary<string, object?> context,
        [Description("Directory where AO session artifacts are stored.")] string sessionDirectory,
        [Description("Optional per-call host execution context for future weave-out routes.")] AoInvocationContext? invocation_context = null)
    {
        return _runtime.RunAsync(objective, context, sessionDirectory, invocation_context);
    }

    [McpServerTool, Description("Resume AO from a structured result envelope.")]
    public Task<AoControlPayload> AoResume(
        [Description("Directory where AO session artifacts are stored.")] string sessionDirectory,
        [Description("Stable AO session identifier returned by ao_run.")] string sessionId,
        [Description("Resume envelope transition_id.")] string transitionId,
        [Description("Optional resume envelope correlation_key.")] string? correlationKey,
        [Description("Resume envelope payload object.")] Dictionary<string, object?>? payload,
        [Description("Optional per-call host execution context for future weave-out routes.")] AoInvocationContext? invocation_context = null)
    {
        return _runtime.ResumeAsync(
            sessionDirectory,
            sessionId,
            new AoResumeEnvelope(transitionId, correlationKey, payload),
            invocation_context);
    }
}
