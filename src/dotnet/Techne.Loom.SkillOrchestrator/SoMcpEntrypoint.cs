using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.Mcp;
using Techne.Loom.Common.Runtime;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Validation;

internal static class SoMcpEntrypoint
{
    public static async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        if (args.Count != 0)
        {
            throw new InvalidOperationException("The SO MCP entrypoint only supports the local stdio transport.");
        }

        var binding = McpRuntimeBindingPolicy.TryReadEnvironment();
        var registry = WorkflowMcpToolSet.Create("so");
        registry.Register(SoMcpOperations.CreateCompileTool());
        var version = GetRuntimeVersion();
        var options = new McpStdioServerOptions(
            LoomRuntimeCatalog.GetMcpServerName(LoomRuntimeProduct.SkillOrchestrator, version),
            version)
        {
            Instructions = "Use fragment inspection by default. Workflow files and result files are path-only inputs.",
            RuntimeBinding = binding,
            ExpectedProduct = LoomRuntimeProduct.SkillOrchestrator,
            RequireRuntimeBinding = McpRuntimeBindingPolicy.IsBindingRequired(),
        };
        await new McpStdioServer(registry, options).RunAsync().ConfigureAwait(false);
        return 0;
    }

    private static string GetRuntimeVersion()
    {
        var informationalVersion = typeof(SoMcpEntrypoint).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?.Split('+', 2)[0]
            .Trim();
        return LoomRuntimeCatalog.NormalizeVersion(
            string.IsNullOrWhiteSpace(informationalVersion)
                ? typeof(SoMcpEntrypoint).Assembly.GetName().Version?.ToString(3) ?? throw new InvalidOperationException("The SO MCP server has no exact assembly version.")
                : informationalVersion);
    }
}
