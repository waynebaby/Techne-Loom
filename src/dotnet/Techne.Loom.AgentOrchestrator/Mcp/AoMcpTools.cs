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

    [McpServerTool, Description("Run AO with objective, context, workflow and event-log files.")]
    public Task<AoControlPayload> AoRun(
        [Description("Objective text for this run.")] string objective,
        [Description("Context dictionary for this run.")] Dictionary<string, object?> context,
        [Description("Mutable workflow snapshot file path.")] string workflowFile,
        [Description("Append-only AO event log file path.")] string eventLogFile)
    {
        return _runtime.RunAsync(objective, context, workflowFile, eventLogFile);
    }

    [McpServerTool, Description("Resume AO from a structured result envelope.")]
    public Task<AoControlPayload> AoResume(
        [Description("Mutable workflow snapshot file path.")] string workflowFile,
        [Description("Append-only AO event log file path.")] string eventLogFile,
        [Description("Resume envelope transition_id.")] string transitionId,
        [Description("Optional resume envelope correlation_key.")] string? correlationKey,
        [Description("Resume envelope payload object.")] Dictionary<string, object?>? payload)
    {
        return _runtime.ResumeAsync(workflowFile, eventLogFile, new AoResumeEnvelope(transitionId, correlationKey, payload));
    }
}
