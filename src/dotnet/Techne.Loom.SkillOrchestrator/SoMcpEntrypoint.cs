using Techne.Loom.Common.Mcp;

internal static class SoMcpEntrypoint
{
    public static async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        if (args.Count != 0)
        {
            throw new InvalidOperationException("The SO MCP entrypoint only supports the local stdio transport.");
        }

        var registry = WorkflowMcpToolSet.Create("so");
        var options = new McpStdioServerOptions(
            "Loom Skill Orchestrator",
            typeof(SoMcpEntrypoint).Assembly.GetName().Version?.ToString() ?? "unknown")
        {
            Instructions = "Use fragment inspection by default. Workflow files and result files are path-only inputs.",
        };
        await new McpStdioServer(registry, options).RunAsync().ConfigureAwait(false);
        return 0;
    }
}
