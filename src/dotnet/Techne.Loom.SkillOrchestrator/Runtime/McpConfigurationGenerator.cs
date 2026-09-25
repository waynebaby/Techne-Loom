using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Techne.Loom.Common.Runtime;

namespace Techne.Loom.SkillOrchestrator.Runtime;

public sealed record McpAppHostConfigurationGenerationResult(
    string Status,
    string Format,
    string OutputFile,
    string PackageId,
    string RuntimeVersion,
    string Rid,
    string RuntimeRoot,
    string LaunchFile,
    string Command,
    IReadOnlyList<string> Arguments,
    string AppHostSha256,
    string ConfigSha256,
    string ServerName);

public static class McpConfigurationGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static McpAppHostConfigurationGenerationResult GenerateForApphost(
        LoomRuntimeIdentity identity,
        string? outputFile,
        string format,
        string? serverName,
        bool force)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var normalizedFormat = format.Trim().ToLowerInvariant();
        if (normalizedFormat is not "vscode" and not "claude")
        {
            throw new ArgumentException("MCP configuration format must be 'vscode' or 'claude'.", nameof(format));
        }

        var launch = LoomRuntimeLaunch.CreateMcpCommand(identity);
        var expectedServerName = LoomRuntimeCatalog.GetMcpServerName(identity.Product, identity.Version);
        var resolvedServerName = string.IsNullOrWhiteSpace(serverName) ? expectedServerName : serverName.Trim();
        if (!string.Equals(resolvedServerName, expectedServerName, StringComparison.Ordinal))
        {
            throw new ArgumentException($"MCP server name must be '{expectedServerName}' for the selected product and exact runtime version.", nameof(serverName));
        }

        var fullOutputFile = string.IsNullOrWhiteSpace(outputFile)
            ? LoomRuntimeCatalog.GetDefaultUserMcpConfigurationPath(identity.Product, identity.Version, normalizedFormat)
            : Path.GetFullPath(outputFile);
        var sectionName = normalizedFormat == "vscode" ? "servers" : "mcpServers";
        var configuration = LoadConfiguration(fullOutputFile, sectionName);
        var servers = GetServerMap(configuration, sectionName);
        var server = CreateServerNode(normalizedFormat, launch);
        var existingServer = servers[resolvedServerName];
        var sameServer = existingServer is not null && JsonNode.DeepEquals(existingServer, server);
        var existed = File.Exists(fullOutputFile);
        var status = force
            ? existed ? "updated" : "generated"
            : sameServer ? "reused" : existed ? "updated" : "generated";
        servers[resolvedServerName] = server;
        var json = configuration.ToJsonString(JsonOptions) + Environment.NewLine;

        if (status != "reused")
        {
            var directory = Path.GetDirectoryName(fullOutputFile);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var temporaryFile = fullOutputFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryFile, json, new UTF8Encoding(false));
                File.Move(temporaryFile, fullOutputFile, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryFile)) File.Delete(temporaryFile);
            }
        }

        return new McpAppHostConfigurationGenerationResult(
            status,
            normalizedFormat,
            fullOutputFile,
            identity.PackageId,
            identity.Version,
            identity.RuntimeIdentifier,
            launch.WorkingDirectory,
            launch.LaunchFile,
            launch.Command,
            launch.Arguments,
            McpRuntimeBindingPolicy.ComputeExecutableSha256(identity.LaunchFile),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant(),
            resolvedServerName);
    }

    private static JsonObject CreateServerNode(string format, LoomRuntimeLaunchCommand launch)
    {
        var server = new JsonObject
        {
            ["command"] = launch.Command,
            ["args"] = JsonSerializer.SerializeToNode(launch.Arguments, JsonOptions),
        };
        if (format == "vscode")
        {
            server["type"] = "stdio";
        }

        if (launch.EnvironmentVariables is not null)
        {
            server["env"] = JsonSerializer.SerializeToNode(launch.EnvironmentVariables, JsonOptions);
        }

        return server;
    }

    private static JsonObject LoadConfiguration(string outputFile, string sectionName)
    {
        if (!File.Exists(outputFile))
        {
            return new JsonObject { [sectionName] = new JsonObject() };
        }

        var json = File.ReadAllText(outputFile);
        try
        {
            return JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidOperationException($"MCP configuration '{outputFile}' must contain a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"MCP configuration '{outputFile}' is not valid JSON.", exception);
        }
    }

    private static JsonObject GetServerMap(JsonObject configuration, string sectionName)
    {
        if (configuration[sectionName] is JsonObject servers)
        {
            return servers;
        }

        if (configuration[sectionName] is not null)
        {
            throw new InvalidOperationException($"MCP configuration property '{sectionName}' must be a JSON object.");
        }

        var created = new JsonObject();
        configuration[sectionName] = created;
        return created;
    }
}