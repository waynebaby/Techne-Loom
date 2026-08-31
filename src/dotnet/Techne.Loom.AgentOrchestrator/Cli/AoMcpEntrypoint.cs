using Techne.Loom.Common.Mcp;

namespace Techne.Loom.AgentOrchestrator.Cli;

internal static class AoMcpEntrypoint
{
    public static async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        if (args.Count != 0)
        {
            throw new InvalidOperationException("The AO MCP entrypoint only supports the local stdio transport.");
        }

        var registry = WorkflowMcpToolSet.Create("ao");
        var options = new McpStdioServerOptions(
            "Loom Agent Execution Orchestrator",
            typeof(AoMcpEntrypoint).Assembly.GetName().Version?.ToString() ?? "unknown")
        {
            Instructions = "Use fragment inspection by default. Workflow files and result files are path-only inputs.",
        };
        await new McpStdioServer(registry, options).RunAsync().ConfigureAwait(false);
        return 0;
    }
}
