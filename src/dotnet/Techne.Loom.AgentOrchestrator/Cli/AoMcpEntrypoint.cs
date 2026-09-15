using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.Mcp;
using Techne.Loom.Common.Runtime;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.AgentOrchestrator.Models;

namespace Techne.Loom.AgentOrchestrator.Cli;

internal static class AoMcpEntrypoint
{
    public static async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        if (args.Count != 0)
        {
            throw new InvalidOperationException("The AO MCP entrypoint only supports the local stdio transport.");
        }

        var binding = McpRuntimeBindingPolicy.TryReadEnvironment();
        var registry = WorkflowMcpToolSet.Create("ao");
        registry.Register(AoMcpOperations.CreateCompileTool());
        var version = GetRuntimeVersion();
        var options = new McpStdioServerOptions(
            LoomRuntimeCatalog.GetMcpServerName(LoomRuntimeProduct.AgentOrchestrator, version),
            version)
        {
            Instructions = "Use fragment inspection by default. Workflow files and result files are path-only inputs.",
            RuntimeBinding = binding,
            ExpectedProduct = LoomRuntimeProduct.AgentOrchestrator,
            RequireRuntimeBinding = McpRuntimeBindingPolicy.IsBindingRequired(),
        };
        await new McpStdioServer(registry, options).RunAsync().ConfigureAwait(false);
        return 0;
    }

    private static string GetRuntimeVersion()
    {
        var informationalVersion = typeof(AoMcpEntrypoint).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?.Split('+', 2)[0]
            .Trim();
        return LoomRuntimeCatalog.NormalizeVersion(
            string.IsNullOrWhiteSpace(informationalVersion)
                ? typeof(AoMcpEntrypoint).Assembly.GetName().Version?.ToString(3) ?? throw new InvalidOperationException("The AO MCP server has no exact assembly version.")
                : informationalVersion);
    }
}
